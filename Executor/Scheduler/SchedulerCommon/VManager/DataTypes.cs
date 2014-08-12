using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Common.VManager.DataTypes
{
    [DataContract]
    public class Host
    {
        [DataMember]
        public string Name;

        [DataMember]
        public string Description;

        [DataMember]
        public string Type;

        [DataMember]
        public bool IsConnected;
    }

    [DataContract]
    public enum MachineState
    {
        [EnumMember]
        STOPPED,
        [EnumMember]
        RUNNING,
        [EnumMember]
        SUSPENDED,
        [EnumMember]
        TRANSITONED
    }

    [DataContract]
    public class Machine
    {
        [DataMember]
        public string Name;

        [DataMember]
        public string Description;

        [DataMember]
        public string Type;

        [DataMember]
        public MachineState State;
    }

    [DataContract]
    public struct HostConnectionParams
    {
        [DataMember]
        public string Name;

        [DataMember]
        public string Type;

        [DataMember]
        public Dictionary<string, string> Parameters;
    }

    public abstract class VManagerException : ApplicationException
    {
    }

    public class HostNotFoundException : VManagerException
    {
        public string HostName
        {
            get;
            private set;
        }

        public HostNotFoundException(string hostName)
        {
            HostName = hostName;            
        }

        public override string Message
        {
            get
            {
                return String.Format("Host not found: {0}", HostName);
            }
        }
    }

    public class MachineNotFoundException : VManagerException
    {
        public string HostName
        {
            get;
            private set;
        }

        public string MachineName
        {
            get;
            private set;
        }

        public MachineNotFoundException(string hostName, string machineName)
        {
            HostName = hostName;
            MachineName = machineName;
        }

        public override string Message
        {
            get
            {
                return String.Format("Machine not found {0} at host {1}", MachineName, HostName);
            }
        }
    }
}
