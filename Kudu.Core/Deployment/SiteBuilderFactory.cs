﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public class SiteBuilderFactory : ISiteBuilderFactory
    {
        private readonly IEnvironment _environment;
        private readonly IBuildPropertyProvider _propertyProvider;

        public SiteBuilderFactory(IBuildPropertyProvider propertyProvider, IEnvironment environment)
        {
            _propertyProvider = propertyProvider;
            _environment = environment;
        }

        public ISiteBuilder CreateBuilder(ITracer tracer, ILogger logger)
        {
            string repositoryRoot = _environment.DeploymentRepositoryPath;

            // If there's a custom deployment file then let that take over.
            string customDeploymentFile;
            if (CustomBuilder.TryGetCustomDeploymentFile(repositoryRoot, out customDeploymentFile))
            {
                return new CustomBuilder(repositoryRoot, customDeploymentFile, _propertyProvider);
            }

            var configuration = new DeploymentConfiguration(repositoryRoot);

            // If the repository has an explicit pointer to a project path to be deployed
            // then use it.
            string targetProjectPath = configuration.ProjectPath;
            if (!String.IsNullOrEmpty(targetProjectPath))
            {
                tracer.Trace("Found .deployment file in repository");

                // Try to resolve the project
                return ResolveProject(repositoryRoot,
                                      targetProjectPath,
                                      tryWebSiteProject: true,
                                      searchOption: SearchOption.TopDirectoryOnly);
            }

            // Get all solutions in the current repository path
            var solutions = VsHelper.GetSolutions(repositoryRoot).ToList();

            if (!solutions.Any())
            {
                return ResolveProject(repositoryRoot,
                                      searchOption: SearchOption.AllDirectories);
            }

            // More than one solution is ambiguous
            if (solutions.Count > 1)
            {
                // TODO: Show relative paths in error messages
                ThrowAmbiguousSolutionsError(solutions);
            }

            // We have a solution
            VsSolution solution = solutions[0];

            // We need to determine what project to deploy so get a list of all web projects and
            // figure out with some heuristic, which one to deploy. 

            // TODO: Pick only 1 and throw if there's more than one
            VsSolutionProject project = solution.Projects.Where(p => p.IsWap || p.IsWebSite).FirstOrDefault();

            if (project == null)
            {
                logger.Log(Resources.Log_NoDeployableProjects, solution.Path);

                return new BasicBuilder(repositoryRoot, _environment.TempPath, _environment.ScriptPath);
            }

            if (project.IsWap)
            {
                return new WapBuilder(_propertyProvider,
                                      repositoryRoot,
                                      project.AbsolutePath,
                                      _environment.TempPath,
                                      _environment.NuGetCachePath,
                                      solution.Path);
            }

            return new WebSiteBuilder(_propertyProvider, 
                                      repositoryRoot, 
                                      project.AbsolutePath, 
                                      _environment.TempPath, 
                                      _environment.NuGetCachePath, 
                                      solution.Path);
        }

        private ISiteBuilder ResolveProject(string repositoryRoot, bool tryWebSiteProject = false, SearchOption searchOption = SearchOption.AllDirectories)
        {
            return ResolveProject(repositoryRoot, repositoryRoot, tryWebSiteProject, searchOption, specificConfiguration: false);
        }

        private ISiteBuilder ResolveProject(string repositoryRoot, string targetPath, bool tryWebSiteProject, SearchOption searchOption = SearchOption.AllDirectories, bool specificConfiguration = true)
        {
            if (DeploymentHelper.IsProject(targetPath))
            {
                return DetermineProject(repositoryRoot, targetPath);
            }

            // Check for loose projects
            var projects = DeploymentHelper.GetProjects(targetPath, searchOption);
            if (projects.Count > 1)
            {
                // Can't determine which project to build
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                                  Resources.Error_AmbiguousProjects,
                                                                  String.Join(", ", projects)));
            }
            else if (projects.Count == 1)
            {
                return DetermineProject(repositoryRoot, projects[0]);
            }

            if (tryWebSiteProject)
            {
                // Website projects need a solution to build so look for one in the repository path
                // that has this website in it.
                var solutions = VsHelper.FindContainingSolutions(repositoryRoot, targetPath);

                // More than one solution is ambiguous
                if (solutions.Count > 1)
                {
                    ThrowAmbiguousSolutionsError(solutions);
                }
                else if (solutions.Count == 1)
                {
                    // Unambiguously pick the root
                    return new WebSiteBuilder(_propertyProvider, 
                                              repositoryRoot, 
                                              targetPath, 
                                              _environment.TempPath, 
                                              _environment.NuGetCachePath, 
                                              solutions[0].Path);
                }
            }

            // This should only ever happen if the user specifies an invalid directory.
            // The other case where the method is called we always resolve the path so it's a non issue there.
            if (specificConfiguration && !Directory.Exists(targetPath))
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                                  Resources.Error_ProjectDoesNotExist,
                                                                  targetPath));
            }

            // If there's none then use the basic builder (the site is xcopy deployable)
            return new BasicBuilder(targetPath, _environment.TempPath, _environment.ScriptPath);
        }

        private ISiteBuilder DetermineProject(string repositoryRoot, string targetPath)
        {
            if (!DeploymentHelper.IsDeployableProject(targetPath))
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, 
                                                                  Resources.Error_ProjectNotDeployable, 
                                                                  targetPath));
            }
            else if (File.Exists(targetPath))
            {
                var solution = VsHelper.FindContainingSolution(repositoryRoot, targetPath);
                string solutionPath = solution != null ? solution.Path : null;

                return new WapBuilder(_propertyProvider,
                                      repositoryRoot,
                                      targetPath,
                                      _environment.TempPath,
                                      _environment.NuGetCachePath,
                                      solutionPath);
            }

            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                                  Resources.Error_ProjectDoesNotExist,
                                                                  targetPath));
        }

        private static void ThrowAmbiguousSolutionsError(IList<VsSolution> solutions)
        {
            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                              Resources.Error_AmbiguousSolutions,
                                                              String.Join(", ", solutions.Select(s => s.Path))));
        }
    }
}
