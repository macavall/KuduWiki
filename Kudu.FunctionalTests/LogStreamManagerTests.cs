﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class LogStreamManagerTests
    {
        [Fact]
        public void TestLogStreamBasic()
        {
            string repoName = "LogTester";
            string repoCloneUrl = "https://github.com/KuduApps/LogTester.git";
            string appName = KuduUtils.GetRandomWebsiteName("TestLogStreamBasic");

            ApplicationManager.Run(appName, appManager =>
            {
                // Act
                using (var localRepo = Git.Clone(repoName, repoCloneUrl))
                {
                    appManager.GitDeploy(localRepo.PhysicalPath);
                }

                CreateLogDirectory(appManager.SiteUrl, @"LogFiles");

                using (var waitHandle = new LogStreamWaitHandle(appManager.LogStreamManager.GetStream().Result))
                {
                    string line = waitHandle.WaitNextLine(10000);
                    Assert.True(!String.IsNullOrEmpty(line) && line.Contains("Welcome"), "check welcome message: " + line);

                    string content = Guid.NewGuid().ToString();
                    WriteLogText(appManager.SiteUrl, @"LogFiles\temp.txt", content);
                    line = waitHandle.WaitNextLine(10000);
                    Assert.Equal(content, line);

                    content = Guid.NewGuid().ToString();
                    WriteLogText(appManager.SiteUrl, @"LogFiles\temp.log", content);
                    line = waitHandle.WaitNextLine(10000);
                    Assert.Equal(content, line);

                    // write to xml file, we should not get any live stream
                    content = Guid.NewGuid().ToString();
                    WriteLogText(appManager.SiteUrl, @"LogFiles\temp.xml", content);
                    line = waitHandle.WaitNextLine(1000);
                    Assert.Null(line);
                }
            });
        }

        [Fact]
        public void TestLogStreamSubFolder()
        {
            string appName = KuduUtils.GetRandomWebsiteName("TestLogStreamFilter");
            string repoName = "LogTester";
            string repoCloneUrl = "https://github.com/KuduApps/LogTester.git";

            ApplicationManager.Run(appName, appManager =>
            {
                // Act
                using (var localRepo = Git.Clone(repoName, repoCloneUrl))
                {
                    appManager.GitDeploy(localRepo.PhysicalPath);
                }
                List<string> logFiles = new List<string>();
                List<LogStreamWaitHandle> waitHandles = new List<LogStreamWaitHandle>();
                for (int i = 0; i < 2; ++i)
                {
                    logFiles.Add(@"LogFiles\Folder" + i + "\\temp.txt");
                    //Create the directory
                    CreateLogDirectory(appManager.SiteUrl, @"LogFiles\Folder" + i);
                    RemoteLogStreamManager mgr = appManager.CreateLogStreamManager("folder" + i);
                    var waitHandle = new LogStreamWaitHandle(mgr.GetStream().Result);
                    string line = waitHandle.WaitNextLine(10000);
                    Assert.True(!string.IsNullOrEmpty(line) && line.Contains("Welcome"), "check welcome message: " + line);
                    waitHandles.Add(waitHandle);
                }

                using (LogStreamWaitHandle waitHandle = new LogStreamWaitHandle(appManager.LogStreamManager.GetStream().Result))
                {
                    try
                    {
                        string line = waitHandle.WaitNextLine(10000);
                        Assert.True(!string.IsNullOrEmpty(line) && line.Contains("Welcome"), "check welcome message: " + line);

                        // write to folder0, we should not get any live stream for folder1 listener
                        string content = Guid.NewGuid().ToString();
                        WriteLogText(appManager.SiteUrl, logFiles[0], content);
                        line = waitHandle.WaitNextLine(10000);
                        Assert.Equal(content, line);
                        line = waitHandles[0].WaitNextLine(10000);
                        Assert.Equal(content, line);
                        line = waitHandles[1].WaitNextLine(1000);
                        Assert.True(line == null, "no more message: " + line);

                        // write to folder1, we should not get any live stream for folder0 listener
                        content = Guid.NewGuid().ToString();
                        WriteLogText(appManager.SiteUrl, logFiles[1], content);
                        line = waitHandle.WaitNextLine(10000);
                        Assert.Equal(content, line);
                        line = waitHandles[1].WaitNextLine(10000);
                        Assert.Equal(content, line);
                        line = waitHandles[0].WaitNextLine(1000);
                        Assert.True(line == null, "no more message: " + line);
                    }
                    finally
                    {
                        waitHandles[0].Dispose();
                        waitHandles[1].Dispose();
                    }
                }
            });
        }

        [Fact]
        public void TestLogStreamNotFound()
        {
            string appName = KuduUtils.GetRandomWebsiteName("TestLogStreamNotFound");

            ApplicationManager.Run(appName, appManager =>
            {
                RemoteLogStreamManager manager = new RemoteLogStreamManager(appManager.ServiceUrl + "/logstream/notfound");
                var ex = KuduAssert.ThrowsUnwrapped<WebException>(() => manager.GetStream().Wait());
                Assert.Equal(((HttpWebResponse)ex.Response).StatusCode, HttpStatusCode.NotFound);
            });
        }

        private static void WriteLogText(string siteUrl, string filePath, string content)
        {
            string url = String.Format("{0}?path={1}&content={2}", siteUrl, filePath, content);
            KuduAssert.VerifyUrl(url);
        }

        private static void CreateLogDirectory(string siteUrl, string directory)
        {
            if (!directory.EndsWith("\\"))
            {
                directory += "\\";
            }
            string url = String.Format("{0}?path={1}", siteUrl, directory);
            KuduAssert.VerifyUrl(url);
        }
    }
}
