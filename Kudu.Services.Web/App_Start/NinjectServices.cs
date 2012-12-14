using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http.Formatting;
using System.Web;
using System.Web.Http;
using System.Web.Routing;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Commands;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.SSHKey;
using Kudu.Core.Tracing;
using Kudu.Services.GitServer;
using Kudu.Services.GitServer.ServiceHookHandlers;
using Kudu.Services.Infrastructure;
using Kudu.Services.Performance;
using Kudu.Services.SSHKey;
using Kudu.Services.Web.Infrastruture;
using Kudu.Services.Web.Services;
using Kudu.Services.Web.Tracing;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Ninject;
using Ninject.Activation;
using XmlSettings;

[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Stop")]

namespace Kudu.Services.Web.App_Start
{
    public static class NinjectServices
    {
        /// <summary>
        /// Root directory that contains the VS target files
        /// </summary>
        private const string SdkRootDirectory = "msbuild";

        /// <summary>
        /// Starts the application
        /// </summary>
        public static void Start()
        {
            DynamicModuleUtility.RegisterModule(typeof(OnePerRequestModule));
            CreateKernel();
        }

        /// <summary>
        /// Stops the application.
        /// </summary>
        public static void Stop()
        {
        }

        /// <summary>
        /// Creates the kernel that will manage your application.
        /// </summary>
        /// <returns>The created kernel.</returns>
        private static IKernel CreateKernel()
        {
            var kernel = new StandardKernel();

            RegisterServices(kernel);

            return kernel;
        }

        /// <summary>
        /// Load your modules or register your services here!
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        private static void RegisterServices(IKernel kernel)
        {
            var serverConfiguration = new ServerConfiguration();
            var gitConfiguration = new RepositoryConfiguration
            {
                Username = AppSettings.GitUsername,
                Email = AppSettings.GitEmail
            };

            IEnvironment environment = GetEnvironment();

            // General
            kernel.Bind<HttpContextBase>().ToMethod(context => new HttpContextWrapper(HttpContext.Current));
            kernel.Bind<IEnvironment>().ToConstant(environment);
            kernel.Bind<IServerConfiguration>().ToConstant(serverConfiguration);
            kernel.Bind<IFileSystem>().To<FileSystem>().InSingletonScope();
            kernel.Bind<RepositoryConfiguration>().ToConstant(gitConfiguration);

            string sdkPath = Path.Combine(HttpRuntime.AppDomainAppPath, SdkRootDirectory);
            kernel.Bind<IBuildPropertyProvider>().ToConstant(new BuildPropertyProvider());

            System.Func<ITracer> createTracerThunk = () => GetTracer(environment, kernel);
            System.Func<ILogger> createLoggerThunk = () => GetLogger(environment, kernel);

            // First try to use the current request profiler if any, otherwise create a new one
            var traceFactory = new TracerFactory(() => TraceServices.CurrentRequestTracer ?? createTracerThunk());

            kernel.Bind<ITracer>().ToMethod(context => TraceServices.CurrentRequestTracer ?? NullTracer.Instance);
            kernel.Bind<ITraceFactory>().ToConstant(traceFactory);
            TraceServices.SetTraceFactory(createTracerThunk, createLoggerThunk);

            // Setup the deployment lock
            string lockPath = Path.Combine(environment.SiteRootPath, Constants.LockPath);
            string deploymentLockPath = Path.Combine(lockPath, Constants.DeploymentLockFile);
            string sshKeyLockPath = Path.Combine(lockPath, Constants.SSHKeyLockFile);
            string initLockPath = Path.Combine(lockPath, Constants.InitLockFile);

            var deploymentLock = new LockFile(kernel.Get<ITraceFactory>(), deploymentLockPath);
            var initLock = new LockFile(kernel.Get<ITraceFactory>(), initLockPath);
            var sshKeyLock = new LockFile(kernel.Get<ITraceFactory>(), sshKeyLockPath);

            kernel.Bind<IOperationLock>().ToConstant(sshKeyLock).WhenInjectedInto<SSHKeyController>();
            kernel.Bind<IOperationLock>().ToConstant(deploymentLock);

            var shutdownDetector = new ShutdownDetector();
            shutdownDetector.Initialize();

            // LogStream service
            kernel.Bind<LogStreamManager>().ToMethod(context => new LogStreamManager(Path.Combine(environment.RootPath, Constants.LogFilesPath),
                                                                                     context.Kernel.Get<ITracer>(),
                                                                                     shutdownDetector));

            // Deployment Service
            kernel.Bind<ISettings>().ToMethod(context => new XmlSettings.Settings(GetSettingsPath(environment)))
                                             .InRequestScope();
            kernel.Bind<IDeploymentSettingsManager>().To<DeploymentSettingsManager>()
                                             .InRequestScope();

            kernel.Bind<ISiteBuilderFactory>().To<SiteBuilderFactoryDispatcher>()
                                             .InRequestScope();

            kernel.Bind<IServerRepository>().ToMethod(context => new GitExeServer(environment.RepositoryPath,
                                                                                  environment.SiteRootPath,
                                                                                  initLock,
                                                                                  GetRequestTraceFile(environment, context.Kernel),
                                                                                  context.Kernel.Get<IDeploymentEnvironment>(),
                                                                                  context.Kernel.Get<ITraceFactory>()))
                                            .InRequestScope();

            kernel.Bind<ILogger>().ToMethod(context => GetLogger(environment, context.Kernel))
                                             .InRequestScope();

            kernel.Bind<IRepository>().ToMethod(context => new GitExeRepository(environment.RepositoryPath, environment.SiteRootPath, context.Kernel.Get<ITraceFactory>()))
                                                .InRequestScope();

            kernel.Bind<IDeploymentManager>().To<DeploymentManager>()
                                             .InRequestScope();
            kernel.Bind<ISSHKeyManager>().To<SSHKeyManager>()
                                             .InRequestScope();

            kernel.Bind<IDeploymentRepository>().ToMethod(context => new GitDeploymentRepository(environment.RepositoryPath,
                                                                                                 environment.SiteRootPath,
                                                                                                 context.Kernel.Get<ITraceFactory>()))
                                                .InRequestScope();

            // Git server
            kernel.Bind<IDeploymentEnvironment>().To<DeploymentEnvrionment>();

            kernel.Bind<IGitServer>().ToMethod(context => new GitExeServer(environment.RepositoryPath,
                                                                           environment.SiteRootPath,
                                                                           initLock,
                                                                           GetRequestTraceFile(environment, context.Kernel),
                                                                           context.Kernel.Get<IDeploymentEnvironment>(),
                                                                           context.Kernel.Get<ITraceFactory>()))
                                     .InRequestScope();

            // Git Servicehook parsers
            kernel.Bind<IServiceHookHandler>().To<GitHubHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<BitbucketHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<DropboxHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<CodePlexHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<CodebaseHqHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<GitlabHqHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<GitHubCompatHandler>().InRequestScope();

            // Command executor
            kernel.Bind<ICommandExecutor>().ToMethod(context => GetCommandExecutor(environment, context))
                                           .InRequestScope();

            RegisterRoutes(kernel, RouteTable.Routes);
        }

        public static void RegisterRoutes(IKernel kernel, RouteCollection routes)
        {
            var configuration = kernel.Get<IServerConfiguration>();
            GlobalConfiguration.Configuration.Formatters.Clear();
            GlobalConfiguration.Configuration.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.LocalOnly;
            var jsonFormatter = new JsonMediaTypeFormatter();
            GlobalConfiguration.Configuration.Formatters.Add(jsonFormatter);
            GlobalConfiguration.Configuration.DependencyResolver = new NinjectWebApiDependencyResolver(kernel);

            // the scenario is to have kudu service running but w/o git functionalities.
            // this is utilized by windows azures where we try to avoid deployment collision - if git disabled.
            // we intentionally on block git related operation - not other repository-related such as /deployment
            if (!AppSettings.DisableGit)
            {
                // Git Service
                routes.MapHttpRoute("git-info-refs", configuration.GitServerRoot + "/info/refs", new { controller = "InfoRefs", action = "Execute" });

                // Push url
                routes.MapHandler<ReceivePackHandler>(kernel, "git-receive-pack", configuration.GitServerRoot + "/git-receive-pack");

                // Fetch Hook
                routes.MapHandler<FetchHandler>(kernel, "fetch", "deploy");

                // Clone url
                routes.MapHandler<UploadPackHandler>(kernel, "git-upload-pack", configuration.GitServerRoot + "/git-upload-pack");
            }

            // Scm (deployment repository)
            routes.MapHttpRoute("scm-info", "scm/info", new { controller = "LiveScm", action = "GetRepositoryInfo" });
            routes.MapHttpRoute("scm-clean", "scm/clean", new { controller = "LiveScm", action = "Clean" });
            routes.MapHttpRoute("scm-delete", "scm", new { controller = "LiveScm", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Scm files editor
            routes.MapHttpRoute("scm-get-files", "scmvfs/{*path}", new { controller = "LiveScmEditor", action = "GetItem" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });
            routes.MapHttpRoute("scm-put-files", "scmvfs/{*path}", new { controller = "LiveScmEditor", action = "PutItem" }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpRoute("scm-delete-files", "scmvfs/{*path}", new { controller = "LiveScmEditor", action = "DeleteItem" }, new { verb = new HttpMethodConstraint("DELETE") });

            // These older scm routes are there for backward compat, and should eventually be deleted once clients are changed.
            routes.MapHttpRoute("live-scm-info", "live/scm/info", new { controller = "LiveScm", action = "GetRepositoryInfo" });
            routes.MapHttpRoute("live-scm-clean", "live/scm/clean", new { controller = "LiveScm", action = "Clean" });
            routes.MapHttpRoute("live-scm-delete", "live/scm", new { controller = "LiveScm", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Live files editor
            routes.MapHttpRoute("vfs-get-files", "vfs/{*path}", new { controller = "Vfs", action = "GetItem" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });
            routes.MapHttpRoute("vfs-put-files", "vfs/{*path}", new { controller = "Vfs", action = "PutItem" }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpRoute("vfs-delete-files", "vfs/{*path}", new { controller = "Vfs", action = "DeleteItem" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Live Command Line
            routes.MapHttpRoute("execute-command", "command", new { controller = "Command", action = "ExecuteCommand" }, new { verb = new HttpMethodConstraint("POST") });

            // Deployments
            routes.MapHttpRoute("all-deployments", "deployments", new { controller = "Deployment", action = "GetDeployResults" });
            routes.MapHttpRoute("one-deployment-get", "deployments/{id}", new { controller = "Deployment", action = "GetResult" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("one-deployment-put", "deployments/{id}", new { controller = "Deployment", action = "Deploy" }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpRoute("one-deployment-delete", "deployments/{id}", new { controller = "Deployment", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });
            routes.MapHttpRoute("one-deployment-log", "deployments/{id}/log", new { controller = "Deployment", action = "GetLogEntry" });
            routes.MapHttpRoute("one-deployment-log-details", "deployments/{id}/log/{logId}", new { controller = "Deployment", action = "GetLogEntryDetails" });

            // SSHKey
            routes.MapHttpRoute("get-sshkey", "sshkey", new { controller = "SSHKey", action = "GetPublicKey" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("put-sshkey", "sshkey", new { controller = "SSHKey", action = "SetPrivateKey" }, new { verb = new HttpMethodConstraint("PUT") });

            // Environment
            routes.MapHttpRoute("get-env", "environment", new { controller = "Environment", action = "Get" }, new { verb = new HttpMethodConstraint("GET") });

            // Settings
            routes.MapHttpRoute("set-setting", "settings", new { controller = "Settings", action = "Set" }, new { verb = new HttpMethodConstraint("POST") });
            routes.MapHttpRoute("get-all-settings", "settings", new { controller = "Settings", action = "GetAll" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("get-setting", "settings/{key}", new { controller = "Settings", action = "Get" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("delete-setting", "settings/{key}", new { controller = "Settings", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Diagnostics
            routes.MapHttpRoute("diagnostics", "dump", new { controller = "Diagnostics", action = "GetLog" });
            routes.MapHttpRoute("diagnostics-set-setting", "diagnostics/settings", new { controller = "Diagnostics", action = "Set" }, new { verb = new HttpMethodConstraint("POST") });
            routes.MapHttpRoute("diagnostics-get-all-settings", "diagnostics/settings", new { controller = "Diagnostics", action = "GetAll" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("diagnostics-get-setting", "diagnostics/settings/{key}", new { controller = "Diagnostics", action = "Get" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("diagnostics-delete-setting", "diagnostics/settings/{key}", new { controller = "Diagnostics", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // LogStream
            routes.MapHandler<LogStreamHandler>(kernel, "logstream", "logstream/{*path}");
        }

        private static ITracer GetTracer(IEnvironment environment, IKernel kernel)
        {
            TraceLevel level = kernel.Get<IDeploymentSettingsManager>().GetTraceLevel();
            if (level > TraceLevel.Off)
            {
                string tracePath = Path.Combine(environment.TracePath, Constants.TraceFile);
                string textPath = Path.Combine(environment.TracePath, TraceServices.CurrentRequestTraceFile);
                return new CascadeTracer(new Tracer(tracePath, level), new TextTracer(textPath, level));
            }

            return NullTracer.Instance;
        }

        private static ILogger GetLogger(IEnvironment environment, IKernel kernel)
        {
            TraceLevel level = kernel.Get<IDeploymentSettingsManager>().GetTraceLevel();
            if (level > TraceLevel.Off)
            {
                string textPath = Path.Combine(environment.DeploymentTracePath, TraceServices.CurrentRequestTraceFile);
                return new TextLogger(textPath);
            }

            return NullLogger.Instance;
        }

        private static string GetRequestTraceFile(IEnvironment environment, IKernel kernel)
        {
            TraceLevel level = kernel.Get<IDeploymentSettingsManager>().GetTraceLevel();
            if (level > TraceLevel.Off)
            {
                return TraceServices.CurrentRequestTraceFile;
            }

            return null;
        }

        private static ICommandExecutor GetCommandExecutor(IEnvironment environment, IContext context)
        {
            if (System.String.IsNullOrEmpty(environment.RepositoryPath))
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            return new CommandExecutor(environment.RootPath);
        }

        private static string GetSettingsPath(IEnvironment environment)
        {
            return Path.Combine(environment.DeploymentCachePath, Constants.DeploySettingsPath);
        }

        private static IEnvironment GetEnvironment()
        {
            string siteRoot = PathResolver.ResolveSiteRootPath();
            string root = Path.GetFullPath(Path.Combine(siteRoot, ".."));
            string webRootPath = Path.Combine(siteRoot, Constants.WebRoot);
            string deployCachePath = Path.Combine(siteRoot, Constants.DeploymentCachePath);
            string diagnosticsPath = Path.Combine(siteRoot, Constants.DiagnosticsPath);
            string sshKeyPath = Path.Combine(siteRoot, Constants.SSHKeyPath);
            string repositoryPath = Path.Combine(siteRoot, Constants.RepositoryPath);
            string tempPath = Path.GetTempPath();
            string deploymentTempPath = Path.Combine(tempPath, Constants.RepositoryPath);
            string scriptPath = Path.Combine(HttpRuntime.BinDirectory, Constants.ScriptsPath);
            string nodeModulesPath = Path.Combine(HttpRuntime.BinDirectory, Constants.NodeModulesPath);

            return new Kudu.Core.Environment(
                                   new FileSystem(),
                                   root,
                                   siteRoot,
                                   tempPath,
                                   repositoryPath,
                                   webRootPath,
                                   deployCachePath,
                                   diagnosticsPath,
                                   sshKeyPath,
                                   scriptPath,
                                   nodeModulesPath);
        }

        private class DeploymentManagerFactory : IDeploymentManagerFactory
        {
            private readonly System.Func<IDeploymentManager> _factory;
            public DeploymentManagerFactory(System.Func<IDeploymentManager> factory)
            {
                _factory = factory;
            }

            public IDeploymentManager CreateDeploymentManager()
            {
                return _factory();
            }
        }
    }
}
