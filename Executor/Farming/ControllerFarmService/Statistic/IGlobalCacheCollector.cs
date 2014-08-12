using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonDataTypes.RExService.Service.Entity.Info;
using MITP;
using ServiceProxies.StatGlobalCache;

namespace ControllerFarmService.Statistic
{
    interface IGlobalCacheCollector{}

    interface INodeGlobalCacheCollector : IGlobalCacheCollector
    {
        void push(string resName, IEnumerable<NodeStateResponse> infos);
    }

    interface ITaskGlobalCacheCollector : IGlobalCacheCollector
    {
        void push(TaskRunContext context, ulong taskId, TaskStateInfo info);
    }
}
