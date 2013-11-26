﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Deployment;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests.Jobs
{
    public class ContinuousJobsTests
    {
        private const string VerificationFilePath = "LogFiles/verification.txt";
        private const string JobsBinPath = "Site/wwwroot/App_Data/jobs";
        private const string JobsDataPath = "data/jobs";
        private const string ExpectedVerificationFileContent = "Verified!!!";
        private const string ExpectedChangedFileContent = "Changed!!!";
        private const string JobScript = "echo " + ExpectedVerificationFileContent + " >> %WEBROOT_PATH%\\..\\..\\LogFiles\\verification.txt\n";

        private const string ContinuousJobsBinPath = JobsBinPath + "/continuous";
        private const string BasicContinuousJobExecutablePath = ContinuousJobsBinPath + "/basicJob1/run.cmd";
        private const string ConsoleWorkerExecutablePath = ContinuousJobsBinPath + "/deployedJob/ConsoleWorker.exe";

        private const string TriggeredJobBinPath = "Site/wwwroot/App_Data/jobs/triggered";

        [Fact]
        public void PushAndRedeployContinuousJobAsConsoleWorker()
        {
            RunScenario("PushAndRedeployContinuousJobAsConsoleWorker", appManager =>
            {
                using (TestRepository testRepository = Git.Clone("ConsoleWorker"))
                {
                    ///////// Part 1
                    TestTracer.Trace("I) Starting ConsoleWorker test, deploying the worker");

                    PushAndVerifyConsoleWorker(appManager, testRepository, new string[] { ExpectedVerificationFileContent });

                    ///////// Part 2
                    TestTracer.Trace("II) Make sure redeploy works for console worker");
                    testRepository.Replace("ConsoleWorker\\Program.cs", ExpectedVerificationFileContent, ExpectedChangedFileContent);
                    Git.Commit(testRepository.PhysicalPath, "Made a small change");

                    PushAndVerifyConsoleWorker(appManager, testRepository, new string[] { ExpectedVerificationFileContent, ExpectedChangedFileContent }, expectedDeployments: 2);
                }
            });
        }

        [Fact]
        public void DeleteConsoleWorkerExecutableStopsIt()
        {
            RunScenario("DeleteConsoleWorkerExecutableStopsIt", appManager =>
            {
                using (TestRepository testRepository = Git.Clone("ConsoleWorker"))
                {
                    ///////// Part 1
                    TestTracer.Trace("I) Starting ConsoleWorker test, deploying the worker");

                    PushAndVerifyConsoleWorker(appManager, testRepository, new string[] { ExpectedVerificationFileContent });

                    TestTracer.Trace("Make sure process is up");
                    var processes = appManager.ProcessManager.GetProcessesAsync().Result;
                    var workerProcess = processes.FirstOrDefault(p => String.Equals("ConsoleWorker", p.Name, StringComparison.OrdinalIgnoreCase));
                    Assert.NotNull(workerProcess);

                    ///////// Part 2
                    TestTracer.Trace("II) Verifying worker gone when executable file is removed");

                    appManager.VfsManager.Delete(ConsoleWorkerExecutablePath);

                    WaitUntilAssertVerified(
                        "no continuous jobs exist",
                        TimeSpan.FromSeconds(60),
                        () =>
                        {
                            var jobs = appManager.JobsManager.ListContinuousJobsAsync().Result;
                            Assert.Equal(0, jobs.Count());
                        });

                    WaitUntilAssertVerified(
                        "make sure process is down",
                        TimeSpan.FromSeconds(30),
                        () =>
                        {
                            var allProcesses = appManager.ProcessManager.GetProcessesAsync().Result;
                            var process = allProcesses.FirstOrDefault(p => String.Equals("ConsoleWorker", p.Name, StringComparison.OrdinalIgnoreCase));
                            Assert.Null(process);
                        });
                }
            });
        }

        [Fact]
        public void ContinuousJobStartsAfterGoingDown()
        {
            RunScenario("ContinuousJobStartsAfterGoingDown", appManager =>
            {
                TestTracer.Trace("Copying the script to the continuous job directory");

                WaitUntilAssertVerified(
                    "writing file",
                    TimeSpan.FromSeconds(10),
                    () => appManager.VfsManager.WriteAllText(BasicContinuousJobExecutablePath, JobScript));

                var expectedContinuousJob = new ContinuousJob()
                {
                    Name = "basicJob1",
                    JobType = "continuous",
                    Status = "PendingRestart",
                    RunCommand = "run.cmd"
                };

                WaitUntilAssertVerified(
                    "verify continuous job",
                    TimeSpan.FromSeconds(60),
                    () =>
                    {
                        ContinuousJob deployedJob = appManager.JobsManager.GetContinuousJobAsync("basicJob1").Result;
                        AssertContinuousJob(expectedContinuousJob, deployedJob);
                    });

                TestTracer.Trace("Waiting for verification file to have 2 lines (which means it ran twice)");

                WaitUntilAssertVerified(
                    "verification file",
                    TimeSpan.FromSeconds(30),
                    () =>
                    {
                        VerifyVerificationFile(appManager, new string[] { ExpectedVerificationFileContent, ExpectedVerificationFileContent });
                    });
            });
        }

        [Fact]
        public void ContinuousJobStopsWhenDisabledStartsWhenEnabled()
        {
            RunScenario("ContinuousJobStopsWhenDisabledStartsWhenEnabled", appManager =>
            {
                using (TestRepository testRepository = Git.Clone("ConsoleWorker"))
                {
                    TestTracer.Trace("Starting ConsoleWorker test, deploying the worker");

                    PushAndVerifyConsoleWorker(appManager, testRepository, new string[] { ExpectedVerificationFileContent });

                    TestTracer.Trace("Make sure process is up");
                    var processes = appManager.ProcessManager.GetProcessesAsync().Result;
                    var workerProcess = processes.FirstOrDefault(p => String.Equals("ConsoleWorker", p.Name, StringComparison.OrdinalIgnoreCase));
                    Assert.NotNull(workerProcess);

                    appManager.JobsManager.DisableContinuousJobAsync("deployedJob").Wait();

                    WaitUntilAssertVerified(
                        "continuous job disabled",
                        TimeSpan.FromSeconds(30),
                        () =>
                        {
                            var jobs = appManager.JobsManager.ListContinuousJobsAsync().Result;
                            Assert.Equal(1, jobs.Count());
                            Assert.Equal("Stopped", jobs.First().Status);
                        });

                    WaitUntilAssertVerified(
                        "make sure process is down",
                        TimeSpan.FromSeconds(30),
                        () =>
                        {
                            var allProcesses = appManager.ProcessManager.GetProcessesAsync().Result;
                            var process = allProcesses.FirstOrDefault(p => String.Equals("ConsoleWorker", p.Name, StringComparison.OrdinalIgnoreCase));
                            Assert.Null(process);
                        });

                    appManager.JobsManager.EnableContinuousJobAsync("deployedJob").Wait();

                    WaitUntilAssertVerified(
                        "continuous job enabled",
                        TimeSpan.FromSeconds(30),
                        () =>
                        {
                            var jobs = appManager.JobsManager.ListContinuousJobsAsync().Result;
                            Assert.Equal(1, jobs.Count());
                            Assert.Equal("Running", jobs.First().Status);
                        });

                    WaitUntilAssertVerified(
                        "make sure process is up",
                        TimeSpan.FromSeconds(30),
                        () =>
                        {
                            var allProcesses = appManager.ProcessManager.GetProcessesAsync().Result;
                            var process = allProcesses.FirstOrDefault(p => String.Equals("ConsoleWorker", p.Name, StringComparison.OrdinalIgnoreCase));
                            Assert.NotNull(process);
                        });
                }
            });
        }

        [Fact]
        public void TriggeredJobTriggers()
        {
            RunScenario("TriggeredJobTriggers", appManager =>
            {
                const string jobName = "job1";

                TestTracer.Trace("Copying the script to the triggered job directory");

                WaitUntilAssertVerified(
                    "writing file",
                    TimeSpan.FromSeconds(10),
                    () => appManager.VfsManager.WriteAllText(TriggeredJobBinPath + "/" + jobName + "/run.cmd", JobScript));

                var expectedTriggeredJob = new TriggeredJob()
                {
                    Name = jobName,
                    JobType = "triggered",
                    RunCommand = "run.cmd"
                };

                TestTracer.Trace("Verify triggered job exists");

                TriggeredJob triggeredJob = appManager.JobsManager.GetTriggeredJobAsync(jobName).Result;
                AssertTriggeredJob(expectedTriggeredJob, triggeredJob);

                TestTracer.Trace("Trigger the job");

                appManager.JobsManager.InvokeTriggeredJobAsync(jobName).Wait();

                WaitUntilAssertVerified(
                    "verify triggered job run",
                    TimeSpan.FromSeconds(10),
                    () =>
                    {
                        TriggeredJobHistory triggeredJobHistory = appManager.JobsManager.GetTriggeredJobHistoryAsync(jobName).Result;
                        Assert.NotNull(triggeredJobHistory);
                        Assert.Equal(1, triggeredJobHistory.TriggeredJobRuns.Count());

                        AssertTriggeredJobRun(triggeredJobHistory.TriggeredJobRuns.First(), "Success", "echo ");

                        VerifyVerificationFile(appManager, new string[] { ExpectedVerificationFileContent });
                    });

                TestTracer.Trace("Trigger the job again");

                appManager.JobsManager.InvokeTriggeredJobAsync(jobName).Wait();

                WaitUntilAssertVerified(
                    "verify triggered job run again",
                    TimeSpan.FromSeconds(10),
                    () =>
                    {
                        TriggeredJobHistory triggeredJobHistory = appManager.JobsManager.GetTriggeredJobHistoryAsync(jobName).Result;
                        Assert.NotNull(triggeredJobHistory);
                        Assert.Equal(2, triggeredJobHistory.TriggeredJobRuns.Count());

                        foreach (TriggeredJobRun triggeredJobRun in triggeredJobHistory.TriggeredJobRuns)
                        {
                            AssertTriggeredJobRun(triggeredJobRun, "Success", "echo ");
                        }

                        VerifyVerificationFile(appManager, new string[] { ExpectedVerificationFileContent, ExpectedVerificationFileContent });
                    });
            });
        }

        [Fact]
        public void JobsScriptsShouldBeChosenByOrder()
        {
            RunScenario("JobsScriptsShouldBeChosenByOrder", appManager =>
            {
                const string jobName = "job1";

                TestTracer.Trace("Adding all scripts");

                var scriptFileNames = new string[]
                {
                    "run.cmd",
                    "run.bat",
                    "run.exe",
                    "run.sh",
                    "run.php",
                    "run.py",
                    "run.js",
                    "go.cmd",
                    "do.bat",
                    "console.exe",
                    "invoke.sh",
                    "request.php",
                    "respond.py",
                    "execute.js"
                };

                foreach (string scriptFileName in scriptFileNames)
                {
                    appManager.VfsManager.WriteAllText(TriggeredJobBinPath + "/" + jobName + "/" + scriptFileName, JobScript);
                }

                foreach (string scriptFileName in scriptFileNames)
                {
                    TestTracer.Trace("Verify - " + scriptFileName);

                    var expectedTriggeredJob = new TriggeredJob()
                    {
                        Name = jobName,
                        JobType = "triggered",
                        RunCommand = scriptFileName
                    };

                    TriggeredJob triggeredJob = appManager.JobsManager.GetTriggeredJobAsync(jobName).Result;
                    AssertTriggeredJob(expectedTriggeredJob, triggeredJob);

                    appManager.VfsManager.Delete(TriggeredJobBinPath + "/" + jobName + "/" + scriptFileName);
                }
            });
        }

        private void VerifyVerificationFile(ApplicationManager appManager, string[] expectedContentLines)
        {
            string verificationFileContent = appManager.VfsManager.ReadAllText(VerificationFilePath).TrimEnd();
            string[] lines = verificationFileContent.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(expectedContentLines.Length, lines.Length);
            for (int i = 0; i < expectedContentLines.Length; i++)
            {
                Assert.Equal(expectedContentLines[i], lines[i].Trim());
            }
        }

        private void AssertTriggeredJobRun(TriggeredJobRun actualTriggeredJobRun, string expectedStatus, string expectedOutput = null, string expectedError = null)
        {
            Assert.NotNull(actualTriggeredJobRun);
            Assert.Equal(expectedStatus, actualTriggeredJobRun.Status);
            Assert.NotNull(actualTriggeredJobRun.Duration);
            Assert.NotNull(actualTriggeredJobRun.EndTime);
            Assert.NotNull(actualTriggeredJobRun.Id);
            Assert.NotNull(actualTriggeredJobRun.StartTime);
            Assert.NotNull(actualTriggeredJobRun.Url);

            AssertUrlContentAsync(actualTriggeredJobRun.OutputUrl, expectedOutput).Wait();
            AssertUrlContentAsync(actualTriggeredJobRun.ErrorUrl, expectedError).Wait();
        }

        private async Task AssertUrlContentAsync(Uri address, string expectedContent)
        {
            if (expectedContent == null)
            {
                Assert.Null(address);
                return;
            }

            using (var httpClient = new HttpClient())
            {
                using (var response = await httpClient.GetAsync(address))
                {
                    var content = await response.Content.ReadAsStringAsync();
                    TestTracer.Trace("Request to: {0}\nStatus code: {1}\nContent: {2}", address, response.StatusCode, content);

                    Assert.True(content.Contains(expectedContent), "Expected content: " + expectedContent);
                }
            }
        }

        private void RunScenario(string name, Action<ApplicationManager> action)
        {
            ApplicationManager.Run(name, appManager =>
            {
                appManager.SettingsManager.SetValue(SettingsKeys.JobsInterval, "5").Wait();

                // Make sure verification file and jobs doesn't exist
                WaitUntilAssertVerified(
                    "clean site for jobs",
                    TimeSpan.FromSeconds(60),
                    () =>
                    {
                        appManager.VfsManager.Delete(JobsBinPath + "?recursive=true");
                        appManager.VfsManager.Delete(JobsDataPath + "?recursive=true");
                        appManager.VfsManager.Delete(VerificationFilePath);
                    });

                action(appManager);
            });
        }

        private void PushAndVerifyConsoleWorker(ApplicationManager appManager, TestRepository testRepository, string[] expectedVerificationFileLines, int expectedDeployments = 1)
        {
            appManager.GitDeploy(testRepository.PhysicalPath);
            var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

            Assert.Equal(expectedDeployments, results.Count);
            for (int i = 0; i < expectedDeployments; i++)
            {
                Assert.Equal(DeployStatus.Success, results[i].Status);
            }

            var expectedContinuousJob = new ContinuousJob()
            {
                Name = "deployedJob",
                JobType = "continuous",
                Status = "Running",
                RunCommand = "ConsoleWorker.exe"
            };

            WaitUntilAssertVerified(
                "verify continuous job",
                TimeSpan.FromSeconds(60),
                () =>
                {
                    ContinuousJob deployedJob = appManager.JobsManager.GetContinuousJobAsync("deployedJob").Result;
                    AssertContinuousJob(expectedContinuousJob, deployedJob);
                });

            WaitUntilAssertVerified(
                "verification file",
                TimeSpan.FromSeconds(30),
                () =>
                {
                    VerifyVerificationFile(appManager, expectedVerificationFileLines);
                });
        }

        private void WaitUntilAssertVerified(string description, TimeSpan maxWaitTime, Action assertAction)
        {
            TestTracer.Trace("Waiting for " + description);

            Stopwatch waitTime = Stopwatch.StartNew();

            while (waitTime.Elapsed < maxWaitTime)
            {
                try
                {
                    assertAction();
                    return;
                }
                catch
                {
                }

                Thread.Sleep(1000);
            }

            assertAction();
        }

        private void AssertJob(JobBase expectedJob, JobBase actualJob)
        {
            Assert.NotNull(actualJob);
            Assert.Equal(expectedJob.Name, actualJob.Name);
            Assert.Equal(expectedJob.RunCommand, actualJob.RunCommand);
            Assert.Equal(expectedJob.JobType, actualJob.JobType);
            Assert.NotNull(actualJob.Url);
            Assert.NotNull(actualJob.ExtraInfoUrl);
        }

        private void AssertContinuousJob(ContinuousJob expectedContinuousJob, ContinuousJob actualContinuousJob)
        {
            AssertJob(expectedContinuousJob, actualContinuousJob);
            Assert.NotNull(actualContinuousJob);
            Assert.Equal(expectedContinuousJob.Status, actualContinuousJob.Status);
            Assert.NotNull(actualContinuousJob.LogUrl);
        }

        private void AssertTriggeredJob(TriggeredJob expectedTriggeredJob, TriggeredJob actualTriggeredJob)
        {
            AssertJob(expectedTriggeredJob, actualTriggeredJob);
            Assert.NotNull(actualTriggeredJob);
            Assert.NotNull(actualTriggeredJob.HistoryUrl);
        }
    }
}