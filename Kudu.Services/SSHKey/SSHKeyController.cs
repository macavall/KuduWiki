﻿using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core.SSHKey;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Kudu.Services.SSHKey
{
    public class SSHKeyController : ApiController
    {
        private const string KeyParameterName = "key";
        private const int LockTimeoutSecs = 5;

        private readonly ITracer _tracer;
        private readonly ISSHKeyManager _sshKeyManager;
        private readonly IOperationLock _sshKeyLock;

        public SSHKeyController(ITracer tracer, ISSHKeyManager sshKeyManager, IOperationLock sshKeyLock)
        {
            _tracer = tracer;
            _sshKeyManager = sshKeyManager;
            _sshKeyLock = sshKeyLock;
        }
        
        /// <summary>
        /// Set the private key. The supported key format is privacy enhanced mail (PEM)
        /// </summary>
        [HttpPut]
        [SuppressMessage("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly", Justification = "By design")]
        public void SetPrivateKey()
        {
            string key;
            if (IsContentType("application/json"))
            {
                JObject result = GetJsonContent();
                key = result == null ? null : result.Value<string>(KeyParameterName);
            }
            else
            {
                // any other content-type assuming the content is key
                // curl http://server/sshkey -X PUT --upload-file /c/temp/id_rsa
                key = Request.Content.ReadAsStringAsync().Result;
            }

            if (String.IsNullOrEmpty(key))
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, new ArgumentNullException(KeyParameterName)));
            }

            using (_tracer.Step("SSHKeyController.SetPrivateKey"))
            {
                // This is not what we want
                bool success = _sshKeyLock.TryLockOperation(() =>
                {
                    try
                    {
                        _sshKeyManager.SetPrivateKey(key);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex));
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Conflict, ex));
                    }
                }, TimeSpan.FromSeconds(LockTimeoutSecs));

                if (!success)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(
                        HttpStatusCode.Conflict,
                        String.Format(CultureInfo.CurrentCulture, Resources.Error_OperationLockTimeout, LockTimeoutSecs)));
                }
            }
        }

        [HttpPost]
        public string GetPublicKey(bool forceCreate = false)
        {
            using (_tracer.Step("SSHKeyController.GetPublicKey"))
            {
                string key = null;
                bool success = _sshKeyLock.TryLockOperation(() =>
                {
                    try
                    {
                        key = _sshKeyManager.GetOrCreateKey(forceCreate);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Conflict, ex));
                    }
                }, TimeSpan.FromSeconds(LockTimeoutSecs));

                if (!success)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(
                        HttpStatusCode.Conflict,
                        String.Format(CultureInfo.CurrentCulture, Resources.Error_OperationLockTimeout, LockTimeoutSecs)));
                }

                return key;
            }
        }

        private bool IsContentType(string mediaType)
        {
            var contentType = Request.Content.Headers.ContentType;
            if (contentType == null)
            {
                return false;
            }

            return contentType.MediaType != null &&
                contentType.MediaType.StartsWith(mediaType, StringComparison.OrdinalIgnoreCase);
        }

        private JObject GetJsonContent()
        {
            try
            {
                return Request.Content.ReadAsAsync<JObject>().Result;
            }
            catch
            {
                // We're going to return null here since we don't want to force a breaking change
                // on the client side. If the incoming request isn't application/json, we want this 
                // to return null.
                return null;
            }
        }
    }
}
