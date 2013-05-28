﻿using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Kudu.Client.Infrastructure;
using Kudu.Web.Infrastructure;
using Kudu.Web.Models;

namespace Kudu.Web.Controllers
{
    public class ApplicationController : Controller
    {
        private readonly IApplicationService _applicationService;
        private readonly KuduEnvironment _environment;
        private readonly ICredentialProvider _credentialProvider;

        public ApplicationController(IApplicationService applicationService,
                                     ICredentialProvider credentialProvider,
                                     KuduEnvironment environment)
        {
            _applicationService = applicationService;
            _credentialProvider = credentialProvider;
            _environment = environment;
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            ViewBag.showAdmingWarning = !_environment.IsAdmin && _environment.RunningAgainstLocalKuduService;
            base.OnActionExecuting(filterContext);
        }

        public ViewResult Index()
        {
            var applications = (from name in _applicationService.GetApplications()
                                orderby name
                                select name).ToList();

            return View(applications);
        }

        public async Task<ActionResult> Details(string slug)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null)
            {
                return HttpNotFound();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            var repositoryInfo = await application.GetRepositoryInfo(credentials);
            
            var appViewModel = new ApplicationViewModel(application);
            appViewModel.RepositoryInfo = repositoryInfo;

            ViewBag.slug = slug;
            ViewBag.tab = "settings";
            ViewBag.appName = appViewModel.Name;

            return View(appViewModel);
        }

        [HttpGet]
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Create(string name)
        {
            string slug = name.GenerateSlug();

            try
            {
                _applicationService.AddApplication(slug);

                return RedirectToAction("Details", new { slug });
            }
            catch (SiteExistsException)
            {
                ModelState.AddModelError("Name", "Site already exists");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }

            return View("Create");
        }

        [HttpPost]
        public ActionResult Delete(string slug)
        {
            if (_applicationService.DeleteApplication(slug))
            {
                return RedirectToAction("Index");
            }

            return HttpNotFound();
        }

        public async Task<ActionResult> Trace(string slug)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null)
            {
                return HttpNotFound();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            var document = await application.DownloadTrace(credentials);
            return View(document);
        }

        public ActionResult Develop(string slug)
        {
            return RedirectToAction("Details", new { slug });
        }
    }
}