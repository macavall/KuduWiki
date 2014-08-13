﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using Newtonsoft.Json;

namespace Kudu.Core.Diagnostics
{
    [DebuggerDisplay("{Id} {Name}")]
    public class ProcessInfo
    {
        [JsonProperty(PropertyName = "id")]
        public int Id { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "href", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Uri Href { get; set; }

        [JsonProperty(PropertyName = "minidump", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Uri MiniDump { get; set; }

        [JsonProperty(PropertyName = "gcdump", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Uri GCDump { get; set; }

        [JsonProperty(PropertyName = "parent", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Uri Parent { get; set; }

        [JsonProperty(PropertyName = "children", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public IEnumerable<Uri> Children { get; set; }

        [JsonProperty(PropertyName = "threads", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public IEnumerable<ProcessThreadInfo> Threads { get; set; }

        [JsonProperty(PropertyName = "open_file_handles", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public IEnumerable<string> OpenFileHandles { get; set; }

        [JsonProperty(PropertyName = "modules", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public IEnumerable<ProcessModuleInfo> Modules { get; set; }

        [JsonProperty(PropertyName = "file_name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string FileName { get; set; }

        [JsonProperty(PropertyName = "command_line", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string CommandLine { get; set; }

        //[JsonProperty(PropertyName = "arguments", DefaultValueHandling = DefaultValueHandling.Ignore)]
        //public string Arguments { get; set; }

        //[JsonProperty(PropertyName = "username", DefaultValueHandling = DefaultValueHandling.Ignore)]
        //public string UserName { get; set; }

        [JsonProperty(PropertyName = "handle_count", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int HandleCount { get; set; }

        [JsonProperty(PropertyName = "module_count", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int ModuleCount { get; set; }

        [JsonProperty(PropertyName = "thread_count", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int ThreadCount { get; set; }

        [JsonProperty(PropertyName = "start_time", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DateTime StartTime { get; set; }

        [JsonProperty(PropertyName = "total_cpu_time", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public TimeSpan TotalProcessorTime { get; set; }

        [JsonProperty(PropertyName = "user_cpu_time", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public TimeSpan UserProcessorTime { get; set; }

        [JsonProperty(PropertyName = "privileged_cpu_time", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public TimeSpan PrivilegedProcessorTime { get; set; }

        [JsonProperty(PropertyName = "working_set", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 WorkingSet64 { get; set; }

        [JsonProperty(PropertyName = "peak_working_set", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 PeakWorkingSet64 { get; set; }

        [JsonProperty(PropertyName = "private_memory", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 PrivateMemorySize64 { get; set; }

        [JsonProperty(PropertyName = "virtual_memory", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 VirtualMemorySize64 { get; set; }

        [JsonProperty(PropertyName = "peak_virtual_memory", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 PeakVirtualMemorySize64 { get; set; }

        [JsonProperty(PropertyName = "paged_system_memory", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 PagedSystemMemorySize64 { get; set; }

        [JsonProperty(PropertyName = "non_paged_system_memory", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 NonpagedSystemMemorySize64 { get; set; }

        [JsonProperty(PropertyName = "paged_memory", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 PagedMemorySize64 { get; set; }

        [JsonProperty(PropertyName = "peak_paged_memory", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 PeakPagedMemorySize64 { get; set; }

        [JsonProperty(PropertyName = "time_stamp", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DateTime TimeStamp { get; set; }

        [JsonProperty(PropertyName = "environment_variables", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Dictionary<string, string> EnvironmentVariables { get; set; }
    }
}