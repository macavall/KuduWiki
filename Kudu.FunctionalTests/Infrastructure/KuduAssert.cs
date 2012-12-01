﻿using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Kudu.Client;
using Kudu.Client.Infrastructure;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests.Infrastructure
{
    public static class KuduAssert
    {
        public static T ThrowsUnwrapped<T>(Action action) where T : Exception
        {
            var ex = Assert.Throws<AggregateException>(() => action());
            var baseEx = ex.GetBaseException();
            Assert.IsAssignableFrom<T>(baseEx);
            return (T)baseEx;
        }

        public static void VerifyUrl(Uri url, ICredentials cred, params string[] contents)
        {
            VerifyUrl(url.ToString(), cred, contents);
        }

        public static void VerifyUrl(string url, ICredentials cred, params string[] contents)
        {
            HttpClient client = HttpClientHelper.CreateClient(url, cred);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Kudu-Test", "1.0"));
            var response = client.GetAsync(url).Result.EnsureSuccessful();

            if (contents.Length > 0)
            {
                var responseBody = response.Content.ReadAsStringAsync().Result;
                foreach (var content in contents)
                {
                    Assert.Contains(content, responseBody, StringComparison.Ordinal);
                }
            }
        }

        public static void VerifyUrl(string url, string content = null, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Kudu-Test", "1.0"));
            var response = client.GetAsync(url).Result;
            string responseBody = response.Content.ReadAsStringAsync().Result;

            Assert.True(statusCode == response.StatusCode,
                String.Format("For {0}, Expected Status Code: {1} Actual Status Code: {2}. \r\n Response: {3}", url, statusCode, response.StatusCode, responseBody));

            if (content != null)
            {
                Assert.Contains(content, responseBody, StringComparison.Ordinal);
            }
        }

        public static void VerifyLogOutput(ApplicationManager appManager, string id, params string[] expectedMatches)
        {
            var entries = appManager.DeploymentManager.GetLogEntriesAsync(id).Result.ToList();
            Assert.True(entries.Count > 0);
            var allDetails = entries.Where(e => e.DetailsUrl != null)
                                    .SelectMany(e => appManager.DeploymentManager.GetLogEntryDetailsAsync(id, e.Id).Result).ToList();
            var allEntries = entries.Concat(allDetails).ToList();
            Assert.True(expectedMatches.All(match => allEntries.Any(e => e.Message.Contains(match))));
        }
    }
}
