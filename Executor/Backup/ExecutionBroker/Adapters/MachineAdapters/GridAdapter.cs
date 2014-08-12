using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MITP
{
    public class GridAdapter : AbstractMachineAdapter
    {
        public override bool Mathces(Task task)
        {
            if (task.AssignedTo.ProviderName == CONST.Providers.GridNns)
                return true;

            return false;
        }

        public override void OnStart(Task task, string ftpFolder)
        {
            var provider = Broker.ProviderByName(CONST.Providers.GridNns);
            string providedTaskId = task.TaskId.ToString();

            task.Incarnation.ProvidedTaskId = providedTaskId;
            //task.Incarnation.InputFolderPath = provider.GetFtpInputFolder(providedTaskId);
            //task.Incarnation.OutputFolderPath = provider.GetFtpOutputFolder(providedTaskId);
        }

        public override void OnFinish(Task task, string ftpFolder)
        {
        }
    }
}