using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MITP;

namespace ControllerFarmService.Statistic
{
    // Empty collector does nothing
    class EmptyTaskCacheCollector:TaskCacheCollector
    {
        public void push(TaskRunContext context, ulong taskId, TaskStateInfo info)
        {
        }
    }
}
