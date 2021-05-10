﻿using Kudu.Contracts.Infrastructure;
using System;

namespace Kudu.Core.Helpers
{
    public static class EnvironmentHelper
    {
        public static string NormalizeBinPath(string binPath)
        {
            if (!string.IsNullOrWhiteSpace(binPath) && !OSDetector.IsOnWindows())
            {
                int binIdx = binPath.LastIndexOf("Bin", StringComparison.Ordinal);
                if (binIdx >= 0)
                {
                    string subStr = binPath.Substring(binIdx);
                    // make sure file path is end with ".....Bin" or "....Bin/"
                    if (subStr.Length < 5 && binPath.EndsWith(subStr, StringComparison.OrdinalIgnoreCase))
                    {
                        // real bin folder is lower case, but in mono, value is "Bin" instead of "bin"
                        binPath = binPath.Substring(0, binIdx) + subStr.ToLowerInvariant();
                    }
                }
            }

            return binPath;
        }

        // Is this a Windows Containers site?
        public static bool IsWindowsContainers()
        {
            string isolation = System.Environment.GetEnvironmentVariable("WEBSITE_ISOLATION");
            return isolation == "hyperv" || isolation == "process";
        }

        public static bool IsRunFromPackage()
        {
            string runFromPackage = System.Environment.GetEnvironmentVariable("WEBSITE_RUN_FROM_PACKAGE");
            return IsValidRunFromPackage(runFromPackage);
        }

        public static bool IsLCOW()
        {
            return
                System.Environment.GetEnvironmentVariable("WEBSITE_ISOLATION") == "hyperv" &&
                System.Environment.GetEnvironmentVariable("WEBSITE_OS") == "linux";
        }

        internal static bool IsValidRunFromPackage(string runFromPackageValue)
        {
            if (string.IsNullOrEmpty(runFromPackageValue))
            {
                return false;
            }

            return StringUtils.IsTrueLike(runFromPackageValue)
                || Uri.TryCreate(runFromPackageValue, UriKind.Absolute, out _);
        }
    }
}