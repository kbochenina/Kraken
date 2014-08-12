using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.VManager.DataTypes;

namespace Common.VManager
{
    public interface IVManager
    {
        string Type
        {
            get;
        }

        Host[] GetHosts();

        Host GetHost(string hostName);
        
        Host AddHost(HostConnectionParams parameters);

        Machine[] GetHostsMachines(string hostName);

        Machine GetMachineState(string hostName, string machineName);

        bool ConnectToHost(string hostName);

        bool DisconnectFromHost(string hostName);

        bool IsGuestOSRunning(string hostName, string machineName);

        bool StartMachine(string hostName, string machineName, Dictionary<string, string> parameters);

        bool RestartMachine(string hostName, string machineName);

        bool SuspendMachine(string hostName, string machineName);

        bool WakeMachine(string hostName, string machineName);

        void StopMachine(string hostName, string machineName);

        void BeginTransaction();

        void EndTransaction();
    }
}
