﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;

namespace Kudu.Core.Deployment.Generator
{
    class AspNet5Builder : GeneratorSiteBuilder
    {
        private IFileFinder _fileFinder;
        private readonly string _projectPath;
        private readonly string _sourcePath;
        private readonly bool _isConsoleApp;

        public AspNet5Builder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, IFileFinder fileFinder, string sourcePath, string projectPath, bool isConsoleApp)
            : base(environment, settings, propertyProvider, sourcePath)
        {
            _fileFinder = fileFinder;
            _projectPath = projectPath;
            _sourcePath = sourcePath;
            _isConsoleApp = isConsoleApp;
        }

        protected override string ScriptGeneratorCommandArguments
        {
            get
            {
                var commandArguments = new StringBuilder();
                if (_isConsoleApp)
                {
                    commandArguments.AppendFormat("--dnxConsoleApp \"{0}\"", FileSystemHelpers.GetDirectoryName(_projectPath));
                }
                else
                {
                    var aspNetSdk = AspNet5Helper.GetAspNet5Sdk(_sourcePath, _fileFinder);
                    commandArguments.AppendFormat("--aspNet5 \"{0}\" --aspNet5Version \"{1}\" --aspNet5Runtime \"{2}\" --aspNet5Architecture \"{3}\"",
                        _projectPath,
                        aspNetSdk.Version,
                        aspNetSdk.Runtime,
                        aspNetSdk.Architecture);
                }
                return commandArguments.ToString();
            }
        }

        public override string ProjectType
        {
            get { return "ASP.NET 5"; }
        }
    }
}
