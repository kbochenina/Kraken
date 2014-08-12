using System;
using System.Collections.Generic;
using System.ServiceModel;
using CommonDataTypes;
using CommonDataTypes.RExService.Service.Entity.Info;

namespace ControllerFarmService.Controllers
{
    [ServiceContract]
    public interface IStatisticalBuffer
    {
        [OperationContract]
        Dictionary<ulong, TaskStatInfo> GetAllCacheableTaskInfoStartedWith(DateTime date);

        [OperationContract]
        TaskStatInfo GetTaskInfoStartedWith(ulong taskId,DateTime date);

        [OperationContract]
        Dictionary<string/*resName*/, Dictionary<string/*nodeName*/, List<NodeInfo>>> GetAllCacheableResourcesInfoStartedWith(DateTime date);
    }
}
