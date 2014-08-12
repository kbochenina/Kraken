using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GunDDoSer
{
    partial class Program
    {
        static ExecutionService.TaskDescription CreateLsTask(ulong taskId)
        {
            var task = new ExecutionService.TaskDescription()
            {
                UserId = "sm",
                WfId = "GunDDoSing",
                TaskId = taskId,

                Package  = "ls",

                InputFiles = new ExecutionService.TaskFileDescription[]
                {
                    new ExecutionService.TaskFileDescription()
                    {
                        SlotName = "in_file",
                        FileName = "excluder.conf",
                        StorageId = "0FPITTIR0W1HX947XFWQ",
                    }
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
