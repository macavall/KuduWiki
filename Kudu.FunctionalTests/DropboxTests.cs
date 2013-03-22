﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Kudu.Client;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Dropbox;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.Services;
using Kudu.TestHarness;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class DropboxTests
    {
        static Random _random = new Random(unchecked((int)DateTime.Now.Ticks));

        [Fact]
        public void TestDropboxBasic()
        {
            OAuthInfo oauth = GetOAuthInfo();
            if (oauth == null)
            {
                // only run in private kudu
                return;
            }

            AccountInfo account = GetAccountInfo(oauth);
            DropboxDeployInfo deploy = GetDeployInfo("/BasicTest", oauth, account);

            string appName = "DropboxTest";
            ApplicationManager.Run(appName, appManager =>
            {
                HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials);
                client.PostAsJsonAsync("deploy?scmType=Dropbox", deploy).Result.EnsureSuccessful();

                KuduAssert.VerifyUrl(appManager.SiteUrl + "/default.html", "Hello Default!");
                KuduAssert.VerifyUrl(appManager.SiteUrl + "/temp/temp.html", "Hello Temp!");
                KuduAssert.VerifyUrl(appManager.SiteUrl + "/New Folder/New File.html", "Hello New File!");
            });
        }

        [Fact]
        public void TestDropboxSpecialChars()
        {
            OAuthInfo oauth = GetOAuthInfo();
            if (oauth == null)
            {
                // only run in private kudu
                return;
            }

            AccountInfo account = GetAccountInfo(oauth);
            DropboxDeployInfo deploy = GetDeployInfo("/SpecialCharsTest", oauth, account);

            string appName = "SpecialCharsTest";
            ApplicationManager.Run(appName, appManager =>
            {
                HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials);
                client.PostAsJsonAsync("deploy?scmType=Dropbox", deploy).Result.EnsureSuccessful();
            });
        }

        [Fact]
        public void TestDropboxSpecialFolderChars()
        {
            OAuthInfo oauth = GetOAuthInfo();
            if (oauth == null)
            {
                // only run in private kudu
                return;
            }

            AccountInfo account = GetAccountInfo(oauth);
            DropboxDeployInfo deploy = GetDeployInfo("/!@#$faiztest$#!@", oauth, account);

            string appName = "SpecialCharsTest";
            ApplicationManager.Run(appName, appManager =>
            {
                HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials);
                client.PostAsJsonAsync("deploy?scmType=Dropbox", deploy).Result.EnsureSuccessful();
            });
        }

        [Fact]
        public void TestDropboxRateLimiter()
        {
            // Set the limit to 60/sec, let it run for 5s
            // Expected count around 300 give or take.
            var rateLimiter = new DropboxHelper.RateLimiter(60, TimeSpan.FromSeconds(1));
            var start = DateTime.Now;
            int total = 0;
            while (DateTime.Now - start <= TimeSpan.FromSeconds(5))
            {
                rateLimiter.Throtte();
                ++total;
            }

            TestTracer.Trace("total = {0}", total);

            // For robustness, allow wider range
            Assert.True(total >= 240, total + " should be >= 240");
            Assert.True(total <= 360, total + " should be <= 360");
        }

        [Fact]
        public void TestDropboxConcurrent()
        {
            OAuthInfo oauth = GetOAuthInfo();
            if (oauth == null)
            {
                // only run in private kudu
                return;
            }

            AccountInfo account = GetAccountInfo(oauth);
            DropboxDeployInfo deploy = GetDeployInfo("/BasicTest", oauth, account);

            string appName = "DropboxTest";
            ApplicationManager.Run(appName, appManager =>
            {
                HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials);
                var tasks = new Task<HttpResponseMessage>[]
                {
                    client.PostAsJsonAsync("deploy", deploy),
                    client.PostAsJsonAsync("deploy", deploy)
                };

                Task.WaitAll(tasks);

                var success = tasks[0].Result.IsSuccessStatusCode ? tasks[0].Result : tasks[1].Result;
                var failure = !Object.ReferenceEquals(success, tasks[0].Result) ? tasks[0].Result : tasks[1].Result;

                success.EnsureSuccessful();
                Assert.Equal(HttpStatusCode.Conflict, failure.StatusCode);

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("Dropbox", results[0].Deployer);
            });
        }

        private OAuthInfo GetOAuthInfo()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (var reader = new JsonTextReader(new StreamReader(assembly.GetManifestResourceStream("Kudu.FunctionalTests.dropbox.oauth.json"))))
            {
                JsonSerializer serializer = new JsonSerializer();
                return serializer.Deserialize<OAuthInfo>(reader);
            }
        }

        private AccountInfo GetAccountInfo(OAuthInfo oauth)
        {
            var uri = new Uri("https://api.dropbox.com/1/account/info");
            var client = GetDropboxClient(HttpMethod.Get, uri, oauth);
            var response = client.GetAsync(uri.PathAndQuery).Result.EnsureSuccessful();
            using (var reader = new JsonTextReader(new StreamReader(response.Content.ReadAsStreamAsync().Result)))
            {
                JsonSerializer serializer = new JsonSerializer();
                return serializer.Deserialize<AccountInfo>(reader);
            }
        }

        private DeltaInfo GetDeltaInfo(OAuthInfo oauth, string cursor = null)
        {
            Uri uri;
            HttpClient client;
            if (!String.IsNullOrEmpty(cursor))
            {
                uri = new Uri("https://api.dropbox.com/1/delta/?cursor=" + cursor);
                client = GetDropboxClient(HttpMethod.Post, uri, oauth, new KeyValuePair<string, string>("cursor", cursor));
            }
            else
            {
                uri = new Uri("https://api.dropbox.com/1/delta");
                client = GetDropboxClient(HttpMethod.Post, uri, oauth);
            }

            var response = client.PostAsync(uri.PathAndQuery, null).Result.EnsureSuccessful();
            using (var reader = new JsonTextReader(new StreamReader(response.Content.ReadAsStreamAsync().Result)))
            {
                JsonSerializer serializer = new JsonSerializer();
                return new DeltaInfo(serializer.Deserialize<JObject>(reader));
            }
        }

        private DropboxDeployInfo GetDeployInfo(string path, OAuthInfo oauth, AccountInfo account, string cursor = null)
        {
            List<DropboxDeltaInfo> deltas = new List<DropboxDeltaInfo>();
            string timeStamp = GetUtcTimeStamp();
            string oldCursor = cursor;
            string newCursor = "";
            while (true)
            {
                DeltaInfo delta = GetDeltaInfo(oauth, cursor);
                newCursor = delta.cursor;
                if (newCursor == oldCursor)
                {
                    break;
                }

                foreach (EntryInfo info in delta.entries)
                {
                    DropboxDeltaInfo item = new DropboxDeltaInfo();

                    if (!info.metadata.path.StartsWith(path))
                    {
                        continue;
                    }

                    if (info.metadata == null || info.metadata.is_deleted || string.IsNullOrEmpty(info.metadata.path))
                    {
                        item.Path = info.path;
                        item.IsDeleted = true;
                    }
                    else
                    {
                        item.Path = info.metadata.path;
                        item.IsDirectory = info.metadata.is_dir;
                        if (!item.IsDirectory)
                        {
                            item.Modified = info.metadata.modified;
                            item.Nonce = GetNonce();
                            item.Signature = GetSignature(oauth, info.path, timeStamp, item.Nonce);
                        }
                    }

                    deltas.Add(item);
                }

                if (!delta.has_more)
                {
                    break;
                }

                cursor = newCursor;
            }

            if (deltas.Count == 0)
            {
                throw new InvalidOperationException("the repo is up-to-date.");
            }

            return new DropboxDeployInfo
            {
                TimeStamp = timeStamp,
                Token = oauth.Token,
                ConsumerKey = oauth.ConsumerKey,
                OAuthVersion = "1.0",
                SignatureMethod = "HMAC-SHA1",
                OldCursor = oldCursor,
                NewCursor = newCursor,
                Path = path,
                UserName = account.display_name,
                Email = account.email,
                Deltas = deltas
            };
        }

        private HttpClient GetDropboxClient(HttpMethod method, Uri uri, OAuthInfo oauth, params KeyValuePair<string, string>[] query)
        {
            var parameters = new Dictionary<string, string>
            {
                { "oauth_consumer_key", oauth.ConsumerKey },
                { "oauth_signature_method", "HMAC-SHA1" },
                { "oauth_timestamp", GetUtcTimeStamp() },
                { "oauth_nonce", GetNonce() },
                { "oauth_version", "1.0" },
            };

            if (!string.IsNullOrEmpty(oauth.Token))
            {
                parameters["oauth_token"] = oauth.Token;
            }

            var pp = new Dictionary<string, string>(parameters);
            foreach (KeyValuePair<string, string> pair in query)
            {
                pp.Add(pair.Key, pair.Value);
            }

            var strb = new StringBuilder();
            foreach (var pair in pp.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (strb.Length != 0)
                {
                    strb.Append('&');
                }

                strb.AppendFormat("{0}={1}", pair.Key, pair.Value);
            }

            string data = string.Format(
                "{0}&{1}&{2}",
                method.ToString().ToUpperInvariant(),
                UrlEncode(uri.AbsoluteUri.Split('?')[0]),
                UrlEncode(strb.ToString()));

            string key = string.Format(
                "{0}&{1}",
                UrlEncode(oauth.ConsumerSecret),
                string.IsNullOrEmpty(oauth.TokenSecret) ? string.Empty : UrlEncode(oauth.TokenSecret));

            HMACSHA1 hmacSha1 = new HMACSHA1();
            hmacSha1.Key = Encoding.ASCII.GetBytes(key);
            byte[] hashBytes = hmacSha1.ComputeHash(Encoding.ASCII.GetBytes(data));

            parameters.Add("oauth_signature", Convert.ToBase64String(hashBytes));

            strb = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in parameters)
            {
                if (strb.Length != 0)
                {
                    strb.Append(',');
                }

                strb.AppendFormat("{0}=\"{1}\"", pair.Key, pair.Value);
            }

            var client = new HttpClient();
            client.BaseAddress = new Uri(string.Format("{0}://{1}", uri.Scheme, uri.Host));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", strb.ToString());
            return client;
        }

        private string GetSignature(OAuthInfo oauth, string path, string timeStamp, string nonce)
        {
            var strb = new StringBuilder();
            strb.AppendFormat("{0}={1}", "oauth_consumer_key", oauth.ConsumerKey);
            strb.AppendFormat("&{0}={1}", "oauth_nonce", nonce);
            strb.AppendFormat("&{0}={1}", "oauth_signature_method", "HMAC-SHA1");
            strb.AppendFormat("&{0}={1}", "oauth_timestamp", timeStamp);
            strb.AppendFormat("&{0}={1}", "oauth_token", oauth.Token);
            strb.AppendFormat("&{0}={1}", "oauth_version", "1.0");

            string data = String.Format("{0}&{1}&{2}",
                "GET",
                UrlEncode("https://api-content.dropbox.com/1/files/sandbox" + DropboxPathEncode(path.ToLower())),
                UrlEncode(strb.ToString()));

            var key = String.Format("{0}&{1}",
                UrlEncode(oauth.ConsumerSecret),
                UrlEncode(oauth.TokenSecret));

            HMACSHA1 hmacSha1 = new HMACSHA1();
            hmacSha1.Key = Encoding.ASCII.GetBytes(key);
            byte[] hashBytes = hmacSha1.ComputeHash(Encoding.ASCII.GetBytes(data));
            return Convert.ToBase64String(hashBytes);
        }

        private static string DropboxPathEncode(string path)
        {
            const string DropboxPathUnreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_./";

            StringBuilder result = new StringBuilder();
            foreach (char symbol in path)
            {
                if (DropboxPathUnreservedChars.IndexOf(symbol) != -1)
                {
                    result.Append(symbol);
                }
                else
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(new char[] { symbol });
                    foreach (byte b in bytes)
                    {
                        result.AppendFormat("%{0:X2}", b);
                    }
                }
            }
            return result.ToString();
        }

        private string UrlEncode(string str)
        {
            Regex reg = new Regex("%[a-f0-9]{2}");
            return reg.Replace(HttpUtility.UrlEncode(str), m => m.Value.ToUpperInvariant());
        }

        private string GetUtcTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds).ToString();
        }

        private string GetNonce()
        {
            const string unreserved = "-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz";

            var chars = new char[8];
            for (int i = 0; i < 8; ++i)
            {
                chars[i] = unreserved[_random.Next(unreserved.Length)];
            }
            return new String(chars);
        }

        public class OAuthInfo
        {
            public string ConsumerKey { get; set; }
            public string ConsumerSecret { get; set; }
            public string Token { get; set; }
            public string TokenSecret { get; set; }
        }

        public class AccountInfo
        {
            public string display_name { get; set; }
            public string email { get; set; }
        }

        public class DeltaInfo
        {
            public DeltaInfo(JObject json)
            {
                cursor = (string)json["cursor"];
                has_more = (bool)json["has_more"];
                entries = new List<EntryInfo>();
                foreach (JArray entry in json["entries"])
                {
                    entries.Add(new EntryInfo(entry));
                }
            }

            public string cursor { get; set; }
            public bool has_more { get; set; }
            public List<EntryInfo> entries { get; set; }
        }

        public class EntryInfo
        {
            public EntryInfo(JArray json)
            {
                path = (string)json[0];
                metadata = json[1] is JObject ? new Metadata((JObject)json[1]) : null;
            }

            public string path { get; set; }
            public Metadata metadata { get; set; }
        }

        public class Metadata
        {
            public Metadata(JObject json)
            {
                path = (string)json["path"];
                is_dir = json["is_dir"] == null ? false : (bool)json["is_dir"];
                is_deleted = json["is_deleted"] == null ? false : (bool)json["is_deleted"];
                if (!is_dir && !is_deleted)
                {
                    modified = (string)json["modified"];
                }
            }

            public string path { get; set; }
            public bool is_dir { get; set; }
            public bool is_deleted { get; set; }
            public string modified { get; set; }
        }
    }
}

