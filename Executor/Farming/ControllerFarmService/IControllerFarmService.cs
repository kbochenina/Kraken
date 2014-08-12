using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using CommonDataTypes.RExService.Service.Entity.Info;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;

namespace MITP
{
    [DataContract]
    public class FileContext
    {
        [DataMember] public string FileName  { get; private set; }
        [DataMember] public string StorageId { get; private set; }
    }

    [DataContract]
    [BsonIgnoreExtraElements]
    public class TaskRunContext // todo : compare with Task and TaskDescription
    {
        public object LocalId { get; set; }

        [DataMember] public ulong TaskId { get; private set; }
        
        //[DataMember] public IncarnationParams Incarnation { get; private set; } // todo : embed
        [DataMember] public string UserCert    { get; private set; }

        [DataMember] public string PackageName { get; private set; }
        [DataMember] public string CommandLine { get; private set; }

        [DataMember] public IEnumerable<FileContext> InputFiles         { get; private set; }
        [DataMember] public IEnumerable<string> ExpectedOutputFileNames { get; private set; }
        
        [DataMember] public IEnumerable<NodeRunConfig> NodesConfig      { get; private set; }

        public Resource Resource { get; set; }

        [BsonIgnore] public IStatelessResourceController Controller { get; set; }
    }

    [DataContract]
    public class TaskStateInfo
    {
        [DataMember] public TaskState State { get; set; }
        [DataMember] public string StateComment { get; set; }

        [DataMember] public Dictionary<string, TimeSpan> ActionsDuration  { get; private set; }

        [DataMember] public Dictionary<string, double> ResourceConsuption { get; private set; }

        [DataMember] public ProcessStatInfo ProcessInfo { get; set; }

        [DataMember] public string NodeName { get; set; }

        public bool IsFinished()
        {
            return IsFinished(State);
        }

        public static bool IsFinished(TaskState state)
        {
            if (state != TaskState.Started)
                return true;

            return false;
        }

        public TaskStateInfo(TaskState currentState = TaskState.Started, string stateComment = "")
        {
            State = currentState;
            StateComment = stateComment;

            ActionsDuration = new Dictionary<string, TimeSpan>();
            ResourceConsuption = new Dictionary<string, double>();
        }

        public TaskStateInfo(TaskStateInfo otherTask)
        {
            State = otherTask.State;
            StateComment = otherTask.StateComment;

            ActionsDuration = new Dictionary<string, TimeSpan>(otherTask.ActionsDuration);
            ResourceConsuption = new Dictionary<string, double>(otherTask.ResourceConsuption);
        }
    }


    [ServiceContract]
    public interface IControllerFarmService
    {
        [OperationContract]
        void Run(TaskRunContext task);

        [OperationContract]
        void Abort(ulong taskId);

        [OperationContract]
        TaskStateInfo GetTaskStateInfo(ulong taskId); // should remember string providedTaskId, Resource resource, IEnumerable<NodeConfig> nodesConfig

        //public TaskHistory GetTaskHistory(ulong taskId); // todo : Task history

        [OperationContract]
        ulong[] GetActiveTaskIds();

        [OperationContract]
        string[] GetActiveResourceNames();

        [OperationContract]
        NodeStateInfo[] GetNodesState(string resourceName);

        [OperationContract(IsOneWay=true)]
        void ReloadAllResources(string dumpingKey = null);
    }

    /*
    struct TimeSlice<T>
    {
        DateTime Time;
        T Value;
    }

    class TaskHistory  // todo : Task history
    {
        [DataMember] TaskStateInfo[] States { get; set; }
        [DataMember] TaskStateInfo CurrentState { get { return States.LastOrDefault(); } }

        [DataMember] Dictionary<string, TimeSpan> ActionTimes;
        [DataMember] Dictionary<string, List<TimeSlice<double>>> ResourceConsuption;
    }
    */
}
