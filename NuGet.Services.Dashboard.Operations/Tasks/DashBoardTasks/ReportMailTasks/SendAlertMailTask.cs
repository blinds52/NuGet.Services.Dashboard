﻿using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGetGallery.Operations.Common;
using AnglicanGeek.DbExecutor;
using System;
using System.Net;
using System.Web.Script.Serialization;
using System.Net.Mail;
using System.Web.Helpers;
using System.Web.UI;
using System.Net.Mime;
using System.Configuration;
using NuGet.Services.Dashboard.Common;

namespace NuGetGallery.Operations
{
    [Command("SendAlertMailTask", "Creates a pager duty incident or sends an alert email based on configuration", AltName = "samt")]
    public class SendAlertMailTask : OpsTask 
    {
        [Option("ErrorDetails", AltName = "e")]
        public string Details { get; set; }

        [Option("AlertSubject", AltName = "s")]
        public string AlertSubject { get; set; }

        [Option("AlertName", AltName = "n")]
        public string AlertName { get; set; }
        
        [Option("Component", AltName = "c")]
        public string Component { get; set; }

        [Option("Level", AltName = "l")]
        public string Level { get; set; }


        public override void ExecuteCommand()
        {
            loggingMail();
            //Either create an incident or send mail based on the current settings.
            if (ConfigurationManager.AppSettings["UsePagerDuty"].Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                CreateIncident();  
            }
            else
            {
                if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["SmtpUserName"]) && !string.IsNullOrEmpty(ConfigurationManager.AppSettings["SmtpPassword"]))
                {
                    SendEmail();
                }
            }          
        }

        private void loggingMail()
        {
            List<AlertEntry> alertlist = new List<AlertEntry>();
            string blobName = string.Format("{0:yyyy-MM}/{1}@{2}", DateTime.Now, AlertSubject.Replace(" ", "_"),Component.Replace(" ","_"));
            string blobName_action = string.Format("{0:yyyy-MM}_action/{1}@{2}_action", DateTime.Now, AlertSubject.Replace(" ", "_"), Component.Replace(" ", "_"));
            string _connectionString = ConfigurationManager.AppSettings["AlertStorageConnection"];
            string containerName = ConfigurationManager.AppSettings["AlertContainer"];
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(blobName);
            CloudBlockBlob action_blob = container.GetBlockBlobReference(blobName_action);

            if (!action_blob.Exists())
            {
                List<AlertEntry> actionlist = new List<AlertEntry>();
                var content = new JavaScriptSerializer().Serialize(actionlist);
                ReportHelpers.CreateBlob(storageAccount, blobName_action, containerName, "application/json", ReportHelpers.ToStream(content));
            }

            if (blob.Exists())
            {
                string json = ReportHelpers.Load(storageAccount, blobName, containerName);
                alertlist = new JavaScriptSerializer().Deserialize<List<AlertEntry>>(json);
            }

            alertlist.Add(new AlertEntry { time = DateTime.Now, message = Details });

            var key = new JavaScriptSerializer().Serialize(alertlist);
            ReportHelpers.CreateBlob(storageAccount, blobName, containerName, "application/json", ReportHelpers.ToStream(key));
        }

        private void CreateIncident()
        {
            WebClient client = new WebClient();
            client.Headers[HttpRequestHeader.Accept] = "application/json";
            client.Headers[HttpRequestHeader.ContentType] = "application/json";

            client.UploadStringCompleted += (object source, UploadStringCompletedEventArgs e) =>
            {
                if (e.Error != null || e.Cancelled)
                {
                    Console.WriteLine("Error" + e.Error);
                    Console.ReadKey();
                }
            };

            JavaScriptSerializer js = new JavaScriptSerializer();
            TriggerDetails triggerDetails = new TriggerDetails(Component, Details);
            var detailJson = js.Serialize(triggerDetails);

            Trigger trigger = new Trigger(ConfigurationManager.AppSettings["PagerDutyServiceKey"],AlertName,AlertSubject,detailJson);           
            var triggerJson = js.Serialize(trigger);
            client.UploadString(new Uri("https://events.pagerduty.com/generic/2010-04-15/create_event.json"), triggerJson); 
            
        }

        private void SendEmail()
        {
            SmtpClient sc = new SmtpClient("smtphost");
            NetworkCredential nc = new NetworkCredential(ConfigurationManager.AppSettings["SmtpUserName"], ConfigurationManager.AppSettings["SmtpPassword"]);
            sc.UseDefaultCredentials = true;
            sc.Credentials = nc;
            sc.Host = "outlook.office365.com";
            sc.EnableSsl = true;
            sc.Port = 587;
            //ServicePointManager.ServerCertificateValidationCallback = delegate(object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) { return true; };
            System.Net.Mail.MailMessage message = new System.Net.Mail.MailMessage();
            message.From = new MailAddress(ConfigurationManager.AppSettings["SmtpUserName"], "NuGet Gallery Live site monitor");
            message.To.Add(new MailAddress(ConfigurationManager.AppSettings["MailRecepientAddress"], ConfigurationManager.AppSettings["MailRecepientAddress"]));
            message.Subject = string.Format("[NuGet Gallery LiveSite Monitoring]: {0}", AlertSubject);
            message.IsBodyHtml = true;
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(@"<html><body>" + GetMailContent() + "</body></html>", new ContentType("text/html")));

            try
            {
                sc.Send(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(" Error in sending mail : {0}", ex.Message);
                Console.ReadKey();
            }
        }

        private string GetMailContent()
        {
            StreamReader sr = new StreamReader(@"ScriptsAndReferences\DashboardAlertMail.htm");
            string mailBody = sr.ReadToEnd();
            sr.Close();
            mailBody = mailBody.Replace("{AlertSubjectLine}", AlertSubject);
            mailBody = mailBody.Replace("{Level}", Level);
            mailBody = mailBody.Replace("{ComponentName}", Component);
            mailBody = mailBody.Replace("{Alert}", AlertName);
            mailBody = mailBody.Replace("{AlertDescription}", Details);
            mailBody = mailBody.Replace("{AlertTime}", DateTime.Now.ToString());
            
            return mailBody;          
        }      

    }
    /// <summary>
    /// Defines a "Trigger" event for PagerDuty.
    /// </summary>
    public class Trigger
    {
        public string service_key ;
        public string incident_key;
        public string event_type = "trigger";
        public string description;
        public object details;
        public string client;
        public string client_url;

        public Trigger()
        {

        }
        public Trigger(string serviceKey,string incidentKey,string description,object details)
        {
            this.service_key = serviceKey;
            this.incident_key = incidentKey;
            this.description = description;           
            this.details = details;
            this.event_type = "trigger";
            this.client = "NuGet Dashboard";
            this.client_url = "https://dashboard.nuget.org";
        }
    }
    /// <summary>
    /// Defines additional details for a trigger.
    /// </summary>
    public class TriggerDetails
    {
        public string component;       
        public string ErrorMessage;

        public TriggerDetails()
        {

        }
        public TriggerDetails(string componentName, string errorMessage)
        {
            this.component = componentName;           
            this.ErrorMessage = errorMessage;
        }
    }
}
