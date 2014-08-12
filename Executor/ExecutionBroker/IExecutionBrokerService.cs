using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace MITP
{
    [DataContract]
    public struct TaskDependency
    {
        [DataMember]
        public string WfId { get; private set; }

        [DataMember]
        public ulong ParentTaskId { get; private set; }

        [DataMember]
        public ulong ChildTaskId { get; private set; }
    }

    [ServiceContract]
    public interface IExecutionBrokerService
    {
        [OperationContract]
        bool MagicHappens();

        //[OperationContract]
        //void Define(IEnumerable<TaskDescription> tasks, IEnumerable<TaskDependency> dependencies);

        [OperationContract]
        void DefineTask(TaskDescription task);

        // todo : return generated taskIds[] in case they are missing
        // todo : dependecies -> taskDescription

        [OperationContract]
        void DefineDependencies(IEnumerable<TaskDependency> dependencies);

        // REV: зачем ulong
        [OperationContract]
        void Execute(IEnumerable<ulong> taskIds);

        // REV: может и в аборт передавать массив?
        [OperationContract]
        void Abort(IEnumerable<ulong> taskId);

        // REV: тоже неплохо бы сделать версию с массивом
        [OperationContract]
        Task GetInfo(ulong taskId);

        // todo : task Info with states

        [OperationContract]
        IEnumerable<BriefTaskInfo> GetBriefTaskList();

        /*
        [OperationContract]
        void OnSituationChanged(); // event handler
        */

        [OperationContract]
        ulong GetNewTaskId();

        // for debug purposes

        [OperationContract]
        void Update();
    }
}