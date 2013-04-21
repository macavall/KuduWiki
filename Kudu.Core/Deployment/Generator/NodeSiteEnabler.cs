﻿using System;
using System.IO;
using System.IO.Abstractions;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    public static class NodeSiteEnabler
    {
        private static readonly string[] IisStartupFiles = new[]
        {
            "default.htm", "default.html", "default.asp", "index.htm", "index.html", "iisstart.htm", "default.aspx", "index.php"
        };

        private static readonly string[] NodeDetectionFiles = new[] { "server.js", "app.js", "package.json" };

        public static bool LooksLikeNode(IFileSystem fileSystem, string siteFolder)
        {
            bool potentiallyLooksLikeNode = false;

            // Check if any of the known start pages exist
            foreach (var nodeDetectionFile in NodeDetectionFiles)
            {
                string fullPath = Path.Combine(siteFolder, nodeDetectionFile);
                if (fileSystem.File.Exists(fullPath))
                {
                    potentiallyLooksLikeNode = true;
                    break;
                }
            }

            if (potentiallyLooksLikeNode)
            {
                // Check if any of the known iis start pages exist
                // If so, then it is not a node.js web site
                foreach (var iisStartupFile in IisStartupFiles)
                {
                    string fullPath = Path.Combine(siteFolder, iisStartupFile);
                    if (fileSystem.File.Exists(fullPath))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }
    }
}
