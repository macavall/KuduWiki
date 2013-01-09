﻿using System;
using System.IO;
using System.IO.Abstractions;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    public class NodeSiteEnabler
    {
        private static readonly string[] NodeStartFiles = new[] { "server.js", "app.js" };

        public static bool LooksLikeNode(IFileSystem fileSystem, string siteFolder)
        {
            // Check if any of the known start pages exist
            foreach (var nodeDetectionFile in NodeStartFiles)
            {
                string fullPath = Path.Combine(siteFolder, nodeDetectionFile);
                if (fileSystem.File.Exists(fullPath))
                {
                    return true;
                }
            }

            return false;
        }

        public static void SelectNodeVersion(IFileSystem fileSystem, string scriptPath, string sourcePath, string destinationPath, IDeploymentSettingsManager settings, ITracer tracer, ILogger logger)
        {
            // The node.js version selection logic is implemented in selectNodeVersion.js. 

            // run with default node.js version which is on the path
            Executable executor = new Executable("node.exe", String.Empty, settings.GetCommandIdleTimeout());
            try
            {
                string log = executor.ExecuteWithConsoleOutput(
                    tracer,
                    "\"{0}\\selectNodeVersion.js\" \"{1}\" \"{2}\"",
                    scriptPath,
                    sourcePath,
                    destinationPath).Item1;

                logger.Log(log);
            }
            catch (Exception e)
            {
                var exception = new InvalidOperationException(Resources.Error_UnableToSelectNodeVersion, e);
                logger.Log(exception);
                throw exception;
            }
        }
    }
}
