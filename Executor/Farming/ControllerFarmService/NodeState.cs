using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;
using CommonDataTypes.RExService.Service.Entity.Info;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MITP
{
    [DataContract]
    public enum NodeState
    {
        [EnumMember] Available,
        [EnumMember] Busy,
        [EnumMember] Down
    }

    [DataContract]
    public class NodeStateResponse
    {
        [DataMember] public string NodeName { get; protected set; }

        [DataMember] public NodeState State { get; set; }
        [DataMember] public uint CoresUsed { get; set; }
        [DataMember] public Dictionary<string, string> DynamicHardwareParams { get; set; }

        [DataMember] public List<NodeInfo> Consumption { get; set; } 

        public NodeStateResponse(string nodeName)
        {
            NodeName = nodeName;
            CoresUsed = 0;
            DynamicHardwareParams = new Dictionary<string, string>();
        }
    }

    [DataContract]
    public class NodeStateInfo : NodeStateResponse
    {
        [DataMember] public string ResourceName { get; private set; }

        [DataMember] public uint TasksSubmissionLimit  { get; private set; }
        [DataMember] public uint CoresCount { get; private set; }
        
        [DataMember] public uint TasksSubmitted { get; set; }
        [DataMember] public uint CoresReserved { get; set; }

        [DataMember]
        public int CoresAvailable
        {
            get
            {
                if (State != NodeState.Available)
                    return 0;

                if (TasksSubmitted >= TasksSubmissionLimit)
                    return 0;

                if (CoresReserved >= CoresCount || CoresUsed >= CoresCount)
                    return 0;

                long coresAvailable = CoresCount - Math.Max(CoresReserved, CoresUsed);
                return (int) coresAvailable;
            }

            private set { } // WCF asked for it
        }

        public NodeStateInfo(NodeStateInfo otherState)
            : base(otherState.NodeName)
        {
            ResourceName = otherState.ResourceName;
            NodeName = otherState.NodeName;

            TasksSubmissionLimit = otherState.TasksSubmissionLimit;
            CoresCount = otherState.CoresCount;

            State = otherState.State;

            TasksSubmitted = otherState.TasksSubmitted;
            CoresReserved = otherState.CoresReserved;
            CoresUsed = otherState.CoresUsed;

            DynamicHardwareParams = new Dictionary<string, string>(otherState.DynamicHardwareParams);
        }

        public NodeStateInfo(string resourceName, string nodeName, uint coresCount, uint tasksSubmissionLimit)
            : base(nodeName)
        {
            ResourceName = resourceName;
            NodeName = nodeName;

            TasksSubmissionLimit = tasksSubmissionLimit;
            CoresCount = coresCount;

            State = NodeState.Available;

            TasksSubmitted = 0;
            CoresReserved = 0;
            CoresUsed = 0;

            DynamicHardwareParams = new Dictionary<string, string>();
        }

        public void ApplyState(NodeStateResponse otherState)
        {
            State = otherState.State;
            CoresUsed = otherState.CoresUsed;
            DynamicHardwareParams = new Dictionary<string, string>(otherState.DynamicHardwareParams);
        }

        public void ApplyState(NodeStateInfo otherState)
        {
            ApplyState((NodeStateResponse) otherState);

            TasksSubmitted = otherState.TasksSubmitted;
            CoresReserved = otherState.CoresReserved;
        }
    }
}
