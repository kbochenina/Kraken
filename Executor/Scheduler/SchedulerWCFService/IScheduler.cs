using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using Scheduler;
using Common;

namespace SchedulerWCFService
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IService1" in both code and config file together.
    [ServiceContract]    
    public interface IScheduler
    {
        [OperationContract]
        [FaultContract(typeof(ErrorMessage))]
        TaskScheduler.LaunchPlan Reschedule(TaskScheduler.Workflow workflow);

        [OperationContract]
        [FaultContract(typeof(ErrorMessage))]
        [ServiceKnownType(typeof(Scheduler.Estimated.EstimatedUrgentWorkflow))]
        [ServiceKnownType(typeof(Scheduler.Estimated.UrgentPlan))]
        Scheduler.Estimated.LaunchPlan RescheduleEstimated(Scheduler.Estimated.EstimatedWorkflow workflow);

        [FaultContract(typeof(ErrorMessage))]
        [OperationContract]
        TaskScheduler.Workflow CreateTestWF();

        [OperationContract]
        [FaultContract(typeof(ErrorMessage))]
        TaskScheduler.LaunchPlan RescheduleUrgent(TaskScheduler.UrgentWorkflow workflow);

        [FaultContract(typeof(ErrorMessage))]
        [OperationContract]
        TaskScheduler.UrgentWorkflow CreateUrgentTestWF();

        [FaultContract(typeof(ErrorMessage))]
        [OperationContract]
        IEnumerable<string> GetClusterNames();

        [FaultContract(typeof(ErrorMessage))]
        [OperationContract]
        IEnumerable<string> GetAppNames();

        [FaultContract(typeof(ErrorMessage))]
        [OperationContract]
        string GetDefaultUHName();

        [FaultContract(typeof(ErrorMessage))]
        [OperationContract]
        IEnumerable<string> GetUHNames();

        [FaultContract(typeof(ErrorMessage))]
        [OperationContract]
        void SetDefaultUHName(string newName);

        [FaultContract(typeof(ErrorMessage))]
        [OperationContract]
        string GetDefaultHName();

        [FaultContract(typeof(ErrorMessage))]
        [OperationContract]
        IEnumerable<string> GetHNames();

        [FaultContract(typeof(ErrorMessage))]
        [OperationContract]
        void SetDefaultHName(string newName);
    }
}
