using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MITP
{
    public class WinPcAdapter : AbstractMachineAdapter
    {
        public override bool Mathces(Task task)
        {
            if (task.AssignedTo.ProviderName == CONST.Providers.WinPc)
                return true;

            return false;
        }

        public override void OnStart(Task task, string ftpFolder)
        {
            var provider = Broker.ProviderByName(CONST.Providers.WinPc);
            string providedTaskId = task.TaskId.ToString();

            task.Incarnation.ProvidedTaskId = providedTaskId;

            IOProxy.Ftp.MakePath(ftpFolder);
            //task.Incarnation.InputFolderPath = WinPcProvider.FTP_FOLDER + providedTaskId + "/in/";
            //task.Incarnation.OutputFolderPath = WinPcProvider.FTP_FOLDER + providedTaskId + "/out/";
        }

        public override void OnFinish(Task task, string ftpFolder)
        {
        }
    }
}