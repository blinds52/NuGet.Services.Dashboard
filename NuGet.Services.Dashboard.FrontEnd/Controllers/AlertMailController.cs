using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NuGetDashboard.Models;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using System.Configuration;
using System.IO;
using System.Web.Script.Serialization;
using NuGet.Services.Dashboard.Common;
using NuGetDashboard.Utilities;

namespace NuGetDashboard.Controllers
{
    public class AlertMailController : Controller
    {
        //
        // GET: /AlertMail/

        public ActionResult AlertMail_Details()
        {
            List<AlertMailViewModel> ListofAlertMail = new List<AlertMailViewModel>();
            List<AlertEntry> data;
            List<AlertEntry> data_action;
            string filename = string.Format("{0:yyyy-MM/}", DateTime.Now);
            string connectionString = ConfigurationManager.AppSettings["StorageConnection"];

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(MvcApplication.StorageContainer);
            CloudBlobDirectory blobdir = container.GetDirectoryReference(filename);

            if (blobdir.ListBlobs().Count() > 0)
            {


                foreach (CloudBlockBlob blob in blobdir.ListBlobs())
                {
                    string content;
                    string action;
                    using (var memoryStream = new MemoryStream())
                    {
                        blob.DownloadToStream(memoryStream);
                        content = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
                    }

                    string blobName_action = string.Format("{0:yyyy-MM}_action/{1}_action", DateTime.Now, blob.Name.Substring(filename.Count()));
                    CloudBlockBlob action_blob = container.GetBlockBlobReference(blobName_action);

                    using (var memoryStream = new MemoryStream())
                    {
                        action_blob.DownloadToStream(memoryStream);
                        action = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
                    }

                    if (content != null)
                    {
                        data = new JavaScriptSerializer().Deserialize<List<AlertEntry>>(content);
                        data_action = new JavaScriptSerializer().Deserialize<List<AlertEntry>>(action);

                        string lastAction;
                        if (data_action.Count == 0) lastAction = "none";
                        else lastAction = data_action[data_action.Count - 1].message;
                        string name = blob.Name.ToString().Replace("_", " ").Substring(filename.Count());
                        int index = name.IndexOf("@");
                        string subject = name.Substring(0, index);
                        string component = name.Substring(index + 1);
                        if (data.Count != 0)
                            ListofAlertMail.Add(new AlertMailViewModel { AlertSubject = subject, count = data.Count, lastTime = data[data.Count - 1].time, Component = component, details_ID = blob.Name, lastAction = lastAction });
                    }
                }
            }

          return View("~/Views/AlertMail/AlertMail_Details.cshtml", ListofAlertMail);
        }

        public ActionResult Action_Log(string name)
        {
            string filename = string.Format("{0:yyyy-MM}_action/{1}_action", DateTime.Now, name.Replace(" ", "_"));
            var content = BlobStorageService.Load(filename);
            List<AlertEntry> action = new JavaScriptSerializer().Deserialize<List<AlertEntry>>(content);

            return View("~/Views/AlertMail/Action_Log.cshtml",action);
        }

        public ActionResult Record_Details(string name)
        {
            var content = BlobStorageService.Load(name);
            List<AlertEntry>  data = new JavaScriptSerializer().Deserialize<List<AlertEntry>>(content);
            return View("~/Views/AlertMail/Record_Details.cshtml",data);
        }

        [HttpGet]
        public ActionResult Action_Details(string name)
        {
            ActionViewModel data = new ActionViewModel();
            data.AlertSubject = name;
            return View("~/Views/AlertMail/Action_Details.cshtml",data);
        }

        [HttpPost]
        public ActionResult Action_Details(ActionViewModel data)
        {
            string name = string.Format("{0:yyyy-MM}_action/{1}_action", DateTime.Now, data.AlertSubject.Replace(" ","_"));
            var content = BlobStorageService.Load(name);
            List<AlertEntry> action = new JavaScriptSerializer().Deserialize<List<AlertEntry>>(content);
            action.Add(new AlertEntry { time = DateTime.Now, message = data.Action});
            string json = new JavaScriptSerializer().Serialize(action);
            BlobStorageService.CreateBlob(name, "application/json", BlobStorageService.ToStream(json));

            return RedirectToAction("AlertMail_Details");
        }

    }
}
