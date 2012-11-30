﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Xunit;
using Xunit.Extensions;

namespace Kudu.FunctionalTests
{
    public class GitDeploymentTests
    {
        public static IEnumerable<object[]> AspNetApps
        {
            get
            {
                //// Name, RepoName, RepoUrl, RepoCloneUrl, DefaultBranchName, VerificationText, ExpectedResponseCode, Skip, VerificationLogText
                yield return new object[] { "Orchard", "Orchard", "https://github.com/KuduApps/Orchard.git", 
                    "master", "Welcome to Orchard", HttpStatusCode.OK, "" };

                yield return new object[] { "ProjectWithNoSolution", "ProjectWithNoSolution", "https://github.com/KuduApps/ProjectWithNoSolution.git", 
                    "master", "Project without solution", HttpStatusCode.OK, "" };

                yield return new object[] { "HiddenFoldersAndFiles", "HiddenFoldersAndFiles", "https://github.com/KuduApps/HiddenFoldersAndFiles.git", 
                    "master", "Hello World", HttpStatusCode.OK, "" };

                yield return new object[] { "WebSiteInSolution", "WebSiteInSolution", "https://github.com/KuduApps/WebSiteInSolution.git", 
                    "master", "SomeDummyLibrary.Class1", HttpStatusCode.OK, "" };

                yield return new object[] { "kuduglob", "kuduglob", "https://github.com/KuduApps/kuduglob.git", 
                    "master", "ASP.NET MVC", HttpStatusCode.OK, "酷度" };

                yield return new object[] { "AppWithPostBuildEvent", "AppWithPostBuildEvent", "https://github.com/KuduApps/AppWithPostBuildEvent.git", 
                    "master", "Hello Kudu", HttpStatusCode.OK, "Deployment successful" };
            }
        }

        [Theory]
        [PropertyData("AspNetApps")]
        public void PushAndDeployAspNetApps(string name, string repoName, string repoCloneUrl,
                                      string defaultBranchName, string verificationText,
                                      HttpStatusCode expectedResponseCode, string verificationLogText = null)
        {
            PushAndDeployApps(name, repoName, repoCloneUrl, defaultBranchName, verificationText, expectedResponseCode, verificationLogText);
        }


        public static IEnumerable<object[]> NodeApps
        {
            get
            {
                // Name, RepoName, RepoUrl, RepoCloneUrl, DefaultBranchName, VerificationText, ExpectedResponseCode, Skip, VerificationLogText
                yield return new object[] { "Express-Template", "Express-Template", "https://github.com/KuduApps/Express-Template.git", 
                    "master", "Modify this template to jump-start your Node.JS Express Web Pages application", HttpStatusCode.OK, "" };
            }
        }

        [Theory]
        [PropertyData("NodeApps")]
        public void PushAndDeployNodeApps(string name, string repoName, string repoCloneUrl,
                                      string defaultBranchName, string verificationText,
                                      HttpStatusCode expectedResponseCode, string verificationLogText = null)
        {
            // Ensure node is installed.
            Assert.Contains("nodejs", Environment.GetEnvironmentVariable("Path"), StringComparison.OrdinalIgnoreCase);
            PushAndDeployApps(name, repoName, repoCloneUrl, defaultBranchName, verificationText, expectedResponseCode, verificationLogText);
        }

        public static IEnumerable<object[]> PhpApps
        {
            get
            {
                // Name, RepoName, RepoUrl, RepoCloneUrl, DefaultBranchName, VerificationText, ExpectedResponseCode, Skip, VerificationLogText
                yield return new object[] { "PhpBB", "PhpBBweb", "https://github.com/KuduApps/PhpBBweb.git", 
                    "master", "Welcome to phpBB3", HttpStatusCode.OK, "" };
            }
        }

        [Theory]
        [PropertyData("PhpApps")]
        public void PushAndDeployPhpApps(string name, string repoName, string repoCloneUrl,
                                         string defaultBranchName, string verificationText,
                                         HttpStatusCode expectedResponseCode, string verificationLogText = null)
        {
            PushAndDeployApps(name, repoName, repoCloneUrl, defaultBranchName, verificationText, expectedResponseCode, verificationLogText);
        }

        private static void PushAndDeployApps(string name, string repoName, string repoCloneUrl, string defaultBranchName,
                                              string verificationText, HttpStatusCode expectedResponseCode, string verificationLogText)
        {
            string randomTestName = KuduUtils.GetRandomWebsiteName(repoName);


            ApplicationManager.Run(randomTestName, appManager =>
            {
                // Act
                using (TestRepository testRepository = Git.Clone(randomTestName, repoCloneUrl))
                {
                    appManager.GitDeploy(testRepository.PhysicalPath, defaultBranchName);
                }
                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                // Assert
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText, expectedResponseCode);
                if (!String.IsNullOrEmpty(verificationLogText.Trim()))
                {
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, verificationLogText.Trim());
                }
            });
        }
    }
}
