using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    public class RunBackgroundChecksForMetadataBlobs : StorageTask
    {
        public AlertThresholds thresholdValues;

        [Option("CatalogUrl", AltName = "cau")]
        public string CatalogUrl { get; set; }

        [Option("ResolverBlobsBaseUrl", AltName = "reu")]
        public string ResolverBlobsBaseUrl { get; set; }
        

        public override void ExecuteCommand()
        {
            thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));
            List<Tuple<string, string>> jobOutputs = new List<Tuple<string, string>> {
                Tuple.Create("LagBetweenCatalogAndResolverBlobs", CheckLagBetweenCatalogAndResolverBlobs())
            };
            JArray reportObject = ReportHelpers.GetJson(jobOutputs);
            ReportHelpers.CreateBlob(StorageAccount, "RunBackgroundChecksForMetadataBlobsReport.json", ContainerName, "application/json", ReportHelpers.ToStream(reportObject));
        }

        private string CheckLagBetweenCatalogAndResolverBlobs()
        {
            HttpClient client = new HttpClient();
            Task<string> cursorStringTask = client.GetStringAsync(new Uri(ResolverBlobsBaseUrl + "meta/cursor.json"));
            string cursorString = cursorStringTask.Result;  // Not async!
            JObject cursorJson = JObject.Parse(cursorString);

            DateTime cursorTimestamp = cursorJson["http://nuget.org/collector/resolver#cursor"]["@value"].ToObject<DateTime>();

            Task<string> catalogIndexStringTask = client.GetStringAsync(CatalogUrl + "catalog/index.json");
            string catalogIndexString = catalogIndexStringTask.Result;
            JObject catalogIndex = JObject.Parse(catalogIndexString);

            DateTime catalogTimestamp = catalogIndex["timeStamp"].ToObject<DateTime>();

            TimeSpan span = catalogTimestamp - cursorTimestamp;

            double delta = span.TotalMinutes;

            string outputMessage;

            outputMessage = string.Format("The lag from the package catalog to the resolver blob set is {0} minutes. Threshold is {1}. The resolver blob pipeline may be running too slowly.", delta, thresholdValues.CatalogToResolverBlobLagThresholdInMinutes);
            if (delta > thresholdValues.CatalogToResolverBlobLagThresholdInMinutes || delta < 0)
            {
                new SendAlertMailTask
                {
                    AlertSubject = "Alert: resolver blob generation lag",
                    Details = outputMessage,
                    AlertName = "Alert for CheckLagBetweenCatalogAndResolverBlobs",
                    Component = "LagBetweenCatalogAndResolverBlobs"
                }.ExecuteCommand();
            }

            Console.WriteLine(outputMessage);
            return outputMessage;
        }
    }
}
