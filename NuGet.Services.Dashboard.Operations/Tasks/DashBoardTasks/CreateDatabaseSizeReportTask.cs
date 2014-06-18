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
    [Command("CreateDatabaseSizeReportTask", "Creates database size report", AltName = "cdsrt")]
    public class CreateDatabaseSizeReportTask : StorageTask
    {
        private string SqlQueryForDbSize = @"SELECT CONVERT(int,SUM(reserved_page_count)*8.0/1024) FROM sys.dm_db_partition_stats";
        private string SqlQueryForEdition = @"SELECT  DATABASEPROPERTYEX (DB_NAME(), 'Edition') AS Edition";

        private string SqlQueryForMaxSize = @"SELECT (CONVERT(bigint, DATABASEPROPERTYEX (DB_NAME(), 'MaxSizeInBytes')) / (1024*1024)) AS MaxSizeInMB";


        //[Option("Connection string to the primary database", AltName = "pdb")]
        //public SqlConnectionStringBuilder PrimaryConnectionString { get; set; }

        [Option("Connection string to the legacy database", AltName = "ldb")]
        public SqlConnectionStringBuilder LegacyConnectionString { get; set; }

        [Option("Connection string to the warehouse database", AltName = "wdb")]
        public SqlConnectionStringBuilder WarehouseConnectionString { get; set; }



        public override void ExecuteCommand()
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            AlertThresholds thresholdValues = js.Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));
            int threshold = thresholdValues.DatabaseSizeWarningPercentThreshold;

            List<DatabaseSize> dbSizeDetails = new List<DatabaseSize>();
           // dbSizeDetails.Add(GetDataSize(PrimaryConnectionString.ConnectionString,threshold));
            dbSizeDetails.Add(GetDataSize(LegacyConnectionString.ConnectionString, threshold));
            dbSizeDetails.Add(GetDataSize(WarehouseConnectionString.ConnectionString, threshold));
          
            var json = js.Serialize(dbSizeDetails);
            ReportHelpers.CreateBlob(StorageAccount, "DBSize.json", ContainerName, "application/json",ReportHelpers.ToStream(json));
        }

        private DatabaseSize GetDataSize(string connectionString,int threshold)
        {
            
            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    int sizeInMb = dbExecutor.Query<int>(SqlQueryForDbSize).SingleOrDefault();
                    Int64 maxSizeInMb = dbExecutor.Query<Int64>(SqlQueryForMaxSize).SingleOrDefault();
                    var percentUsed = ((float)sizeInMb/maxSizeInMb)*100;                    
                    string edition = dbExecutor.Query<string>(SqlQueryForEdition).SingleOrDefault();
                    string dbName = Util.GetDbName(connectionString);

                    if (percentUsed > threshold)
                    {
                        new SendAlertMailTask
                        {
                            AlertSubject = string.Format("SQL Azure database size alert activated for {0}",dbName),
                            Details = string.Format("DB Size excced the threshold percent.Current Used % : {0}, Threshold % : {1}, Current Size in MB : {2}, Max Allowed in MB: {3}", percentUsed, threshold,sizeInMb,maxSizeInMb ),
                            AlertName = "SQL Azure DB alert for database size limit",
                            Component = string.Format("SQL Azure database-{0}",dbName)
                        }.ExecuteCommand();
                    }

                    return new DatabaseSize(dbName, sizeInMb, maxSizeInMb, edition);
                }
            }
        }

      
    }
}


