﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client;
using Kudu.Client.Deployment;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.FunctionalTests
{
    [TestHarnessClassCommand]
    public class DeploymentManagerTests
    {
        [Fact]
        public async Task DeploymentApisReturn404IfDeploymentIdDoesntExist()
        {
            string appName = "Rtn404IfDeployIdNotExist";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                string id = "foo";
                var ex = await ExceptionAssert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.DeleteAsync(id));
                Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);

                ex = await ExceptionAssert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.DeployAsync(id));
                Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);

                ex = await ExceptionAssert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.DeployAsync(id, clean: true));
                Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);

                ex = await ExceptionAssert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.GetLogEntriesAsync(id));
                Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);

                ex = await ExceptionAssert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.GetResultAsync(id));
                Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);

                ex = await ExceptionAssert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.GetLogEntryDetailsAsync(id, "fakeId"));
                Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);
            });
        }

        [Fact]
        public async Task DeploymentApis()
        {
            // Arrange

            string appName = "DeploymentApis";

            using (var repo = Git.Clone("HelloWorld"))
            {
                await ApplicationManager.RunAsync(appName, async appManager =>
                {
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                    Assert.Equal(1, results.Count);
                    var result = results[0];
                    Assert.Equal("davidebbo", result.Author);
                    Assert.Equal("david.ebbo@microsoft.com", result.AuthorEmail);
                    Assert.True(result.Current);
                    Assert.Equal(DeployStatus.Success, result.Status);
                    Assert.NotNull(result.Url);
                    Assert.NotNull(result.LogUrl);
                    Assert.True(String.IsNullOrEmpty(result.Deployer));

                    // Make sure we end up on the master branch
                    CommandResult commandResult = await appManager.CommandExecutor.ExecuteCommand("git status", @"site\repository");
                    Assert.Contains("On branch master", commandResult.Output);

                    ICredentials cred = appManager.DeploymentManager.Credentials;
                    KuduAssert.VerifyUrl(result.Url, cred);
                    KuduAssert.VerifyUrl(result.LogUrl, cred);

                    var resultAgain = await appManager.DeploymentManager.GetResultAsync(result.Id);
                    Assert.Equal("davidebbo", resultAgain.Author);
                    Assert.Equal("david.ebbo@microsoft.com", resultAgain.AuthorEmail);
                    Assert.True(resultAgain.Current);
                    Assert.Equal(DeployStatus.Success, resultAgain.Status);
                    Assert.NotNull(resultAgain.Url);
                    Assert.NotNull(resultAgain.LogUrl);
                    KuduAssert.VerifyUrl(resultAgain.Url, cred);
                    KuduAssert.VerifyUrl(resultAgain.LogUrl, cred);

                    repo.WriteFile("HelloWorld.txt", "This is a test");
                    Git.Commit(repo.PhysicalPath, "Another commit");
                    appManager.GitDeploy(repo.PhysicalPath);
                    results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                    Assert.Equal(2, results.Count);
                    string oldId = results[1].Id;

                    // Delete one
                    await appManager.DeploymentManager.DeleteAsync(oldId);

                    results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                    Assert.Equal(1, results.Count);
                    Assert.NotEqual(oldId, results[0].Id);

                    result = results[0];

                    // Redeploy
                    await appManager.DeploymentManager.DeployAsync(result.Id);

                    // Clean deploy
                    await appManager.DeploymentManager.DeployAsync(result.Id, clean: true);

                    var entries = (await appManager.DeploymentManager.GetLogEntriesAsync(result.Id)).ToList();

                    Assert.True(entries.Count > 0);

                    // First entry is always null
                    Assert.Null(entries[0].DetailsUrl);

                    var entryWithDetails = entries.First(e => e.DetailsUrl != null);

                    var nested = (await appManager.DeploymentManager.GetLogEntryDetailsAsync(result.Id, entryWithDetails.Id)).ToList();

                    Assert.True(nested.Count > 0);

                    KuduAssert.VerifyLogOutput(appManager, result.Id, "Cleaning Git repository");

                    // Can't delete the active one
                    var ex = await ExceptionAssert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.DeleteAsync(result.Id));
                    Assert.Equal(HttpStatusCode.Conflict, ex.ResponseMessage.StatusCode);

                    // Corrupt git repository by removing HEAD file from it
                    // And verify git repository is not identified
                    appManager.VfsManager.Delete("site\\repository\\.git\\HEAD");

                    var notFoundException = await ExceptionAssert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.DeployAsync(null));

                    // Expect a not found failure as no repository is found (since the git repository is now corrupted)
                    Assert.Equal(HttpStatusCode.NotFound, notFoundException.ResponseMessage.StatusCode);

                    // Another got push should reinitialize the git repository
                    appManager.GitDeploy(repo.PhysicalPath);

                    results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);

                    // Make sure running this again doesn't throw an exception
                    await appManager.DeploymentManager.DeployAsync(null);
                });
            }
        }

        [Fact]
        public async Task DeploymentVerifyEtag()
        {
            string appName = "VerifyEtag";

            using (var repo = Git.Clone("HelloWorld"))
            {
                await ApplicationManager.RunAsync(appName, async appManager =>
                {
                    EntityTagHeaderValue etag = null;
                    EntityTagHeaderValue etagWithQuery = null;

                    // no etag
                    etag = await VerifyEtagAsync(appManager, "/deployments", null, HttpStatusCode.OK);
                    etagWithQuery = await VerifyEtagAsync(appManager, "/deployments/?$top=1", null, HttpStatusCode.OK);
                    Assert.NotEqual(etag, etagWithQuery);

                    // match etag
                    etag = await VerifyEtagAsync(appManager, "/deployments", etag, HttpStatusCode.NotModified);
                    etagWithQuery = await VerifyEtagAsync(appManager, "/deployments/?$top=1", etagWithQuery, HttpStatusCode.NotModified);
                    Assert.NotEqual(etag, etagWithQuery);

                    appManager.GitDeploy(repo.PhysicalPath);

                    var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(results[0].Current);

                    // mismatch etag
                    etag = await VerifyEtagAsync(appManager, "/deployments", etag, HttpStatusCode.OK);
                    etagWithQuery = await VerifyEtagAsync(appManager, "/deployments/?$top=1", etagWithQuery, HttpStatusCode.OK);
                    Assert.NotEqual(etag, etagWithQuery);

                    // match etag
                    etag = await VerifyEtagAsync(appManager, "/deployments", etag, HttpStatusCode.NotModified);
                    etagWithQuery = await VerifyEtagAsync(appManager, "/deployments/?$top=1", etagWithQuery, HttpStatusCode.NotModified);
                    Assert.NotEqual(etag, etagWithQuery);
                });
            }
        }

        private async Task<EntityTagHeaderValue> VerifyEtagAsync(ApplicationManager appManager, string uri, EntityTagHeaderValue input, HttpStatusCode statusCode)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                if (input != null)
                {
                    request.Headers.IfNoneMatch.Add(input);
                }

                using (var response = await appManager.DeploymentManager.Client.SendAsync(request))
                {
                    Assert.Equal(statusCode, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    if (response.StatusCode == HttpStatusCode.NotModified)
                    {
                        Assert.Equal(input, response.Headers.ETag);
                    }
                    else
                    {
                        Assert.NotEqual(input, response.Headers.ETag);
                    }

                    return response.Headers.ETag;
                }
            }
        }

        [Fact]
        public async Task DeploymentManagerExtensibility()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "DeploymentApis";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                await ApplicationManager.RunAsync(appName, async appManager =>
                {
                    var handler = new FakeMessageHandler()
                    {
                        InnerHandler = HttpClientHelper.CreateClientHandler(appManager.DeploymentManager.ServiceUrl, appManager.DeploymentManager.Credentials)
                    };

                    var manager = new RemoteDeploymentManager(appManager.DeploymentManager.ServiceUrl, appManager.DeploymentManager.Credentials, handler);
                    var results = (await manager.GetResultsAsync()).ToList();

                    Assert.Equal(0, results.Count);
                    Assert.NotNull(handler.Url);
                });
            }
        }

        [Fact]
        public async Task DeleteKuduSiteCleansProperly()
        {
            string appName = "DeleteKuduSiteCleansProperly";
            string defaultHtmFile = "default.htm";

            using (var repo = Git.Clone("HelloWorld"))
            {
                await ApplicationManager.RunAsync(appName, async appManager =>
                {
                    // Deploy HelloWorld repository
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                    // Verify deployed properly
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.NotNull(results[0].LastSuccessEndTime);

                    // Verify default.htm file from HelloWorld exists
                    bool defaultHtmExists = appManager.VfsWebRootManager.Exists(defaultHtmFile);
                    Assert.True(defaultHtmExists, defaultHtmFile + " doesn't exist");

                    // Add file to wwwroot not through deployment/repository
                    string extraFileName = "extra.file";
                    appManager.VfsWebRootManager.WriteAllText(extraFileName, "extra content");

                    // Delete repository without removing wwwroot
                    await appManager.RepositoryManager.Delete();
                    results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                    // Verify deployments were cleaned
                    Assert.Equal(0, results.Count);

                    // Verify extra.file was not cleaned
                    bool extraFileExists = appManager.VfsWebRootManager.Exists(extraFileName);
                    Assert.True(extraFileExists, extraFileName + " doesn't exist");

                    // Verify default.htm was not cleaned
                    defaultHtmExists = appManager.VfsWebRootManager.Exists(defaultHtmFile);
                    Assert.True(defaultHtmExists, defaultHtmFile + " doesn't exist");

                    // Redeploy HelloWorld repository
                    appManager.GitDeploy(repo.PhysicalPath);
                    results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                    // Verify deployed properly
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.NotNull(results[0].LastSuccessEndTime);

                    // Verify default.htm file from HelloWorld exists
                    defaultHtmExists = appManager.VfsWebRootManager.Exists(defaultHtmFile);
                    Assert.True(defaultHtmExists, defaultHtmFile + " doesn't exist");

                    // Verify extra.file there
                    extraFileExists = appManager.VfsWebRootManager.Exists(extraFileName);
                    Assert.True(extraFileExists, extraFileName + " doesn't exist");
                });
            }
        }

        [Fact]
        public async Task PullApiTestGitHubFormat()
        {
            string githubPayload = @"{ ""after"": ""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"", ""before"": ""7e2a599e2d28665047ec347ab36731c905c95e8b"",  ""commits"": [ { ""added"": [], ""author"": { ""email"": ""prkrishn@hotmail.com"", ""name"": ""Pranav K"", ""username"": ""pranavkm"" }, ""id"": ""43acf30efa8339103e2bed5c6da1379614b00572"", ""message"": ""Changes from master again"", ""modified"": [ ""Hello.txt"" ], ""timestamp"": ""2012-12-17T17:32:15-08:00"" } ], ""compare"": ""https://github.com/KuduApps/GitHookTest/compare/7e2a599e2d28...7e2a599e2d28"", ""created"": false, ""deleted"": false, ""forced"": false, ""head_commit"": { ""added"": [ "".gitignore"", ""SimpleWebApplication.sln"", ""SimpleWebApplication/About.aspx"", ""SimpleWebApplication/About.aspx.cs"", ""SimpleWebApplication/About.aspx.designer.cs"", ""SimpleWebApplication/Account/ChangePassword.aspx"", ""SimpleWebApplication/Account/ChangePassword.aspx.cs"", ""SimpleWebApplication/Account/ChangePassword.aspx.designer.cs"", ""SimpleWebApplication/Account/ChangePasswordSuccess.aspx"", ""SimpleWebApplication/Account/ChangePasswordSuccess.aspx.cs"", ""SimpleWebApplication/Account/ChangePasswordSuccess.aspx.designer.cs"", ""SimpleWebApplication/Account/Login.aspx"", ""SimpleWebApplication/Account/Login.aspx.cs"", ""SimpleWebApplication/Account/Login.aspx.designer.cs"", ""SimpleWebApplication/Account/Register.aspx"", ""SimpleWebApplication/Account/Register.aspx.cs"", ""SimpleWebApplication/Account/Register.aspx.designer.cs"", ""SimpleWebApplication/Account/Web.config"", ""SimpleWebApplication/Default.aspx"", ""SimpleWebApplication/Default.aspx.cs"", ""SimpleWebApplication/Default.aspx.designer.cs"", ""SimpleWebApplication/Global.asax"", ""SimpleWebApplication/Global.asax.cs"", ""SimpleWebApplication/Properties/AssemblyInfo.cs"", ""SimpleWebApplication/Scripts/jquery-1.4.1-vsdoc.js"", ""SimpleWebApplication/Scripts/jquery-1.4.1.js"", ""SimpleWebApplication/Scripts/jquery-1.4.1.min.js"", ""SimpleWebApplication/SimpleWebApplication.csproj"", ""SimpleWebApplication/Site.Master"", ""SimpleWebApplication/Site.Master.cs"", ""SimpleWebApplication/Site.Master.designer.cs"", ""SimpleWebApplication/Styles/Site.css"", ""SimpleWebApplication/Web.Debug.config"", ""SimpleWebApplication/Web.Release.config"", ""SimpleWebApplication/Web.config"" ], ""author"": { ""email"": ""david.ebbo@microsoft.com"", ""name"": ""davidebbo"", ""username"": ""davidebbo"" }, ""committer"": { ""email"": ""david.ebbo@microsoft.com"", ""name"": ""davidebbo"", ""username"": ""davidebbo"" }, ""distinct"": false, ""id"": ""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""message"": ""Initial"", ""modified"": [], ""removed"": [], ""timestamp"": ""2011-11-21T23:07:42-08:00"", ""url"": ""https://github.com/KuduApps/GitHookTest/commit/7e2a599e2d28665047ec347ab36731c905c95e8b"" }, ""pusher"": { ""name"": ""none"" }, ""ref"": ""refs/heads/foo/blah"", ""repository"": { ""created_at"": ""2012-06-28T00:07:55-07:00"", ""description"": """", ""fork"": false, ""forks"": 1, ""has_downloads"": true, ""has_issues"": true, ""has_wiki"": true, ""language"": ""ASP"", ""name"": ""GitHookTest"", ""open_issues"": 0, ""organization"": ""KuduApps"", ""owner"": { ""email"": ""kuduapps@hotmail.com"", ""name"": ""KuduApps"" }, ""private"": false, ""pushed_at"": ""2012-06-28T00:11:48-07:00"", ""size"": 188, ""url"": ""https://github.com/KuduApps/SimpleWebApplication"", ""watchers"": 1 } }";
            string appName = "PullApiTestGitHubFormat";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                await appManager.SettingsManager.SetValue("branch", "foo/blah");

                var post = new Dictionary<string, string>
                {
                    { "payload", githubPayload }
                };

                await DeployPayloadHelperAsync(appManager, client =>
                {
                    client.DefaultRequestHeaders.Add("X-Github-Event", "push");
                    return client.PostAsync("deploy?scmType=GitHub", new FormUrlEncodedContent(post));
                });

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("GitHub", results[0].Deployer);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET!");
            });
        }

        [Fact]
        public async Task PullApiTestBitbucketFormat()
        {
            string bitbucketPayload = @"{ ""canon_url"": ""https://github.com"", ""commits"": [ { ""author"": ""davidebbo"", ""branch"": ""master"", ""files"": [ { ""file"": ""Mvc3Application/Views/Home/Index.cshtml"", ""type"": ""modified"" } ], ""message"": ""Blah2\n"", ""node"": ""e550351c5188"", ""parents"": [ ""297fcc65308c"" ], ""raw_author"": ""davidebbo <david.ebbo@microsoft.com>"", ""raw_node"": ""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"", ""revision"": null, ""size"": -1, ""timestamp"": ""2012-09-20 03:11:20"", ""utctimestamp"": ""2012-09-20 01:11:20+00:00"" } ], ""repository"": { ""absolute_url"": ""/KuduApps/SimpleWebApplication"", ""fork"": false, ""is_private"": false, ""name"": ""Mvc3Application"", ""owner"": ""davidebbo"", ""scm"": ""git"", ""slug"": ""mvc3application"", ""website"": """" }, ""user"": ""davidebbo"" }";
            string appName = "PullApiTestBitbucketFormat";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var post = new Dictionary<string, string>
                {
                    { "payload", bitbucketPayload }
                };

                await appManager.SettingsManager.SetValue(SettingsKeys.UseShallowClone, "true");

                await DeployPayloadHelperAsync(appManager, client =>
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Bitbucket.org");
                    return client.PostAsync("deploy?scmType=BitbucketGit", new FormUrlEncodedContent(post));
                });

                var resultsTask = appManager.DeploymentManager.GetResultsAsync();
                var verifyUrl = KuduAssert.VerifyUrlAsync(appManager.SiteUrl, "Welcome to ASP.NET!");

                await Task.WhenAll(resultsTask, verifyUrl);

                var results = (await resultsTask).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.True(results[0].Deployer.StartsWith("Bitbucket"));
            });
        }

        [Fact]
        public async Task PullApiTestBitbucketFormatWithMercurial()
        {
            string bitbucketPayload = @"{""canon_url"":""https://bitbucket.org"",""commits"":[{""author"":""pranavkm"",""branch"":""default"",""files"":[{""file"":""Hello.txt"",""type"":""modified""}],""message"":""Some more changes"",""node"":""0bbefd70c4c4"",""parents"":[""3cb8bf8aec0a""],""raw_author"":""Pranav <pranavkm@outlook.com>"",""raw_node"":""0bbefd70c4c4213bba1e91998141f6e861cec24d"",""revision"":4,""size"":-1,""timestamp"":""2012-12-17 19:41:28"",""utctimestamp"":""2012-12-17 18:41:28+00:00""}],""repository"":{""absolute_url"":""/kudutest/hellomercurial/"",""fork"":false,""is_private"":false,""name"":""HelloMercurial"",""owner"":""kudutest"",""scm"":""hg"",""slug"":""hellomercurial"",""website"":""""},""user"":""kudutest""}";
            string appName = "PullApiTestBitbucketFormatWithMercurial";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var client = CreateClient(appManager);

                await appManager.SettingsManager.SetValue("branch", "default");

                client.DefaultRequestHeaders.Add("User-Agent", "Bitbucket.org");
                var post = new Dictionary<string, string>
                {
                    { "payload", bitbucketPayload }
                };

                (await client.PostAsync("deploy?scmType=BitbucketHg", new FormUrlEncodedContent(post))).EnsureSuccessful();

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.True(results[0].Deployer.StartsWith("Bitbucket"));

                KuduAssert.VerifyUrl(appManager.SiteUrl + "Hello.txt", "Hello mercurial");
            });
        }

        [Fact]
        public async Task PullApiTestBitbucketFormatWithPrivateMercurialRepository()
        {
            string bitbucketPayload = @"{ ""canon_url"": ""https://bitbucket.org"", ""commits"": [ { ""author"": ""pranavkm"", ""branch"": ""Test-Branch"", ""files"": [ { ""file"": ""Hello.txt"", ""type"": ""modified"" } ], ""message"": ""Hello Mercurial! change"", ""node"": ""ee26963f2e54"", ""parents"": [ ""16ea3237dbcd"" ], ""raw_author"": ""Pranav <pranavkm@outlook.com>"", ""raw_node"": ""ee26963f2e54b9db5c0cd160600b29c4f7a7eff7"", ""revision"": 10, ""size"": -1, ""timestamp"": ""2012-12-24 18:22:14"", ""utctimestamp"": ""2012-12-24 17:22:14+00:00"" } ], ""repository"": { ""absolute_url"": ""/kudutest/privatemercurial/"", ""fork"": false, ""is_private"": true, ""name"": ""PrivateMercurial"", ""owner"": ""kudutest"", ""scm"": ""hg"", ""slug"": ""privatemercurial"", ""website"": """" }, ""user"": ""kudutest"" }";
            string appName = "PullApiTestBitbucketFormatWithMercurial";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                if (!SshHelper.PrepareSSHEnv(appManager.SSHKeyManager))
                {
                    // Run SSH tests only if the key is present
                    return;
                }
                var client = CreateClient(appManager);
                await appManager.SettingsManager.SetValue("branch", "Test-Branch");

                client.DefaultRequestHeaders.Add("User-Agent", "Bitbucket.org");
                var post = new Dictionary<string, string>
                {
                    { "payload", bitbucketPayload }
                };

                (await client.PostAsync("deploy?scmType=BitbucketHg", new FormUrlEncodedContent(post))).EnsureSuccessful();

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.True(results[0].Deployer.StartsWith("Bitbucket"));

                KuduAssert.VerifyUrl(appManager.SiteUrl + "Hello.txt", "Hello mercurial!");
            });
        }

        [Fact]
        public async Task PullApiTestGitlabHQFormat()
        {
            string payload = @"{ ""before"":""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""after"":""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"", ""ref"":""refs/heads/master"", ""user_id"":1, ""user_name"":""Remco Ros"", ""commits"":[ { ""id"":""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"", ""message"":""Settings as content file"", ""timestamp"":""2012-11-11T14:32:02+01:00"", ""url"":""http://gitlab.proscat.nl/inspectbin/commits/4109312962bb269ecc3a0d7a3c82a119dcd54c8b"", ""author"":{ ""name"":""Remco Ros"", ""email"":""r.ros@proscat.nl"" }}], ""repository"":{ ""name"":""testing"", ""private"":false, ""url"":""https://github.com/KuduApps/SimpleWebApplication"", ""description"":null, ""homepage"":""https://github.com/KuduApps/SimpleWebApplication"" }}";
            string appName = "PullApiTestGitlabHQFormat";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                await DeployPayloadHelperAsync(appManager, client => client.PostAsync("deploy", new StringContent(payload)));

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("GitlabHQ", results[0].Deployer);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET!");
            });
        }

        [Fact]
        public async Task PullApiTestCodebaseFormat()
        {
            string payload = @"{ ""before"":""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""after"":""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"", ""ref"":""refs/heads/master"", ""repository"":{ ""name"":""testing"", ""public_access"":true, ""url"":""http://test.codebasehq.com/projects/test-repositories/repositories/git1"", ""clone_urls"": {""ssh"": ""git@codebasehq.com:test/test-repositories/git1.git"", ""http"": ""https://github.com/KuduApps/SimpleWebApplication""}}}";
            string appName = "PullApiTestCodebaseFormat";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                await DeployPayloadHelperAsync(appManager, client =>
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Codebasehq.com");
                    return client.PostAsync("deploy", new FormUrlEncodedContent(post));
                });

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("CodebaseHQ", results[0].Deployer);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET!");
            });
        }

        [Fact]
        public async Task PullApiTestKilnHgFormat()
        {
            string kilnPayload = @"{ ""commits"": [ { ""author"": ""Brian Surowiec <xtorted@optonline.net>"", ""branch"": ""default"", ""id"": ""0bbefd70c4c4213bba1e91998141f6e861cec24d"", ""message"": ""more fun text"", ""revision"": 20, ""tags"": [ ""tip"" ], ""timestamp"": ""1/16/2013 3:32:04 AM"", ""url"": ""https://13degrees.kilnhg.com/Code/Kudu-Public/Group/Site/History/d2415cbaa78e"" } ], ""pusher"": { ""accesstoken"": false, ""email"": ""xtorted@optonline.net"", ""fullName"": ""Brian Surowiec"" }, ""repository"": { ""central"": true, ""description"": """", ""id"": 113336, ""name"": ""Site"", ""url"": ""https://bitbucket.org/kudutest/hellomercurial/"" } }";
            string appName = "PullApiTestKilnHgFormat";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var client = CreateClient(appManager);

                // since we're pulling against bitbucket we need to simulate a self-hosted setup of kiln
                await appManager.SettingsManager.SetValue("kiln.domain", "bitbucket\\.org");
                await appManager.SettingsManager.SetValue("branch", "default");

                var post = new Dictionary<string, string>
                {
                    { "payload", kilnPayload }
                };

                (await client.PostAsync("deploy", new FormUrlEncodedContent(post))).EnsureSuccessful();

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("Kiln", results[0].Deployer);
                KuduAssert.VerifyUrl(appManager.SiteUrl + "Hello.txt", "Hello mercurial");
            });
        }

        [Fact]
        public async Task PullApiTestGenericFormat()
        {
            string payload = @"{ ""oldRef"": ""0000000000000000000"", ""newRef"": ""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"", ""url"": ""https://github.com/KuduApps/SimpleWebApplication.git"", ""deployer"" : ""CodePlex"", ""branch"":""master""  }";
            string appName = "PullApiTestGenericFormat";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                await DeployPayloadHelperAsync(appManager, client => client.PostAsync("deploy", new FormUrlEncodedContent(post)));

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("davidebbo", results[0].Author);
                Assert.Equal("david.ebbo@microsoft.com", results[0].AuthorEmail);
                Assert.Equal("Settings as content file", results[0].Message.Trim());
                Assert.Equal("ea1c6d7ea669c816dd5f86206f7b47b228fdcacd", results[0].Id);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET!");
                Assert.Equal("CodePlex", results[0].Deployer);

                // Verify the deployment status
            });
        }

        [Fact]
        public async Task PullApiTestGenericFormatCustomBranch()
        {
            string payload = @"{ ""oldRef"": ""0000000000000000000"", ""newRef"": ""b4bd5b73ec4c15019d41d16e418c3017b70b3796"", ""url"": ""https://github.com/KuduApps/RepoWithMultipleBranches.git"", ""deployer"" : ""CodePlex"", branch: ""test"" }";
            string appName = "PullApiTestGenericCustomBranch";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                await appManager.SettingsManager.SetValue("branch", "test");

                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                await DeployPayloadHelperAsync(appManager, client => client.PostAsync("deploy", new FormUrlEncodedContent(post)));

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Test branch");
                Assert.Equal("CodePlex", results[0].Deployer);
            });
        }

        [Fact]
        public async Task DeployingBranchThatExists()
        {
            string payload = @"{ ""url"": ""https://github.com/KuduApps/RepoWithMultipleBranches.git"", ""deployer"" : ""CodePlex"", branch: ""test"", newRef: ""ad21595c668f3de813463df17c04a3b23065fedc"" }";
            string appName = "DeployingBranchThatExists";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                await appManager.SettingsManager.SetValue("branch", "test");

                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                await DeployPayloadHelperAsync(appManager, client => client.PostAsync("deploy", new FormUrlEncodedContent(post)));

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Test branch");
                Assert.Equal("CodePlex", results[0].Deployer);
            });
        }

        [Fact]
        public async Task PullApiTestSimpleFormatMultiBranchWithUpdates()
        {
            var payload = new JObject();
            payload["url"] = "https://github.com/KuduApps/HelloKudu";
            payload["format"] = "basic";
            string appName = "HelloKudu";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                // Fetch master branch from first repo
                await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("GitHub", results[0].Deployer);

                KuduAssert.VerifyUrl(appManager.SiteUrl, "<h1>Hello Kudu</h1>");

                // Switch to foo branch from first repo
                await appManager.SettingsManager.SetValue("branch", "foo");
                await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));
                KuduAssert.VerifyUrl(appManager.SiteUrl, "<h1>Hello Kudu - foo</h1>");

                // Fetch master branch from second repo to simulate update. It has one more commit over first repo
                payload["url"] = "https://github.com/KuduApps/HelloKudu2";
                await appManager.SettingsManager.SetValue("branch", "master");
                await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));
                KuduAssert.VerifyUrl(appManager.SiteUrl, "<h1>Hello again Kudu</h1>");

                // Fetch foo branch from second repo to simulate update. It has a different commit that cannot be
                // fast-forwarded from the foo branch in the first repo
                await appManager.SettingsManager.SetValue("branch", "foo");
                await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));
                KuduAssert.VerifyUrl(appManager.SiteUrl, "<h1>Hi Kudu foo</h1>");
            });
        }

        [Fact]
        public async Task PullApiTestSimpleFormatWithMercurial()
        {
            string payload = @"{""url"":""https://bitbucket.org/kudutest/hellomercurial/"",""format"":""basic"",""scm"":""hg""}";
            string appName = "PullApiTestSimpleFormatWithMercurial";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var client = CreateClient(appManager);

                await appManager.SettingsManager.SetValue("branch", "default");

                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                (await client.PostAsync("deploy", new FormUrlEncodedContent(post))).EnsureSuccessful();

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("Bitbucket", results[0].Deployer);

                KuduAssert.VerifyUrl(appManager.SiteUrl + "Hello.txt", "Hello mercurial");
            });
        }

        [Fact]
        public async Task PullApiTestSimpleFormatWithScmTypeNone()
        {
            var payload = new JObject();
            payload["url"] = "https://github.com/KuduApps/HelloKudu";
            payload["format"] = "basic";
            string appName = "HelloKudu";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                await appManager.SettingsManager.SetValue(SettingsKeys.ScmType, ScmType.None.ToString());

                await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("GitHub", results[0].Deployer);

                KuduAssert.VerifyUrl(appManager.SiteUrl, "Hello Kudu");
            });
        }

        [Fact]
        public async Task PullApiTestRepoWithLongPath()
        {
            var payload = new JObject();
            payload["url"] = "https://github.com/KuduApps/RepoWithLongPath.git";
            payload["format"] = "basic";
            string appName = "RepoWithLongPath";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var exception = await ExceptionAssert.ThrowsAsync<HttpUnsuccessfulRequestException>(async () =>
                {
                    await PostPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));
                });

                Assert.Contains("unable to create file symfony", exception.Message);

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Failed, results[0].Status);

                var entries = (await appManager.DeploymentManager.GetLogEntriesAsync(results[0].Id)).ToList();
                Assert.Equal(1, entries.Count);
                Assert.Equal("Fetching changes.", entries[0].Message);
                Assert.Equal(LogEntryType.Error, entries[0].Type);

                var details = (await appManager.DeploymentManager.GetLogEntryDetailsAsync(results[0].Id, entries[0].Id)).ToList();
                Assert.True(details.Count > 0, "must have at one log detail entry.");
                Assert.Contains("unable to create file symfony", details[0].Message);
                Assert.Equal(LogEntryType.Error, details[0].Type);

                // Must not have entry with "An unknown error has occurred"
                foreach (var detail in details)
                {
                    Assert.False(detail.Message.Contains("An unknown error has occurred"), "Must not contain unknow error!");
                }
            });
        }

        [Theory]
        [InlineData("https://github.com/KuduApps/EmptyGitRepo", null)]
        [InlineData("https://bitbucket.org/kudutest/emptyhgrepo", "hg")]
        public async Task PullApiTestEmptyRepo(string url, string scm)
        {
            var payload = new JObject();
            payload["url"] = url;
            payload["format"] = "basic";
            if (!String.IsNullOrEmpty(scm))
            {
                payload["scm"] = scm;
            }

            string appName = "PullApiTestGitEmptyRepo";
            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                // Fetch master branch from first repo
                await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(0, results.Count);
            });
        }

        [Fact]
        public async Task DeployHookWithInvalidHttpMethod()
        {
            string appName = "HelloKudu";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var client = CreateClient(appManager);

                HttpResponseMessage response = await client.GetAsync("deploy");

                // It's OK because it gets redirected to the Kudu root
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                response = await client.DeleteAsync("deploy");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

                try
                {
                    response = await client.PutAsync("deploy");
                }
                catch (HttpRequestException ex)
                {
                    Assert.Contains("404", ex.Message);
                }
            });
        }

        [Theory]
        [InlineData(null)]
        [InlineData("hg")]
        public async Task PullApiTestRepoInvalidUrl(string scm)
        {
            Random random = new Random();
            string appName = "RepoInvalidUrl";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                await appManager.SSHKeyManager.GetPublicKey(ensurePublicKey: true);

                // Run per each scm in random order.
                foreach (var info in GetRepoInvalidInfos().Where(r => r.Scm == scm).OrderBy(r => random.Next()))
                {
                    TestTracer.Trace("Scenario: " + info);

                    // Test
                    var exception = await ExceptionAssert.ThrowsAsync<HttpUnsuccessfulRequestException>(async () =>
                    {
                        await PostPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", info.Payload));
                    });

                    // Assert
                    KuduAssert.Match(info.Expect, exception.Message, info.ToString());
                }
            });
        }

        private static IEnumerable<RepoInvalidInfo> GetRepoInvalidInfos()
        {
            yield return new RepoInvalidInfo("InvalidUrl", "Repository url 'InvalidUrl' is invalid.", null);
            yield return new RepoInvalidInfo("InvalidUrl", "Repository url 'InvalidUrl' is invalid.", null);
            yield return new RepoInvalidInfo(".", "Repository url '.' is invalid.", null);
            yield return new RepoInvalidInfo("http://google.com/", "fatal:.*http://.*google.com.* not found", null);
            yield return new RepoInvalidInfo("http://google.com/", "abort: 'http://www.google.com/' does not appear to be an hg repository", "hg");
            yield return new RepoInvalidInfo("InvalidScheme://abcdefghigkl.com/", "fatal: Unable to find remote helper for 'InvalidScheme'", null);
            yield return new RepoInvalidInfo("InvalidScheme://abcdefghigkl.com/", "abort: repository InvalidScheme://abcdefghigkl.com/ not found", "hg");
            yield return new RepoInvalidInfo("http://abcdefghigkl.com/", "Could.*n.*t resolve host.*abcdefghigkl.com", null);
            yield return new RepoInvalidInfo("http://abcdefghigkl.com/", "abort: error: getaddrinfo failed.*hg.exe pull", "hg");
            yield return new RepoInvalidInfo("https://abcdefghigkl.com/", "Could.*n.*t resolve host.*abcdefghigkl.com", null);
            yield return new RepoInvalidInfo("https://abcdefghigkl.com/", "abort: error: getaddrinfo failed.*hg.exe pull", "hg");
            yield return new RepoInvalidInfo("git@abcdefghigkl.com:Invalid/Invalid.git", "ssh: abcdefghigkl.com: no address associated with name", null);
            yield return new RepoInvalidInfo("ssh://hg@abcdefghigkl.com/Invalid/Invalid.git", "abort: no suitable response from remote hg.*hg.exe pull", "hg");
            yield return new RepoInvalidInfo("git@github.com:Invalid/Invalid.git", "Permission denied [(]publickey[)]", null);
            yield return new RepoInvalidInfo("git@bitbucket.org:Invalid/Invalid.git", "Permission denied [(]publickey[)]", null);
            yield return new RepoInvalidInfo("git@github.com:KuduApps/Invalid.git", "Permission denied [(]publickey[)]", null);
            yield return new RepoInvalidInfo("git@bitbucket.org:kudutest/Invalid.git", "Permission denied [(]publickey[)]", null);
            yield return new RepoInvalidInfo("git@github.com:KuduApps/HelloKudu.git", "Permission denied [(]publickey[)]", null);
            yield return new RepoInvalidInfo("git@bitbucket.org:kudutest/jeanprivate.git", "Permission denied [(]publickey[)]", null);
            // due to unreliable error from github
            // yield return new RepoInvalidInfo("https://github.com/KuduApps/HelloKudu.git", "abort: HTTP Error 406: Not Acceptable.*hg.exe pull https://github.com/KuduApps/HelloKudu.git", "hg");
            yield return new RepoInvalidInfo("https://bitbucket.org/kudutest/hellomercurial/", "fatal:.*https://bitbucket.org/kudutest/hellomercurial.* not found", null);
            yield return new RepoInvalidInfo("https://github.com/Invalid/Invalid.git", "fatal: Authentication failed.*git.exe fetch", null);
            yield return new RepoInvalidInfo("https://github.com/KuduQAOrg/Invalid.git", "fatal: Authentication failed.*git.exe fetch", null);
            yield return new RepoInvalidInfo("https://github.com/KuduQAOrg/PrivateSubModule.git", "fatal: Authentication failed.*git.exe fetch", null);
            yield return new RepoInvalidInfo("https://KuduQAOrg@github.com/KuduQAOrg/PrivateSubModule.git", "fatal: Authentication failed.*git.exe fetch", null);
            yield return new RepoInvalidInfo("https://wrongusr@github.com/KuduQAOrg/PrivateSubModule.git", "fatal: Authentication failed.*git.exe fetch", null);
            yield return new RepoInvalidInfo("https://KuduQAOrg:wrongpwd@github.com/KuduQAOrg/PrivateSubModule.git", "fatal: Authentication failed.*git.exe fetch external", null);
            yield return new RepoInvalidInfo("https://bitbucket.org/Invalid/Invalid.git", "fatal:.*https://bitbucket.org/Invalid/Invalid.git.* not found", null);
            yield return new RepoInvalidInfo("https://bitbucket.org/kudutest/Invalid.git", "fatal:.*https://bitbucket.org/kudutest/Invalid.git.* not found", null);
            yield return new RepoInvalidInfo("https://bitbucket.org/kudutest/jeanprivate.git", "fatal: Authentication failed.*git.exe fetch", null);
            yield return new RepoInvalidInfo("https://kudutest@bitbucket.org/kudutest/jeanprivate.git", "fatal: Authentication failed.*git.exe fetch", null);
            yield return new RepoInvalidInfo("https://wrongusr@bitbucket.org/kudutest/jeanprivate.git", "fatal: Authentication failed.*git.exe fetch", null);
            yield return new RepoInvalidInfo("https://kudutest:wrongpwd@bitbucket.org/kudutest/jeanprivate.git", "fatal: Authentication failed.*git.exe fetch external", null);
        }

        public class RepoInvalidInfo
        {
            public RepoInvalidInfo(string url, string expect, string scm)
            {
                this.Url = url;
                this.Expect = expect;
                this.Scm = scm;
                this.Payload = new JObject();
                this.Payload["url"] = url;
                this.Payload["format"] = "basic";
                if (!String.IsNullOrEmpty(scm))
                {
                    this.Payload["scm"] = scm;
                }
            }

            public string Url { get; set; }
            public string Expect { get; set; }
            public string Scm { get; set; }
            public JObject Payload { get; set; }
            public override string ToString()
            {
                return String.Format("RepoInvalidInfo(url: \"{0},\" expect: \"{1}\", scm: \"{2}\")", this.Url, this.Expect, this.Scm);
            }
        }

        private static async Task DeployPayloadHelperAsync(ApplicationManager appManager, Func<HttpClient, Task<HttpResponseMessage>> func)
        {
            (await PostPayloadHelperAsync(appManager, func)).EnsureSuccessful().Dispose();
        }

        private static async Task<HttpResponseMessage> PostPayloadHelperAsync(ApplicationManager appManager, Func<HttpClient, Task<HttpResponseMessage>> func)
        {
            using (HttpClient client = CreateClient(appManager))
            {
                HttpResponseMessage response = await func(client);

                if (response.StatusCode == HttpStatusCode.InternalServerError)
                {
                    response.EnsureSuccessful();
                }

                return response;
            }
        }

        private static HttpClient CreateClient(ApplicationManager appManager)
        {
            HttpClientHandler handler = HttpClientHelper.CreateClientHandler(appManager.ServiceUrl, appManager.DeploymentManager.Credentials);
            return new HttpClient(handler)
            {
                BaseAddress = new Uri(appManager.ServiceUrl),
                Timeout = TimeSpan.FromMinutes(5)
            };
        }

        private async Task WaitForAnyBuildingDeploymentAsync(ApplicationManager appManager)
        {
            bool deploying = false;
            int breakLoop = 0;
            do
            {
                Thread.Sleep(100);

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                deploying =
                    results != null &&
                    results.Any(r => r.Status == DeployStatus.Building);

                breakLoop++;
                if (breakLoop > 200)
                {
                    Assert.True(false, "No deployment result in pending state");
                }
            }
            while (!deploying);
        }

        private class FakeMessageHandler : DelegatingHandler
        {
            public Uri Url { get; set; }

            protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                Url = request.RequestUri;
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
