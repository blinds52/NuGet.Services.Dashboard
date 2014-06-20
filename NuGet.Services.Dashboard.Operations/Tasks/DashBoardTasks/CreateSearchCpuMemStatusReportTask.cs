﻿using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    [Command("CreateSearchCpuMemStatusReportTask", "Creates the report for CPU and Mem usage of search service.", AltName = "cscmsrt")]
    class CreateSearchCpuMemStatusReportTask : StorageTask
    {
        [Option("SearchEndPoint", AltName = "se")]
        public string SearchEndPoint { get; set; }

        [Option("SearchAdminUserName", AltName = "sa")]
        public string SearchAdminUserName { get; set; }

        [Option("SearchAdminkey", AltName = "sk")]
        public string SearchAdminKey { get; set; }

        public override void ExecuteCommand()
        {
            NetworkCredential nc = new NetworkCredential(SearchAdminUserName, SearchAdminKey);
            WebRequest request = WebRequest.Create(SearchEndPoint);
            AlertThresholds thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));
            request.Credentials = nc;
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());
                var process_info = objects["process"];
                double cpusecond = (double)process_info["cpuSeconds"];
                long memory = (long)process_info["virtualMemorySize"];
                int cpuUsage = 0;
                int memUsage = 0;

                if (cpuUsage > thresholdValues.SearchCpuPercentThreshold)
                {
                    new SendAlertMailTask
                    {
                        AlertSubject = "Search Service Alert activated for cpu usage",
                        Details = string.Format("Search service process cpu usage is above {0}% , it's {1}% ", thresholdValues.SearchCpuPercentThreshold.ToString(), cpuUsage.ToString()),
                        AlertName = "Alert for Serach CPU Usage",
                        Component = "SearchService"
                    }.ExecuteCommand();
                }

                if (memUsage > thresholdValues.SearchMemThresholdInGb*(1<<30))
                {
                    new SendAlertMailTask
                    {
                        AlertSubject = "Search Service Alert activated for memory usage",
                        Details = string.Format("Search service process memory usage is above {0}% GB, it's {1}% Byte ", thresholdValues.SearchMemThresholdInGb.ToString(), memUsage.ToString()),
                        AlertName = "Alert for Serach Memory Usage",
                        Component = "SearchService"
                    }.ExecuteCommand();
                }
                ReportHelpers.AppendDatatoBlob(StorageAccount, "SearchCpuUsage" + string.Format("{0:MMdd}", DateTime.Now) + "HourlyReport.json", new Tuple<string, string>(string.Format("{0:HH-mm}", DateTime.Now), cpusecond.ToString()), 24, ContainerName);
                ReportHelpers.AppendDatatoBlob(StorageAccount, "SearchMemUsage" + string.Format("{0:MMdd}", DateTime.Now) + "HourlyReport.json", new Tuple<string, string>(string.Format("{0:HH-mm}", DateTime.Now), memory.ToString()), 24, ContainerName);
            }
        }
    }
}
