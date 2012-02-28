﻿using System;
using System.Diagnostics;
using System.IO;
using Kudu.Client.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl.Git;
using SystemEnvironment = System.Environment;


namespace Kudu.TestHarness
{
    public static class Git
    {
        public static void Push(string repositoryPath, string url, string localBranchName = "master", string remoteBranchName = "master")
        {
            Executable gitExe = GetGitExe(repositoryPath);
            if (localBranchName.Equals("master"))
            {
                // Dump out the error stream (git curl verbose)
                Debug.WriteLine(gitExe.Execute("push {0} {1}", url, remoteBranchName).Item2);
            }
            else
            {
                // Dump out the error stream (git curl verbose)
                Debug.WriteLine(gitExe.Execute("push {0} {1}:{2}", url, localBranchName, remoteBranchName).Item2);
            }
        }

        public static void Init(string repositoryPath)
        {
            Executable gitExe = GetGitExe(repositoryPath);
            gitExe.Execute("init");
        }

        public static void Revert(string repositoryPath, string commit = "HEAD")
        {
            Executable gitExe = GetGitExe(repositoryPath);
            gitExe.Execute("revert --no-edit \"{0}\"", commit);
        }

        public static void Reset(string repositoryPath, string commit = "HEAD^")
        {
            Executable gitExe = GetGitExe(repositoryPath);
            gitExe.Execute("reset --hard \"{0}\"", commit);
        }

        public static void CheckOut(string repositoryPath, string branchName)
        {
            Executable gitExe = GetGitExe(repositoryPath);
            gitExe.Execute("checkout -b {0} -t origin/{0}", branchName);
        }

        public static void Commit(string repositoryPath, string message)
        {
            Executable gitExe = GetGitExe(repositoryPath);
            try
            {
                gitExe.Execute("add -A", message);
                gitExe.Execute("commit -m \"{0}\"", message);
            }
            catch (Exception ex)
            {
                // Swallow exceptions on comit, since things like changing line endings
                // show up as an error
                Debug.WriteLine(ex.Message);
            }
        }

        public static void Add(string repositoryPath, string path)
        {
            Executable gitExe = GetGitExe(repositoryPath);
            gitExe.Execute("add \"{0}\"", path);
        }

        public static TestRepository Clone(string repositoryPath, string source, bool createDirectory = false)
        {
            //Gets full path in case path is relative 
            repositoryPath = GetRepositoryPath(repositoryPath);

            // Make sure the directory is empty
            FileSystemHelpers.DeleteDirectorySafe(repositoryPath);
            Executable gitExe = GetGitExe(repositoryPath);

            if (createDirectory)
            {
                gitExe.Execute("clone \"{0}\"", source);
                // TODO: need to update this path once issue with clonning
                // and sub directory created with "git" name is solved
                return new TestRepository(Path.Combine(repositoryPath, "git"));
            }

            gitExe.Execute("clone \"{0}\" .", source);

            return new TestRepository(repositoryPath);
        }

        public static GitDeploymentResult GitDeploy(string kuduServiceUrl, string localRepoPath, string remoteRepoUrl, string localBranchName, string remoteBranchName, TimeSpan waitTimeout)
        {
            var deploymentManager = new RemoteDeploymentManager(kuduServiceUrl);

            return GitDeploy(deploymentManager, kuduServiceUrl, localRepoPath, remoteRepoUrl, localBranchName, remoteBranchName, waitTimeout);
        }

        public static GitDeploymentResult GitDeploy(RemoteDeploymentManager deploymentManager, string kuduServiceUrl, string localRepoPath, string remoteRepoUrl, string localBranchName, string remoteBranchName, TimeSpan waitTimeout)
        {
            Stopwatch sw = null;

            var result = deploymentManager.WaitForDeployment(() =>
            {
                HttpUtils.WaitForSite(kuduServiceUrl);
                sw = Stopwatch.StartNew();
                Git.Push(localRepoPath, remoteRepoUrl, localBranchName, remoteBranchName);
                sw.Stop();
            },
            waitTimeout);

            return new GitDeploymentResult
            {
                PushResponseTime = sw.Elapsed,
                TotalResponseTime = result.Item1,
                TimedOut = !result.Item2
            };
        }

        public static TestRepository CreateLocalRepository(string repositoryName)
        {
            // Get the path to the repository
            string zippedPath = Path.Combine(PathHelper.ZippedRepositoriesDir, repositoryName + ".zip");

            // Unzip it
            ZipUtils.Unzip(zippedPath, PathHelper.LocalRepositoriesDir);

            return new TestRepository(repositoryName);
        }

        public static string GetRepositoryPath(string repositoryPath)
        {
            if (Path.IsPathRooted(repositoryPath))
            {
                return repositoryPath;
            }

            return Path.Combine(PathHelper.LocalRepositoriesDir, repositoryPath);
        }

        private static string ResolveGitPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            string path = Path.Combine(programFiles, "Git", "bin", "git.exe");

            if (!File.Exists(path))
            {
                throw new InvalidOperationException("Unable to locate git.exe");
            }

            return path;
        }

        private static Executable GetGitExe(string repositoryPath)
        {
            if (!Path.IsPathRooted(repositoryPath))
            {
                repositoryPath = Path.Combine(PathHelper.LocalRepositoriesDir, repositoryPath);
            }

            FileSystemHelpers.EnsureDirectory(repositoryPath);

            var exe = new GitExecutable(repositoryPath);
            exe.SetTraceLevel(2);
            exe.SetHttpVerbose(true);
            return exe;
        }
    }
}
