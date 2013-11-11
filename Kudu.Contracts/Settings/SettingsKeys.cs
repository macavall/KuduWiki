﻿namespace Kudu.Contracts.Settings
{
    public static class SettingsKeys
    {
        /// <remarks>
        /// Legacy value that is superseded by DeploymentBranch
        /// </remarks>
        internal const string Branch = "branch";

        public const string DeploymentBranch = "deployment_branch";
        public const string BuildArgs = "SCM_BUILD_ARGS";
        public const string ScriptGeneratorArgs = "SCM_SCRIPT_GENERATOR_ARGS";
        public const string TraceLevel = "SCM_TRACE_LEVEL";
        public const string CommandIdleTimeout = "SCM_COMMAND_IDLE_TIMEOUT";
        public const string LogStreamTimeout = "SCM_LOGSTREAM_TIMEOUT";
        public const string GitUsername = "SCM_GIT_USERNAME";
        public const string GitEmail = "SCM_GIT_EMAIL";
        public const string ScmType = "ScmType";
        public const string UseShallowClone = "SCM_USE_SHALLOW_CLONE";
        public const string Command = "COMMAND";
        public const string Project = "PROJECT";
        public const string WorkerCommand = "WORKER_COMMAND";
        public const string TargetPath = "SCM_TARGET_PATH";
        public const string RepositoryPath = "SCM_REPOSITORY_PATH";
        public const string NoRepository = "SCM_NO_REPOSITORY";
        public const string WebSiteComputeMode = "WEBSITE_COMPUTE_MODE";
        public const string WebSiteSiteMode = "WEBSITE_SITE_MODE";
        public const string JobsInterval = "SCM_JOBS_INTERVAL";
        public const string JobsIdleTimeoutInSeconds = "SCM_JOBS_IDLE_TIMEOUT";
    }
}
