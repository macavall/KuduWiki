﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.SourceControl.None;
using Kudu.Core.Tracing;

namespace Kudu.Core.SourceControl
{
    // TODO: Add unit tests via FileSystem once they add support for EnumerateFiles
    public class RepositoryFactory : IRepositoryFactory
    {
        private readonly IEnvironment _environment;
        private readonly ITraceFactory _traceFactory;
        private readonly IDeploymentSettingsManager _settings;
        private readonly HttpContextBase _httpContext;

        public RepositoryFactory(IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory traceFactory, HttpContextBase httpContext)
        {
            _environment = environment;
            _settings = settings;
            _traceFactory = traceFactory;
            _httpContext = httpContext;
        }

        /// <summary>
        /// Heuristically guesses if there's a Mercurial repository at the repositoryPath
        /// </summary>
        public virtual bool IsHgRepository
        {
            get
            {
                string hgRepoFiles = Path.Combine(_environment.RepositoryPath, ".hg");
                return Directory.Exists(hgRepoFiles) && 
                       Directory.EnumerateFiles(hgRepoFiles).Any();
            }
        }

        public virtual bool IsGitRepository
        {
            get
            {
                var gitExeRepository = new GitExeRepository(_environment, _settings, _traceFactory);
                return gitExeRepository.Exists;
            }
        }

        public virtual bool IsNoneRepository
        {
            get { return _settings.IsNoneRepository(); }
        }

        public virtual bool IsCustomGitRepository
        {
            get
            {
                var gitExeRepository = new GitExeRepository(_environment, _settings, _traceFactory);
                gitExeRepository.SkipPostReceiveHookCheck = true;
                return gitExeRepository.Exists;
            }
        }

        public IRepository EnsureRepository(RepositoryType repositoryType)
        {
            IRepository repository;
            if (repositoryType == RepositoryType.None)
            {
                if (IsGitRepository)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_MismatchRepository, repositoryType, RepositoryType.Git, _environment.RepositoryPath));
                }
                if (IsHgRepository)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_MismatchRepository, repositoryType, RepositoryType.Mercurial, _environment.RepositoryPath));
                }
                repository = new NoneRepository(_environment, _traceFactory, _httpContext);
            }
            else if (repositoryType == RepositoryType.Mercurial)
            {
                if (IsGitRepository)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_MismatchRepository, repositoryType, RepositoryType.Git, _environment.RepositoryPath));
                }
                if (IsNoneRepository)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_MismatchRepository, repositoryType, RepositoryType.None, _environment.RepositoryPath));
                }
                FileSystemHelpers.EnsureDirectory(_environment.RepositoryPath);
                repository = new HgRepository(_environment.RepositoryPath, _environment.SiteRootPath, _settings, _traceFactory);
            }
            else 
            {
                if (IsHgRepository)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_MismatchRepository, repositoryType, RepositoryType.Mercurial, _environment.RepositoryPath));
                }
                if (IsNoneRepository)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_MismatchRepository, repositoryType, RepositoryType.None, _environment.RepositoryPath));
                }
                repository = new GitExeRepository(_environment, _settings, _traceFactory);
            }

            if (!repository.Exists)
            {
                repository.Initialize();
            }
            return repository;
        }

        public IRepository GetRepository()
        {
            ITracer tracer = _traceFactory.GetTracer();
            if (_settings.IsNoneRepository())
            {
                tracer.Trace("Assuming none repository at {0}", _environment.RepositoryPath);
                return new NoneRepository(_environment, _traceFactory, _httpContext);
            }
            else if (IsGitRepository)
            {
                tracer.Trace("Assuming git repository at {0}", _environment.RepositoryPath);
                return new GitExeRepository(_environment, _settings, _traceFactory);
            } 
            else if (IsHgRepository)
            {
                tracer.Trace("Found mercurial repository at {0}", _environment.RepositoryPath);
                return new HgRepository(_environment.RepositoryPath, _environment.SiteRootPath, _settings, _traceFactory);
            }
            return null;
        }

        public IRepository GetCustomRepository()
        {
            ITracer tracer = _traceFactory.GetTracer();
            if (IsCustomGitRepository)
            {
                tracer.Trace("Assuming custom git repository at {0}", _environment.RepositoryPath);
                var ret = new GitExeRepository(_environment, _settings, _traceFactory);
                ret.SkipPostReceiveHookCheck = true;
                return ret;
            }
            return null;
        }
    }
}
