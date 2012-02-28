﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class GitRepositoryManagementTests
    {
        [Fact]
        public void PushSimpleRepoShouldDeploy()
        {
            // Arrange
            string repositoryName = "Bakery10";
            string appName = "PushSimpleRepoShouldDeploy";
            string verificationText = "Welcome to Fourth Coffee!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void PushRepoWithMultipleProjectsShouldDeploy()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "PushRepoWithMultipleProjectsShouldDeploy";
            string verificationText = "Welcome to ASP.NET MVC! - Change1";
            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.NotNull(results[0].LastSuccessEndTime);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void WarningsAsErrors()
        {
            // Arrange
            string repositoryName = "WarningsAsErrors";
            string appName = "WarningsAsErrors";
            string cloneUrl = "https://github.com/KuduApps/WarningsAsErrors.git";

            using (var repo = Git.Clone(repositoryName, cloneUrl))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    Assert.Null(results[0].LastSuccessEndTime);
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, "Warning as Error: The variable 'x' is declared but never used");
                });
            }
        }

        [Fact]
        public void PushRepoWithProjectAndNoSolutionShouldDeploy()
        {
            // Arrange
            string repositoryName = "Mvc3Application_NoSolution";
            string appName = "PushRepoWithProjectAndNoSolutionShouldDeploy";
            string verificationText = "Kudu Deployment Testing: Mvc3Application_NoSolution";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }
        
        [Fact]
        public void PushRepositoryWithNoDeployableProjectsTreatsAsWebsite()
        {
            // Arrange
            string repositoryName = "PushRepositoryWithNoDeployableProjectsTreatsAsWebsite";
            string appName = "PushRepositoryWithNoDeployableProjectsTreatsAsWebsite";
            string cloneUrl = "https://github.com/KuduApps/NoDeployableProjects.git";

            using (var repo = Git.Clone(repositoryName, cloneUrl))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, "no deployable projects. Deploying files instead");
                });
            }
        }

        [Fact]
        public void WapBuildsReleaseMode()
        {
            // Arrange
            string repositoryName = "WapBuildsReleaseMode";
            string appName = "WapBuildsReleaseMode";
            string cloneUrl = "https://github.com/KuduApps/ConditionalCompilation.git";

            using (var repo = Git.Clone(repositoryName, cloneUrl))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "RELEASE MODE", "Context.IsDebuggingEnabled: False");
                });
            }
        }

        [Fact]
        public void PushAppChangesShouldTriggerBuild()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "PushAppChangesShouldTriggerBuild";
            string verificationText = "Welcome to ASP.NET MVC!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    Git.Revert(repo.PhysicalPath);
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void DeletesToRepositoryArePropagatedForWaps()
        {
            string repositoryName = "Mvc3Application";
            string appName = "DeletesToRepositoryArePropagatedForWaps";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string deletePath = Path.Combine(repo.PhysicalPath, @"Mvc3Application\Controllers\AccountController.cs");
                    string projectPath = Path.Combine(repo.PhysicalPath, @"Mvc3Application\Mvc3Application.csproj");

                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "Account/LogOn", statusCode: HttpStatusCode.OK);

                    File.Delete(deletePath);
                    File.WriteAllText(projectPath, File.ReadAllText(projectPath).Replace(@"<Compile Include=""Controllers\AccountController.cs"" />", ""));
                    Git.Commit(repo.PhysicalPath, "Deleted the filez");
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "Account/LogOn", statusCode: HttpStatusCode.NotFound);
                });
            }
        }

        [Fact]
        public void PushShouldOverwriteModifiedFilesInRepo()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "PushShouldOverwriteModifiedFilesInRepo";
            string verificationText = "Welcome to ASP.NET MVC!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);

                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);

                    appManager.ProjectSystem.WriteAllText("Views/Home/Index.cshtml", "Hello world!");

                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Hello world!");

                    // Make an unrelated change (newline to the end of web.config)
                    repo.AppendFile(@"Mvc3Application\Web.config", "\n");

                    Git.Commit(repo.PhysicalPath, "This is a test");

                    appManager.AssertGitDeploy(repo.PhysicalPath);

                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void GoingBackInTimeShouldOverwriteModifiedFilesInRepo()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "GoingBackInTimeShouldOverwriteModifiedFilesInRepo";
            string verificationText = "Welcome to ASP.NET MVC!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                string id = repo.CurrentId;

                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repositoryName);

                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);

                    repo.AppendFile(@"Mvc3Application\Views\Home\Index.cshtml", "Say Whattttt!");

                    // Make a small changes and commit them to the local repo
                    Git.Commit(repositoryName, "This is a small changes");

                    // Push those changes
                    appManager.AssertGitDeploy(repositoryName);

                    // Make a server site change and verify it shows up
                    appManager.ProjectSystem.WriteAllText("Views/Home/Index.cshtml", "Hello world!");

                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Hello world!");

                    // Now go back in time
                    appManager.DeploymentManager.WaitForDeployment(() =>
                    {
                        appManager.DeploymentManager.DeployAsync(id).Wait();
                    });


                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void DeletesToRepositoryArePropagatedForNonWaps()
        {
            string repositoryName = "Bakery10";
            string appName = "DeletesToRepositoryArePropagatedForNonWaps";
            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string deletePath = Path.Combine(repo.PhysicalPath, @"Styles");

                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);

                    // Assert
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "Styles/Site.css", statusCode: HttpStatusCode.OK);


                    Directory.Delete(deletePath, recursive: true);
                    Git.Commit(repo.PhysicalPath, "Deleted all styles");

                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "Styles/Site.css", statusCode: HttpStatusCode.NotFound);
                });
            }
        }

        [Fact]
        public void FirstPushDeletesPriorContent()
        {
            string repositoryName = "Bakery10";
            string appName = "FirstPushDeletesPriorContent";
            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    appManager.ProjectSystem.WriteAllText("foo.txt", "This is a test file");
                    string url = appManager.SiteUrl + "/foo.txt";

                    KuduAssert.VerifyUrl(url, "This is a test file");

                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);

                    // Assert
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl);
                    KuduAssert.VerifyUrl(url, statusCode: HttpStatusCode.NotFound);
                });
            }
        }

        [Fact]
        public void PushingToNonMasterBranchNoOps()
        {
            string repositoryName = "PushingToNonMasterBranchNoOps";
            string appName = "PushingToNonMasterBranchNoOps";
            string cloneUrl = "https://github.com/KuduApps/RepoWithMultipleBranches.git";
            using (var repo = Git.Clone(repositoryName, cloneUrl))
            {
                Git.CheckOut(repo.PhysicalPath, "test");
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath, "test", "test");
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(0, results.Count);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "The web site is under construction");
                });
            }
        }

        [Fact]
        public void PushingNonMasterBranchToMasterBranchShouldDeploy()
        {
            string repositoryName = "PushingNonMasterBranchToMasterBranchShouldDeploy";
            string appName = "PushingNonMasterBranchToMasterBranchShouldDeploy";
            string cloneUrl = "https://github.com/KuduApps/RepoWithMultipleBranches.git";
            using (var repo = Git.Clone(repositoryName, cloneUrl))
            {
                Git.CheckOut(repo.PhysicalPath, "test");
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath, "test", "master");
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Test branch");
                });
            }
        }

        [Fact]
        public void CloneFromEmptyRepoAndPushShouldDeploy()
        {
            string repositoryName = "CloneFromEmptyRepoAndPushShouldDeploy";
            string appName = "CloneFromEmptyRepoAndPushShouldDeploy";

            ApplicationManager.Run(appName, appManager =>
            {
                // Act
                using (var repo = Git.Clone(repositoryName, appManager.GitUrl, true))
                {
                    // Add a file
                    repo.WriteFile("hello.txt", "Wow");
                    Git.Commit(repo.PhysicalPath, "Added hello.txt");
                    string helloUrl = appManager.SiteUrl + "/hello.txt";

                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    KuduAssert.VerifyUrl(helloUrl, "Wow");
                }
            });
        }


        [Fact]
        public void GoingBackInTimeDeploysOldFiles()
        {
            string repositoryName = "Bakery10";
            string appName = "GoingBackInTimeDeploysOldFiles";
            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                string originalCommitId = repo.CurrentId;

                ApplicationManager.Run(appName, appManager =>
                {
                    // Deploy the app
                    appManager.AssertGitDeploy(repo.PhysicalPath);

                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(1, results.Count);
                    KuduAssert.VerifyUrl(appManager.SiteUrl);

                    // Add a file
                    File.WriteAllText(Path.Combine(repo.PhysicalPath, "hello.txt"), "Wow");
                    Git.Commit(repo.PhysicalPath, "Added hello.txt");
                    string helloUrl = appManager.SiteUrl + "/hello.txt";

                    // Deploy those changes
                    appManager.AssertGitDeploy(repo.PhysicalPath);

                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    KuduAssert.VerifyUrl(helloUrl, "Wow");

                    appManager.DeploymentManager.WaitForDeployment(() =>
                    {
                        // Go back to the first deployment
                        appManager.DeploymentManager.DeployAsync(originalCommitId).Wait();
                    });

                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    KuduAssert.VerifyUrl(helloUrl, statusCode: HttpStatusCode.NotFound);
                    Assert.Equal(2, results.Count);
                });
            }
        }

        [Fact]
        public void NpmSiteInstallsPackages()
        {
            string repositoryName = "NpmSiteInstallsPackages";
            string appName = "NpmSiteInstallsPackages";
            string cloneUrl = "https://github.com/KuduApps/NpmSite.git";

            using (var repo = Git.Clone(repositoryName, cloneUrl))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    var files = appManager.ProjectSystem.GetProject().Files.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(files.Any(f => f.StartsWith("node_modules/express")));
                });
            }
        }

        [Fact]
        public void FailedNpmFailsDeployment()
        {
            string repositoryName = "FailedNpmFailsDeployment";
            string appName = "FailedNpmFailsDeployment";
            string cloneUrl = "https://github.com/KuduApps/NpmSite.git";

            using (var repo = Git.Clone(repositoryName, cloneUrl))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Replace the express dependency with something that doesn't exist
                    repo.Replace("package.json", "express", "MadeUpKuduPackage");
                    Git.Commit(repo.PhysicalPath, "Added fake package to package.json");
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, "'MadeUpKuduPackage' is not in the npm registry.");
                });
            }
        }

        [Fact]
        public void SpecificDeploymentConfiguration()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfiguration",
                                          "WebApplication1",
                                          "This is the application I want deployed");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForProjectFile()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForProjectFile",
                                          "WebApplication1/WebApplication1.csproj",
                                          "This is the application I want deployed");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForWebsite()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForWebsite",
                                          "WebSite1",
                                          "This is a website!");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForWebsiteWithSlash()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForWebsiteWithSlash",
                                          "/WebSite1",
                                          "This is a website!");
        }

        private void VerifyDeploymentConfiguration(string name, string targetProject, string expectedText)
        {
            string cloneUrl = "https://github.com/KuduApps/SpecificDeploymentConfiguration.git";
            using (var repo = Git.Clone(name, cloneUrl))
            {
                ApplicationManager.Run(name, appManager =>
                {
                    string deploymentFile = Path.Combine(repo.PhysicalPath, @".deployment");
                    File.WriteAllText(deploymentFile, String.Format(@"[config]
project = {0}", targetProject));
                    Git.Commit(name, "Updated configuration");

                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, expectedText);
                });
            }
        }
    }
}
