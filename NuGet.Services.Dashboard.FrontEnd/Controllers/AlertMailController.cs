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
            string filename = string.Format("{0:yyyy-MM/}", DateTime.Now);
            string connectionString = ConfigurationManager.AppSettings["StorageConnection"];

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(MvcApplication.StorageContainer);
            CloudBlobDirectory blobdir = container.GetDirectoryReference(filename);

            foreach (CloudBlockBlob blob in blobdir.ListBlobs())
            {
                string content;
                using (var memoryStream = new MemoryStream())
                {
                    blob.DownloadToStream(memoryStream);
                    content = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
                }

                if (content != null)
                {
                    data = new JavaScriptSerializer().Deserialize<List<AlertEntry>>(content);
                    string name = blob.Name.ToString().Replace("_", " ").Substring(filename.Count());
                    int index = name.IndexOf("@");
                    string subject = name.Substring(0, index);
                    string component = name.Substring(index + 1);
                    if (data.Count != 0)
                        ListofAlertMail.Add(new AlertMailViewModel { AlertSubject = subject, count = data.Count, lastTime = data[data.Count-1].time, Component = component, details_ID = blob.Name });
                }
            }

          return View("~/Views/AlertMail/AlertMail_Details.cshtml", ListofAlertMail);
        }

        public ActionResult Record_Details(string name)
        {
            var content = BlobStorageService.Load(name);
            List<AlertEntry>  data = new JavaScriptSerializer().Deserialize<List<AlertEntry>>(content);
            return View("~/Views/AlertMail/Record_Details.cshtml",data);
        }

    }
}
