﻿using Kudu.Core;
using Kudu.TestHarness;
using System;
using System.Collections.Generic;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class CommandExecutorTests
    {
        [Fact]
        public void CommandExecutorEnvironmentSetCorrectly()
        {
            // Arrange
            string appName = "CommandExecuterEnvironmentSetCorrectly";
            ApplicationManager.Run(appName, appManager =>
            {
                List<CommandTestSettings> tests = new List<CommandTestSettings>();

                var commandTestSettings = new CommandTestSettings("set MSBUILD_PATH");
                commandTestSettings.ExpectedResult.Output = "msbuild";
                tests.Add(commandTestSettings);

                commandTestSettings = new CommandTestSettings("set NONEXISTING");
                commandTestSettings.ExpectedResult.Error = "NONEXISTING";
                commandTestSettings.ExpectedResult.ExitCode = 1;
                tests.Add(commandTestSettings);

                commandTestSettings = new CommandTestSettings("echo %DEPLOYMENT_SOURCE%");
                commandTestSettings.ExpectedResult.Output = "\\";
                tests.Add(commandTestSettings);

                commandTestSettings = new CommandTestSettings("echo %DEPLOYMENT_TARGET%");
                commandTestSettings.ExpectedResult.Output = "\\wwwroot";
                tests.Add(commandTestSettings);

                commandTestSettings = new CommandTestSettings("echo %KUDU_SYNC_CMD%");
                commandTestSettings.ExpectedResult.Output = "\\kudusync";
                tests.Add(commandTestSettings);

                commandTestSettings = new CommandTestSettings("echo %KUDU_SELECT_NODE_VERSION_CMD%");
                commandTestSettings.ExpectedResult.Output = "\\selectNodeVersion";
                tests.Add(commandTestSettings);

                commandTestSettings = new CommandTestSettings("echo %NPM_JS_PATH%");
                commandTestSettings.ExpectedResult.Output = "\\npm";
                tests.Add(commandTestSettings);

                commandTestSettings = new CommandTestSettings("echo %PATH%");
                commandTestSettings.ExpectedResult.Output = "git";
                tests.Add(commandTestSettings);

                commandTestSettings = new CommandTestSettings("dir");
                commandTestSettings.WorkingDirectory = ".\\site";
                commandTestSettings.ExpectedResult.Output = "wwwroot";
                tests.Add(commandTestSettings);

                commandTestSettings = new CommandTestSettings("echo %EnableNuGetPackageRestore%");
                commandTestSettings.ExpectedResult.Output = "true";
                tests.Add(commandTestSettings);

                foreach (CommandTestSettings test in tests)
                {
                    VerifyCommand(test, appManager);
                }
            });
        }

        private void VerifyCommand(CommandTestSettings commandTestSettings, ApplicationManager appManager)
        {
            TestTracer.Trace("Running command - '{0}' on '{1}'", commandTestSettings.Command, commandTestSettings.WorkingDirectory);
            CommandResult commandResult = appManager.CommandExecutor.ExecuteCommand(commandTestSettings.Command, commandTestSettings.WorkingDirectory).Result;

            TestTracer.Trace("Received result\nOutput\n======\n{0}\nError\n======\n{1}\nExit Code - {2}", commandResult.Output, commandResult.Error, commandResult.ExitCode);

            Assert.Equal(commandTestSettings.ExpectedResult.ExitCode, commandResult.ExitCode);
            AssertOutput(commandTestSettings.ExpectedResult.Error, commandResult.Error);
            AssertOutput(commandTestSettings.ExpectedResult.Output, commandResult.Output);
        }

        private void AssertOutput(string expected, string actual)
        {
            if (!String.IsNullOrEmpty(expected))
            {
                Assert.Contains(expected, actual, StringComparison.InvariantCultureIgnoreCase);
            }
            else
            {
                Assert.Equal(expected, actual);
            }
        }

        class CommandTestSettings
        {
            public CommandResult ExpectedResult { get; set; }
            public string WorkingDirectory { get; set; }
            public string Command { get; set; }

            public CommandTestSettings(string command)
            {
                ExpectedResult = new CommandResult()
                {
                    Output = String.Empty,
                    Error = String.Empty,
                    ExitCode = 0
                };

                WorkingDirectory = ".";

                Command = command;
            }
        }
    }
}
