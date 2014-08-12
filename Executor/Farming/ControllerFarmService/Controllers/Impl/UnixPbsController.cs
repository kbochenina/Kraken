using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Linq;
using ControllerFarmService;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;

namespace MITP
{
    public class UnixPbsController : PbsController
    {
        private abstract class UnixPbsCommands
        {
            public const string Abort = "qdel";
            public const string Run = "sh";
            //todo replace with a safe logic
            public const string SeparateThread = ">outsdr.txt 2>out_err.txt& sleep 2s ; head -n 1 outsdr.txt";
            public const string GetTaskState = "qstat -f";
            public const string GetNodeState = "pbsnodes -x";
            public const string Ls = "ls -F --format=commas";
        }

        public override string ExecuteRun(ResourceNode node, string scriptPath)
        {
            return SshExec(node, GetRunCommand(), scriptPath + " " + UnixPbsCommands.SeparateThread);
        }

        protected override String GetTaskStateCommand()
        {
            return UnixPbsCommands.GetTaskState;
        }

        protected override String GetNodeStateCommand()
        {
            return UnixPbsCommands.GetNodeState;
        }

        protected override String GetRunCommand()
        {
            return UnixPbsCommands.Run;
        }
    }
}