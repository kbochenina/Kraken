using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using CommonDataTypes;
using CommonDataTypes.RExService.Service.Entity.Info;

namespace StatisticsCacheService
{
    [ServiceContract]
    public interface IStatisticalService
    {
        /// <summary>
        /// //todo make change from processId to taskId
        /// Get current task info for the given process in order to bring some local system information
        /// </summary>
        [OperationContract]
        Dictionary<ulong, TaskStatInfo> GetAllTaskInfoStartedWith(DateTime dt);


        [OperationContract]
        TaskStatInfo GetTaskAllInfoStartedWith(ulong taskId, DateTime dt);
        /// <summary>
        /// Get current system information on drive memory, ram memory and process usage
        /// </summary>
        [OperationContract]
        Dictionary<string/*resName*/, Dictionary<string/*nodeName*/, List<NodeInfo>>> GetAllResourcesInfoStartedWith(DateTime date);
    }
}
