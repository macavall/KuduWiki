﻿using System;

namespace Kudu.Client.Infrastructure
{
    public static class UrlUtility
    {
        public static string EnsureTrailingSlash(string url)
        {
            UriBuilder address = new UriBuilder(url);
            if (!address.Path.EndsWith("/"))
            {
                address.Path += "/";
            }
            return address.Uri.AbsoluteUri;
        }
    }
}
