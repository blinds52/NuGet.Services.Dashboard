using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGet.Services.Dashboard.Common;

namespace NuGetDashboard.Models
{
    public class AlertMailViewModel
    {
        public string AlertSubject { get; set; }
        public int count { get; set; }

        public DateTime lastTime { get; set; }

        public string Component { get; set;}

        public string details_ID { get; set; }

        public string lastAction { get; set; }

    }

    public class ActionViewModel
    {
        public string AlertSubject { get; set; }
        public string Action { get; set; }
    }
}