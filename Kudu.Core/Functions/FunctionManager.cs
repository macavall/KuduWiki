﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kudu.Contracts.Functions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kudu.Core.Functions
{
    public class FunctionManager : IFunctionManager
    {
        private readonly IEnvironment _environment;
        private readonly ITraceFactory _traceFactory;

        public FunctionManager(IEnvironment environment, ITraceFactory traceFactory)
        {
            _environment = environment;
            _traceFactory = traceFactory;
        }

        public async Task SyncTriggersAsync(ITracer tracer = null)
        {
            tracer = tracer ?? _traceFactory.GetTracer();
            using (tracer.Step("FunctionManager.SyncTriggers"))
            {
                await PostDeploymentHelper.SyncFunctionsTriggers(new PostDeploymentTraceListener(tracer));
            }
        }

        private static string FunctionSiteExtensionVersion
        {
            get
            {
                return System.Environment.GetEnvironmentVariable(Constants.FunctionRunTimeVersion);
            }
        }

        public async Task<FunctionEnvelope> CreateOrUpdateAsync(string name, FunctionEnvelope functionEnvelope, Action setConfigChanged)
        {
            var functionDir = Path.Combine(_environment.FunctionsPath, name);

            // Make sure the function folder exists
            if (!FileSystemHelpers.DirectoryExists(functionDir))
            {
                // Cleanup any leftover artifacts from a function with the same name before.
                DeleteFunctionArtifacts(name);
                FileSystemHelpers.EnsureDirectory(functionDir);
            }

            string newConfig = null;
            string configPath = Path.Combine(functionDir, Constants.FunctionsConfigFile);
            string dataFilePath = GetFunctionTestDataFilePath(name);

            // If files are included, write them out
            if (functionEnvelope?.Files != null)
            {
                // If the config is passed in the file collection, save it and don't process it as a file
                if (functionEnvelope.Files.TryGetValue(Constants.FunctionsConfigFile, out newConfig))
                {
                    functionEnvelope.Files.Remove(Constants.FunctionsConfigFile);
                }

                // Delete all existing files in the directory. This will also delete current function.json, but it gets recreated below
                FileSystemHelpers.DeleteDirectoryContentsSafe(functionDir);

                await Task.WhenAll(functionEnvelope.Files.Select(e => FileSystemHelpers.WriteAllTextToFileAsync(Path.Combine(functionDir, e.Key), e.Value)));
            }

            // Get the config (if it was not already passed in as a file)
            if (newConfig == null && functionEnvelope?.Config != null)
            {
                newConfig = JsonConvert.SerializeObject(functionEnvelope?.Config, Formatting.Indented);
            }

            // Get the current config, if any
            string currentConfig = null;
            if (FileSystemHelpers.FileExists(configPath))
            {
                currentConfig = await FileSystemHelpers.ReadAllTextFromFileAsync(configPath);
            }

            // Save the file and set changed flag is it has changed. This helps optimize the syncTriggers call
            if (newConfig != currentConfig)
            {
                await FileSystemHelpers.WriteAllTextToFileAsync(configPath, newConfig);
                setConfigChanged();
            }

            if (functionEnvelope.TestData != null)
            {
                await FileSystemHelpers.WriteAllTextToFileAsync(dataFilePath, functionEnvelope.TestData);
            }

            return await GetFunctionConfigAsync(name); // test_data took from incoming request, it will not exceed the limit
        }

        public async Task<IEnumerable<FunctionEnvelope>> ListFunctionsConfigAsync(FunctionTestData packageLimit = null) // null means no limit
        {
            var configList = await Task.WhenAll(
                    FileSystemHelpers
                    .GetDirectories(_environment.FunctionsPath)
                    .Select(d => Path.Combine(d, Constants.FunctionsConfigFile))
                    .Where(FileSystemHelpers.FileExists)
                    .Select(f => TryGetFunctionConfigAsync(Path.GetFileName(Path.GetDirectoryName(f)), packageLimit)));
            return configList.Where(c => c != null);
        }

        public async Task<FunctionEnvelope> GetFunctionConfigAsync(string name, FunctionTestData packageLimit = null) // null means no limit
        {
            var config = await TryGetFunctionConfigAsync(name, packageLimit);
            if (config == null)
            {
                throw new FileNotFoundException($"Function ({name}) config does not exist or is invalid");
            }
            return config;
        }

        private async Task<T> GetKeyObjectFromFile<T>(string name, IKeyJsonOps<T> keyOp)
        {
            string keyPath = GetFunctionSecretsFilePath(name);
            string key = null;
            if (!FileSystemHelpers.FileExists(keyPath))
            {
                FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(keyPath));
                try
                {
                    using (var fileStream = FileSystemHelpers.OpenFile(keyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    // will fail if file exists, prevent reading prematurely
                    // getting the lock early so no redundant work is being done
                    {
                        string jsonContent = keyOp.GenerateKeyJson(SecurityUtility.GenerateSecretStringsKeyPair(keyOp.NumberOfKeysInDefaultFormat), FunctionSiteExtensionVersion, out key);
                        using (var sw = new StringWriter())
                        using (var sr = new System.IO.StringReader(jsonContent))
                        {
                            new JsonTextWriter(sw) { Formatting = Formatting.Indented }.WriteToken(new JsonTextReader(sr));
                            // if lock acquire lock return false, I wait until write finishes and read keyPath
                            using (var streamWriter = new StreamWriter(fileStream))
                            {
                                await streamWriter.WriteAsync(sw.ToString());
                                await streamWriter.FlushAsync();
                            }
                        }
                    }
                    return keyOp.GenerateKeyObject(key, name);
                }
                catch (IOException)
                {
                    // don't throw exception if the file already existed
                    // fallback to read key files
                }
            }

            string jsonStr = null;
            int timeOut = 5;
            while (true)
            {
                try
                {
                    jsonStr = await FileSystemHelpers.ReadAllTextFromFileAsync(keyPath);
                    break;
                }
                catch (Exception)
                {
                    if (timeOut == 0)
                    {
                        throw new TimeoutException($"Fail to read {keyPath}, the file is being held by another process");
                    }
                    timeOut--;
                    await Task.Delay(250);
                }
            }

            bool isEncrypted;
            key = keyOp.GetKeyValueFromJson(jsonStr, out isEncrypted);
            if (isEncrypted)
            {
                key = SecurityUtility.DecryptSecretString(key);
            }
            return keyOp.GenerateKeyObject(key, name);

        }


        public async Task<MasterKey> GetMasterKeyAsync()
        {
            return await GetKeyObjectFromFile<MasterKey>("host", new MasterKeyJsonOps());
        }

        public async Task<FunctionSecrets> GetFunctionSecretsAsync(string functionName)
        {
            return await GetKeyObjectFromFile<FunctionSecrets>(functionName, new FunctionSecretsJsonOps());
        }

        public async Task<JObject> GetHostConfigAsync()
        {
            var host = await TryGetHostConfigAsync();
            if (host == null)
            {
                throw new FileNotFoundException("Host.json is invalid");
            }
            return host;
        }

        private async Task<JObject> TryGetHostConfigAsync()
        {
            try
            {
                return FileSystemHelpers.FileExists(HostJsonPath)
                    ? JObject.Parse(await FileSystemHelpers.ReadAllTextFromFileAsync(HostJsonPath))
                    : new JObject();
            }
            catch
            {
                // no-op
            }

            return null;
        }

        public async Task<JObject> PutHostConfigAsync(JObject content)
        {
            await FileSystemHelpers.WriteAllTextToFileAsync(HostJsonPath, JsonConvert.SerializeObject(content));
            return await GetHostConfigAsync();
        }

        public void DeleteFunction(string name, bool ignoreErrors)
        {
            FileSystemHelpers.DeleteDirectorySafe(GetFunctionPath(name), ignoreErrors);
            DeleteFunctionArtifacts(name);
        }

        private void DeleteFunctionArtifacts(string name)
        {
            FileSystemHelpers.DeleteFileSafe(GetFunctionTestDataFilePath(name));
            FileSystemHelpers.DeleteFileSafe(GetFunctionSecretsFilePath(name));
            FileSystemHelpers.DeleteFileSafe(GetFunctionLogPath(name));
        }

        private async Task<FunctionEnvelope> TryGetFunctionConfigAsync(string name, FunctionTestData packageLimit)
        {
            try
            {
                var path = GetFunctionConfigPath(name);
                if (FileSystemHelpers.FileExists(path))
                {
                    return await CreateFunctionConfig(await FileSystemHelpers.ReadAllTextFromFileAsync(path), name, packageLimit);
                }
            }
            catch
            {
                // no-op
            }
            return null;
        }

        private async Task<FunctionEnvelope> CreateFunctionConfig(string configContent, string functionName, FunctionTestData packageLimit)
        {
            var functionConfig = JObject.Parse(configContent);

            return new FunctionEnvelope
            {
                Name = functionName,
                ScriptRootPathHref = FilePathToVfsUri(GetFunctionPath(functionName), isDirectory: true),
                ScriptHref = FilePathToVfsUri(GetFunctionScriptPath(functionName, functionConfig)),
                ConfigHref = FilePathToVfsUri(GetFunctionConfigPath(functionName)),
                SecretsFileHref = FilePathToVfsUri(GetFunctionSecretsFilePath(functionName)),
                Href = GetFunctionHref(functionName),
                Config = functionConfig,
                TestData = await GetFunctionTestData(functionName, packageLimit)
            };
        }

        private string GetFunctionScriptPath(string functionName, JObject functionConfig)
        {
            var functionPath = GetFunctionPath(functionName);
            var functionFiles = FileSystemHelpers.GetFiles(functionPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(p => Path.GetFileName(p).ToLowerInvariant() != "function.json").ToArray();

            return DeterminePrimaryScriptFile(functionConfig, functionFiles);
        }

        // Logic for this function is copied from here:
        // https://github.com/Azure/azure-webjobs-sdk-script/blob/dev/src/WebJobs.Script/Host/ScriptHost.cs
        // These two implementations must stay in sync!
        internal static string DeterminePrimaryScriptFile(JObject functionConfig, string[] functionFiles)
        {
            if (functionFiles.Length == 1)
            {
                // if there is only a single file, that file is primary
                return functionFiles[0];
            }
            else
            {
                // First see if there is an explicit primary file indicated
                // in config. If so use that.
                string functionPrimary = null;
                string scriptFileName = (string)functionConfig["scriptFile"];
                if (!string.IsNullOrEmpty(scriptFileName))
                {
                    functionPrimary = functionFiles.FirstOrDefault(p =>
                        string.Compare(Path.GetFileName(p), scriptFileName, StringComparison.OrdinalIgnoreCase) == 0);
                }
                else
                {
                    // if there is a "run" file, that file is primary,
                    // for Node, any index.js file is primary
                    functionPrimary = functionFiles.FirstOrDefault(p =>
                        Path.GetFileNameWithoutExtension(p).ToLowerInvariant() == "run" ||
                        Path.GetFileName(p).ToLowerInvariant() == "index.js");
                }

                return functionPrimary;
            }
        }

        private Uri FilePathToVfsUri(string filePath, bool isDirectory = false)
        {
            filePath = filePath.Substring(_environment.RootPath.Length).Trim('\\').Replace("\\", "/");
            return new Uri($"{_environment.AppBaseUrlPrefix}/api/vfs/{filePath}{(isDirectory ? "/" : string.Empty)}");
        }

        private Uri GetFunctionHref(string functionName)
        {
            return new Uri($"{_environment.AppBaseUrlPrefix}/api/functions/{functionName}");
        }

        private string HostJsonPath
        {
            get
            {
                return Path.Combine(_environment.FunctionsPath, Constants.FunctionsHostConfigFile);
            }
        }

        private string GetFunctionPath(string name)
        {
            var path = Path.Combine(_environment.FunctionsPath, name);
            if (FileSystemHelpers.DirectoryExists(path))
            {
                return path;
            }

            throw new FileNotFoundException($"Function ({name}) does not exist");
        }

        private string GetFunctionConfigPath(string name)
        {
            return Path.Combine(GetFunctionPath(name), Constants.FunctionsConfigFile);
        }

        private string GetFunctionLogPath(string name)
        {
            return Path.Combine(_environment.ApplicationLogFilesPath, Constants.Functions, Constants.Function, name);
        }

        private async Task<string> GetFunctionTestData(string functionName, FunctionTestData packageLimit)
        {
            string testDataFilePath = GetFunctionTestDataFilePath(functionName);

            // Create an empty file if it doesn't exist
            if (!FileSystemHelpers.FileExists(testDataFilePath))
            {
                FileSystemHelpers.WriteAllText(testDataFilePath, String.Empty);
            }

            if (packageLimit != null)
            {
                var fileSize = FileSystemHelpers.FileInfoFromFileName(testDataFilePath).Length;
                if (!packageLimit.DeductFromBytesLeftInPackage(fileSize))
                {
                    return $"Test_Data is of size {fileSize} bytes, but there's only {packageLimit.BytesLeftInPackage} bytes left in ARM response";
                }
            }

            return await FileSystemHelpers.ReadAllTextFromFileAsync(testDataFilePath);

        }

        private string GetFunctionTestDataFilePath(string functionName)
        {
            string folder = Path.Combine(_environment.DataPath, Constants.Functions, Constants.SampleData);
            FileSystemHelpers.EnsureDirectory(folder);
            return Path.Combine(folder, $"{functionName}.dat");
        }

        private string GetFunctionSecretsFilePath(string functionName)
        {
            return Path.Combine(_environment.DataPath, Constants.Functions, Constants.Secrets, $"{functionName}.json");
        }
    }
}