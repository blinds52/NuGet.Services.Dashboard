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
using NuGetGallery;
using NuGetGallery.Infrastructure;
using Elmah;
using NuGet.Services.Dashboard.Common;


namespace NuGetGallery.Operations
{
    [Command("CreateElmahErrorDetailedReportTask", "Creates detailed error report for last N hours from Elmah logs", AltName = "ceedrt")]
    public class CreateElmahErrorDetailedReportTask : StorageTask
    {
        [Option("LastNHours", AltName = "n")]
        public int LastNHours { get; set; }

        [Option("ElmahAccountCredentials", AltName = "ea")]
        public string ElmahAccountCredentials { get; set; }
             
        public override void ExecuteCommand()
        {         
            
            AlertThresholds thresholds = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));
            
            List<ElmahError> listOfErrors = new List<ElmahError>();
            RefreshElmahError RefreshExecute = new RefreshElmahError(StorageAccount, ContainerName, LastNHours, ElmahAccountCredentials);
           
            listOfErrors = RefreshExecute.ExecuteRefresh();

            foreach (ElmahError error in listOfErrors)
            {
                if (error.Severity == 0)
                {
                    if (error.Occurecnes > thresholds.ElmahCriticalErrorPerHourAlertErrorThreshold && LastNHours == 1)
                    {
                        new SendAlertMailTask
                        {
                            AlertSubject = string.Format("Error: Elmah Error Alert activated for {0}", error.Error),
                            Details = String.Format("Number of {0} exceeded Error threshold limit during the last hour.Threshold error count per hour : {1}, Events recorded in the last hour: {2}", error.Error, thresholds.ElmahCriticalErrorPerHourAlertErrorThreshold, error.Occurecnes.ToString()),
                            AlertName = string.Format("Error: Elmah Error Alert for {0}", error.Error),
                            Component = "Web Server",
                            Level = "Error"
                        }.ExecuteCommand();
                    }
                    else if (error.Occurecnes > thresholds.ElmahCriticalErrorPerHourAlertWarningThreshold && LastNHours == 1)
                    {
                        new SendAlertMailTask
                        {
                            AlertSubject = string.Format("Warning: Elmah Error Alert activated for {0}", error.Error),
                            Details = String.Format("Number of {0} exceeded Warning threshold limit during the last hour.Threshold error count per hour : {1}, Events recorded in the last hour: {2}", error.Error, thresholds.ElmahCriticalErrorPerHourAlertWarningThreshold, error.Occurecnes.ToString()),
                            AlertName = string.Format("Warning: Elmah Error Alert for {0}", error.Error),
                            Component = "Web Server",
                            Level = "Warning"
                        }.ExecuteCommand();
                    }
                }
            }

            var json = new JavaScriptSerializer().Serialize(listOfErrors);          
            ReportHelpers.CreateBlob(StorageAccount, "ElmahErrorsDetailed" + LastNHours.ToString() + "hours.json", ContainerName, "application/json", ReportHelpers.ToStream(json));
          
        }      
  

    }
}
