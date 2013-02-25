﻿using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Deployment
{
    public abstract class SolutionBasedSiteBuilder : MsBuildSiteBuilder
    {
        public string SolutionDir
        {
            get
            {
                return Path.GetDirectoryName(SolutionPath);
            }
        }

        public string SolutionPath { get; private set; }

        protected SolutionBasedSiteBuilder(IBuildPropertyProvider propertyProvider, string repositoryPath, string solutionPath, IDeploymentSettingsManager settings)
            : base(settings, propertyProvider, repositoryPath)
        {
            SolutionPath = solutionPath;
        }
        
        public override Task Build(DeploymentContext context)
        {
            ILogger buildLogger = context.Logger.Log(Resources.Log_BuildingSolution, Path.GetFileName(SolutionPath));

            try
            {
                string propertyString = GetPropertyString();

                if (!String.IsNullOrEmpty(propertyString))
                {
                    propertyString = " /p:" + propertyString;
                }

                string extraArguments = GetMSBuildExtraArguments();

                using (context.Tracer.Step("Running msbuild on solution"))
                {
                    // Build the solution first
                    string log = ExecuteMSBuild(context.Tracer, @"""{0}"" /verbosity:m /nologo{1} {2}", SolutionPath, propertyString, extraArguments);
                    buildLogger.Log(log);
                }

                return BuildProject(context);
            }
            catch (Exception ex)
            {
                context.Tracer.TraceError(ex);

                // HACK: Log an empty error to the global logger (post receive hook console output).
                // The reason we don't log the real exception is because the 'live output' running
                // msbuild has already been captured.
                context.GlobalLogger.Log(String.Empty, LogEntryType.Error);

                buildLogger.Log(ex);

                var tcs = new TaskCompletionSource<object>();
                tcs.SetException(ex);

                return tcs.Task;
            }
        }

        protected abstract Task BuildProject(DeploymentContext context);
    }
}
