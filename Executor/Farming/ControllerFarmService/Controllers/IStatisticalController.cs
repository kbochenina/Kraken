using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonDataTypes.RExService.Service.Entity.Info;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;
using ServiceProxies.ExtStatService;

namespace MITP
{
    public interface IStatisticalController
    {
        //todo correct it when you obtain general picture of services
        /// Get current task info for the given process in order to bring some local system information
        /// </summary>
        //List<ProcessStatInfo> GetAllTaskInfo(int taskId);
        /// <summary>
        /// Get current system information on drive memory, ram memory and process usage
        /// </summary>
        /*Dictionary<string , */NodeInfo/*>*/ GetCurrentResourceInfo(ResourceNode node);
    }
}
