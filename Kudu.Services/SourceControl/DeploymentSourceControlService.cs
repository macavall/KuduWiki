﻿using System;
using System.Json;
using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Hg;

namespace Kudu.Services.SourceControl
{
    [ServiceContract]
    public class DeploymentSourceControlService
    {
        private readonly IRepositoryManager _repositoryManager;
        private readonly IHgServer _server;

        public DeploymentSourceControlService(IRepositoryManager repositoryManager,
                                       IHgServer server)
        {
            _repositoryManager = repositoryManager;
            _server = server;
        }

        [WebInvoke]
        public void Create(JsonObject input)
        {
            _repositoryManager.CreateRepository((RepositoryType)Enum.Parse(typeof(RepositoryType), (string)input["type"]));
        }

        [WebInvoke]
        public void Delete()
        {
            // Stop the server (will no-op if nothing is running)
            _server.Stop();
            _repositoryManager.Delete();
        }

        [WebGet(UriTemplate = "kind")]
        public RepositoryType GetRepositoryType()
        {
            return _repositoryManager.GetRepositoryType();
        }
    }
}
