﻿using System;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Deployment
{
    public class WebSiteBuilder : SolutionBasedSiteBuilder
    {
        private readonly string _projectPath;

        public WebSiteBuilder(IBuildPropertyProvider propertyProvider, string sourcePath, string projectPath, string tempPath, string solutionPath, IDeploymentSettingsManager settings)
            : base(propertyProvider, sourcePath, tempPath, solutionPath, settings)
        {
            _projectPath = projectPath;
        }

        protected override Task BuildProject(DeploymentContext context)
        {
            var tcs = new TaskCompletionSource<object>();
            ILogger copyLogger = context.Logger.Log(Resources.Log_PreparingFiles);

            try
            {
                using (context.Tracer.Step("Copying files to output directory"))
                {
                    // Copy to the output path
                    DeploymentHelper.CopyWithManifest(_projectPath, context.OutputPath, context.PreviousManifest);
                }

                using (context.Tracer.Step("Building manifest"))
                {
                    // Generate the manifest from the project path
                    context.ManifestWriter.AddFiles(_projectPath);
                }

                // Log the copied files from the manifest
                copyLogger.LogFileList(context.ManifestWriter.GetPaths());

                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                context.Tracer.TraceError(ex);

                context.GlobalLogger.Log(ex);

                copyLogger.Log(ex);

                tcs.SetException(ex);
            }

            return tcs.Task;
        }
    }
}
