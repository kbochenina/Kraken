using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.VManager;
using Common.VManager.DataTypes;
namespace Common.VManager.Base
{
    public abstract class AManager : IVManager
    {
        public abstract string Type
        {
            get;
        }

        private readonly Dictionary<string, IHost> hosts = new Dictionary<string, IHost>();

        public abstract void BeginTransaction();
        public abstract void EndTransaction();

        public virtual Host[] GetHosts()
        {
            return hosts.Values.Select(h => h.CreateDescription()).ToArray(); ;
        }

        public virtual Host GetHost(string hostName)
        {
            if (!hosts.ContainsKey(hostName))
            {
                throw new Common.VManager.DataTypes.HostNotFoundException(hostName);
            }
            return hosts[hostName].CreateDescription();
        }

        public virtual Host AddHost(Common.VManager.DataTypes.HostConnectionParams parameters)
        {
            var host = CreateHostInstance(parameters);
            if (!hosts.ContainsKey(host.Name))
            {
                hosts[host.Name] = host;
            }
            return GetHost(host.Name);
        }

        protected abstract IHost CreateHostInstance(Common.VManager.DataTypes.HostConnectionParams parameters);

        public virtual Machine[] GetHostsMachines(string hostName)
        {
            return hosts[hostName].GetMachines().Select(m => m.CreateDescription()).ToArray();
        }

        public virtual Machine GetMachineState(string hostName, string machineName)
        {
            return hosts[hostName].GetMachine(machineName).CreateDescription();
        }

        public virtual bool ConnectToHost(string hostName)
        {
            return hosts[hostName].Connect();
        }

        public virtual bool DisconnectFromHost(string hostName)
        {
            return hosts[hostName].Disconnect();
        }

        public virtual bool IsGuestOSRunning(string hostName, string machineName)
        {
            return hosts[hostName].GetMachine(machineName).IsGuestOSRunning;
        }

        public virtual bool StartMachine(string hostName, string machineName, Dictionary<string, string> parameters)
        {
            return hosts[hostName].GetMachine(machineName).Start(parameters);
        }

        public virtual bool RestartMachine(string hostName, string machineName)
        {
            return hosts[hostName].GetMachine(machineName).Restart();
        }

        public virtual bool SuspendMachine(string hostName, string machineName)
        {
            return hosts[hostName].GetMachine(machineName).Suspend();
        }

        public virtual bool WakeMachine(string hostName, string machineName)
        {
            return hosts[hostName].GetMachine(machineName).Wake();
        }

        public virtual void StopMachine(string hostName, string machineName)
        {
            hosts[hostName].GetMachine(machineName).Stop();
        }
    }
}
