using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    [Command("TestTask", "Test.", AltName = "test")]
    class alertTest : StorageTask
    {
        public override void ExecuteCommand()
        {
            int[] testlist = { 1, 2, 3, 2, 3, 1, 4 };
            foreach (int count in testlist)
            {
                new SendAlertMailTask
                {
                    AlertSubject = string.Format("Alert mail test {0}", count),
                    Details = "test message",
                    AlertName = "test alert name",
                    Component = "work job service",
                    Level = "Error"
                }.ExecuteCommand();
            }
        }
    }
}
