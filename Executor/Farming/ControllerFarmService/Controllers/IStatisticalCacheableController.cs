using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonDataTypes.RExService.Service.Entity.Info;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;
using MITP;
using ServiceProxies.ExtStatService;

namespace ControllerFarmService.Controllers
{
    interface IStatisticalCacheableController
    {
        Dictionary<string, List<ProcessStatInfo>> GetTaskInfoStartWith(ulong taskId, DateTime startTime, TaskRunContext task);

        Dictionary<string, List<NodeInfo>> GetResourceInfoStartWith(Resource resource, DateTime startTime);
    }
}
