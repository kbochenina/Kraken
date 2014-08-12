using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GunDDoSer
{
    partial class Program
    {
        static ExecutionService.TaskDescription CreateDummyTask(ulong taskId)
        {
            var task = new ExecutionService.TaskDescription()
            {
                UserId = "sm",
                WfId = "GunDDoSing",
                TaskId = taskId,

                Package  = "dummy",

                Params = new Dictionary<string,string>()
                {
                    {"wait_time", "3000"},
                }
            };

            return task;
        }
    }
}
