﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using NuGetDashboard.Models;
using System.Configuration;
using System.IO;
using System.Web.Script.Serialization;
using NuGetDashboard.Utilities;

namespace NuGetDashboard.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.var = Session["currentEnvironmentName"];
            return View();
        }

        public ActionResult UpdateEnvironment(string envName)
        {            
            Session["currentEnvironmentName"] = envName;
            update();
            return RedirectToAction("Index");
        }

        [HttpGet]
        public JsonResult GetCurrentPacificTime()
        {
            //Returns the current pacific time. The dates in the charts are all in local time (pacific time) as of now. Hence displayed a clock with pacific time in the home page for reference.
            return Json(string.Format("{0:HH:mm:ss}",DateTimeUtility.GetPacificTimeNow()), JsonRequestBehavior.AllowGet);
        }

        private void update()
        {
            object envName = Session["currentEnvironmentName"];
            MvcApplication.DBConnectionString = ConfigurationManager.AppSettings[MvcApplication.DBConnectionStringPrefix + envName];
            MvcApplication.ElmahAccountCredentials = ConfigurationManager.AppSettings[MvcApplication.ElmahAccountCredentialsPrefix + envName];
            MvcApplication.StorageContainer = ConfigurationManager.AppSettings[MvcApplication.StorageContainerPrefix + envName];

        }
    
    }
}
