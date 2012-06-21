﻿using System.Collections.Generic;

namespace Kudu.SiteManagement
{
    public interface ISiteManager
    {
        IEnumerable<string> GetSites();
        Site GetSite(string applicationName);
        Site CreateSite(string applicationName);
        void DeleteSite(string applicationName);
        bool TryCreateDeveloperSite(string applicationName, out string siteUrl);
        void SetDeveloperSiteWebRoot(string applicationName, string siteRoot);
    }
}
