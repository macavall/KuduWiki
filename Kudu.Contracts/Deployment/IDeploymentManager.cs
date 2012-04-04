﻿using System;
using System.Collections.Generic;

namespace Kudu.Core.Deployment
{
    public interface IDeploymentManager
    {
        event Action<DeployResult> StatusChanged;

        IEnumerable<DeployResult> GetResults();
        DeployResult GetResult(string id);
        IEnumerable<LogEntry> GetLogEntries(string id);
        IEnumerable<LogEntry> GetLogEntryDetails(string id, string logId);
        void Delete(string id);
        void Deploy(string id, bool clean);
        void Deploy();
        void CreateExistingDeployment(string id);
    }
}
