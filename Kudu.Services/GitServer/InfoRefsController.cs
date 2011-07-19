#region License

// Copyright 2010 Jeremy Skinner (http://www.jeremyskinner.co.uk)
//  
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
// http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
// 
// The latest version of this file can be found at http://github.com/JeremySkinner/git-dot-aspx

// This file was modified from the one found in git-dot-aspx

#endregion

namespace Kudu.Services.GitServer {
    using System;
    using System.IO;
    using System.Web.Mvc;
    using System.Web.SessionState;
    using Kudu.Services.Authorization;

    // Handles /project/info/refs
    [SessionState(SessionStateBehavior.Disabled)]
    [BasicAuthorize]
    public class InfoRefsController : Controller {
        private readonly Repository _repository;

        public InfoRefsController(Repository repository) {
            _repository = repository;
        }

        public ActionResult Execute(string service) {
            service = GetServiceType(service);
            bool isUsingSmartProtocol = service != null;

            // Service has been specified - we're working with the smart protocol
            if (isUsingSmartProtocol) {
                return SmartInfoRefs(service);
            }

            // working with the dumb protocol.
            return DumbInfoRefs();
        }

        private ActionResult SmartInfoRefs(string service) {
            Response.ContentType = "application/x-git-{0}-advertisement".With(service);
            Response.WriteNoCache();

            // Explicitly set the charset to empty string 
            // We do this as certain git clients (jgit) require it to be empty.
            // If we don't set it, then it defaults to utf-8, which breaks jgit's logic for detecting smart http
            Response.Charset = "";
 
            Response.PktWrite("# service=git-{0}\n", service);
            Response.PktFlush();

            if (service == "upload-pack") {
                _repository.AdvertiseUploadPack(Response.OutputStream);
            }

            else if (service == "receive-pack") {
                _repository.AdvertiseReceivePack(Response.OutputStream);
            }

            return new EmptyResult();
        }

        private ActionResult DumbInfoRefs() {
            Response.WriteNoCache();

            Response.ContentType = "text/plain; charset=utf-8";
           
            _repository.UpdateServerInfo();
            string infoRefsPath = Path.Combine(_repository.GitDirectory(), "info/refs");
            Response.WriteFile(infoRefsPath);
            return new EmptyResult();
        }

        protected string GetServiceType(string service) {
            if (String.IsNullOrWhiteSpace(service)) {
                return null;
            }

            return service.Replace("git-", "");
        }
    }
}
