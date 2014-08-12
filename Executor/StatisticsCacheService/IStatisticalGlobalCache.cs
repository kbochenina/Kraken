using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using CommonDataTypes;
using CommonDataTypes.RExService.Service.Entity.Info;


namespace StatisticsCacheService
{
    [ServiceContract(Namespace = "http://escience.ifmo.ru/nano/services/")]
    public interface IStatisticalGlobalCache
    {
        [OperationContract]
        void AddAllTaskInfo(Dictionary<ulong, TaskStatInfo> data);

        [OperationContract]
        void AddAllResourcesInfo(Dictionary<string, Dictionary<string, List<NodeInfo>>> data);

    }
}
