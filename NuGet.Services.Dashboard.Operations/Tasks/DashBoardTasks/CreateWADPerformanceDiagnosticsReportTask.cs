using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGetGallery.Operations.Common;
using AnglicanGeek.DbExecutor;
using System;
using System.Net;
using System.Web.Script.Serialization;
using NuGetGallery;
using NuGetGallery.Infrastructure;
using Microsoft.WindowsAzure.StorageClient;
using Microsoft.WindowsAzure;



namespace NuGetGallery.Operations
{
    [Command("CreatePerformaceCountOverviewReportTask", "Creates trending report for performance count", AltName = "cpcort")]
    public class CreateWADPerformanceDiagnosticsReportTask : StorageTask
    {
        

        [Option("roleInstance", AltName = "rn")]
        public string roleInstance { get; set; }
        
        [Option("privateKey", AltName = "pk")]
        public string privatekey { get; set; }
        
        [Option("storageName", AltName = "sn")]
        public string storageName { get; set; }

        [Option("DeploymentID", AltName = "di")]
        public string DeploymentID { get; set; }

        [Option("RoleName", AltName = "rn")]
        public string RoleName { get; set; }

        [Option("ContainerSpecifier", AltName = "cs")]
        public string ContainerSpecifier { get; set; }

        public override void ExecuteCommand()
        {
            int period = 60;
            QueryExecuter queryExecuter;
            try
            {
                queryExecuter = new QueryExecuter(storageName, privatekey);
            }
            catch 
            {
                return;
            }

            var data = queryExecuter.QueryPerformanceCounter(ContainerSpecifier, DeploymentID, RoleName, roleInstance,period);

            foreach (PerformanceData point in data)
            {
                ReportHelpers.AppendDatatoBlob(StorageAccount, ContainerSpecifier  + string.Format("{0:MMdd}", data[0].Timestamp) + "HourlyReport.json", new Tuple<string, string>(string.Format("{0:HH-mm}", point.Timestamp), point.CounterValue.ToString()), data.Count, ContainerName);
            }
        }



    }

    public class PerformanceData : TableServiceEntity, IComparable
    {
        Int64 _eventTickCount;

        public Int64 EventTickCount
        {
            get
            {
                return _eventTickCount;
            }
            set
            {
                _eventTickCount = value;
            }
        }
        public string DeploymentId { get; set; }
        public string Role { get; set; }
        public string RoleInstance { get; set; }
        public string CounterName { get; set; }
        public double CounterValue { get; set; }

        public override bool Equals(object o)
        {
            if (this == o) return true;
            if (o == null || (o as PerformanceData) == null) return false;

            PerformanceData that = (PerformanceData)o;

            if (_eventTickCount != that._eventTickCount) return false;
            if (CounterName != null ? !CounterName.Equals(that.CounterName) : that.CounterName != null) return false;
            if (DeploymentId != null ? !DeploymentId.Equals(that.DeploymentId) : that.DeploymentId != null) return false;
            if (Role != null ? !Role.Equals(that.Role) : that.Role != null) return false;
            if (RoleInstance != null ? !RoleInstance.Equals(that.RoleInstance) : that.RoleInstance != null) return false;

            return true;
        }

        public override int GetHashCode()
        {
            int result = DeploymentId != null ? DeploymentId.GetHashCode() : 0;
            result = 31 * result + (Role != null ? Role.GetHashCode() : 0);
            result = 31 * result + (RoleInstance != null ? RoleInstance.GetHashCode() : 0);
            result = 31 * result + (CounterName != null ? CounterName.GetHashCode() : 0);
            result = 31 * result + (int)(_eventTickCount ^ (_eventTickCount >> 32));
            return result;
        }

        public int CompareTo(object obj)
        {
            return _eventTickCount.CompareTo(((PerformanceData)obj)._eventTickCount);
        }
    }

    public class PerformanceDataContext : TableServiceContext
    {
        public PerformanceDataContext(string baseAddress, StorageCredentials credentials)
            : base(baseAddress, credentials)
        {
            ResolveType = ResolveEntityType;
        }

        public Type ResolveEntityType(string name)
        {
            var type = typeof(PerformanceData);
            return type;
        }

        public IQueryable<PerformanceData> PerfData
        {
            get { return this.CreateQuery<PerformanceData>("WADPerformanceCountersTable"); }
        }
    }

    public class QueryExecuter
    {
        /// <summary>
        /// Cloud storage account client
        /// </summary>
        private CloudStorageAccount accountStorage;

        /// <summary>
        /// Default Constructor - Use development storage emulator.
        /// </summary>
        public QueryExecuter()
        {
            accountStorage = CloudStorageAccount.DevelopmentStorageAccount;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="accountName">Azure storage name</param>
        /// <param name="privateKey">Azure storage private key</param>
        public QueryExecuter(string accountName, string privateKey)
        {
            accountStorage = CloudStorageAccount.Parse(String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", accountName, privateKey));
        }

        /// <summary>
        /// Retrive Performance counter data
        /// </summary>
        /// <param name="counterFullName">Counter specifier full name</param>
        /// <param name="deploymentid">Deployment id</param>
        /// <param name="roleName">Role name</param>
        /// <param name="roleInstanceName">Role instance name</param>
        /// <param name="startPeriod">Start sample date time</param>
        /// <param name="endPeriod">End sample date time</param>
        /// <returns></returns>
        public List<PerformanceData> QueryPerformanceCounter(string counterFullName, string deploymentid, string roleName,
                                                                string roleInstanceName,int period)
        {
            PerformanceDataContext context = new PerformanceDataContext(accountStorage.TableEndpoint.ToString(), accountStorage.Credentials);
            var data = context.PerfData;
            DateTime currentTime = DateTime.UtcNow;

            TimeSpan t = currentTime.Subtract(DateTime.MinValue);
            DateTime key = DateTime.MinValue.Add(new TimeSpan(0, ((int)t.TotalMinutes), 0));



            List<DateTime> timePoint = new List<DateTime>();

            for (int i = 0; i < period; i += 5)
            {
                timePoint.Add(key.AddMinutes(-i));
            }
            List<PerformanceData> selectedData = new List<PerformanceData>();
            foreach (DateTime point in timePoint)
            {
                CloudTableQuery<PerformanceData> query = null;
                List<PerformanceData> result = new List<PerformanceData>();
                query = (from d in data
                         where d.CounterName == counterFullName
                                    && d.DeploymentId == deploymentid
                                    && d.Role == roleName
                                    && d.RoleInstance == roleInstanceName
                                    && d.PartitionKey.CompareTo('0' + point.Ticks.ToString()) == 0
                         
                         select d).AsTableServiceQuery<PerformanceData>();





                try
                {
                    result = query.Execute().ToList<PerformanceData>();
                    selectedData.Add(result[0]);

                }


                catch
                {

                }
            }
            return selectedData;
        }
    }


}


