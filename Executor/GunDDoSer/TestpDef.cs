using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GunDDoSer
{
    partial class Program
    {
        static ExecutionService.TaskDescription CreateTestpTask(ulong taskId)
        {
            var task = new ExecutionService.TaskDescription()
            {
                UserId = "sm",
                WfId = "GunDDoSing",
                TaskId = taskId,

                Package  = "testp",

                Params = new Dictionary<string, string>()
                {
                    {"in0", rnd.Next(1, 100).ToString()},
                    {"in1", rnd.Next(1, 100).ToString()},
                },
                
                ExecParams = String.IsNullOrEmpty(RESOURCE)? null: new Dictionary<string, string>()
                {
                    {"Resource", RESOURCE}
                }
            };

            return task;
        }
    }
}
