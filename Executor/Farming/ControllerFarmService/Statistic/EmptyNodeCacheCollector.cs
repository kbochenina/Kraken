using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MITP;
using ServiceProxies.ResourceBaseService;

namespace ControllerFarmService.Statistic
{
    // Empty collector does nothing
    class EmptyNodeCacheCollector:GlobalCacheCollector
    {
        public EmptyNodeCacheCollector(IEnumerable<Resource> resources) : base(resources)
        {
        }

        public void push(string resName, IEnumerable<NodeStateResponse> infos)
        {
        }
    }
}
