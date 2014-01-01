﻿using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Kudu.Core.Deployment
{
    [DebuggerDisplay("{Id} {Status}")]
    [DataContract]
    public class DeployResult
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "status")]
        public DeployStatus Status { get; set; }

        [DataMember(Name = "status_text")]
        public string StatusText { get; set; }

        [DataMember(Name = "author_email")]
        public string AuthorEmail { get; set; }

        [DataMember(Name = "author")]
        public string Author { get; set; }

        [DataMember(Name = "deployer")]
        public string Deployer { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "progress")]
        public string Progress { get; set; }

        [DataMember(Name = "received_time")]
        public DateTime ReceivedTime { get; set; }

        [DataMember(Name = "start_time")]
        public DateTime StartTime { get; set; }

        [DataMember(Name = "end_time")]
        public DateTime? EndTime { get; set; }

        [DataMember(Name = "last_success_end_time")]
        public DateTime? LastSuccessEndTime { get; set; }

        [DataMember(Name = "complete")]
        public bool Complete { get; set; }

        [DataMember(Name = "active")]
        public bool Current { get; set; }

        [DataMember(Name = "is_temp")]
        public bool IsTemporary { get; set; }

        [DataMember(Name = "is_readonly")]
        public bool IsReadOnly { get; set; }

        [DataMember(Name = "url")]
        public Uri Url { get; set; }

        [DataMember(Name = "log_url")]
        public Uri LogUrl { get; set; }

        [DataMember(Name = "site_name")]
        public string SiteName { get; set; }
    }
}