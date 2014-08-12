using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;


namespace MITP
{
    public interface IStatelessResourceController
    {
        object Run(TaskRunContext task);

        void Abort(TaskRunContext task);

        TaskStateInfo GetTaskStateInfo(TaskRunContext task);

        IEnumerable<NodeStateResponse> GetNodesState(Resource resource);
    }
}