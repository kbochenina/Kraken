using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using CommonDataTypes.RExService.Service.Entity.Info;
using MITP;
using MITP.Entity;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;

namespace ControllerFarmService
{
    public class UnixBaseController : PbsController, IStatelessResourceController
    {
        //think to remove this criteria
        public static double BUSY_RANGE = 90.0; 
        
        private abstract class SshUnixCommands
        {
            public const string Run = "sh";
            public const string SeparateThread = ">outsdr.txt 2>out_err.txt& echo $!";
            public const string GetLastPid = "echo $!";
            public const string Abort = "kill -9";
            public const string GetTaskState = "ps -p"; //change to checkfilefinished command
            public const string Ls = "ls -F --format=commas";
            //public const string GetNodeState = "top -n 1|grep \"Cpu(s):\"|awk '{ print $2 }'"; - TERM depended.

            public const string GetNodeState = "uptime";//"uptime |awk '{ print $12 }'";
        }

        protected override bool GetFromResourceTaskStateInfo(TaskRunContext task, out string result)
        {
            var node = GetNode(task);

            try
            {
                result = SshExec(node, GetTaskStateCommand(), (string)task.LocalId, null).ToLowerInvariant();
            }
            catch (Exception e)
            {
                Log.Error(String.Format("Exception while updating task's {0} state: {1}", task.TaskId, e));
                result = "SshExec error while updating task's state";
            }

            string clusterFolder = IncarnationParams.IncarnatePath(node.DataFolders.LocalFolder, task.TaskId, CopyPhase.Out);
            string result2 = SshExec(node, SshUnixCommands.Ls, clusterFolder);
            
            return result.Contains(task.LocalId.ToString()) && !result2.Contains(ClavireFinishFileName);
        }

        public override string ExecuteRun(ResourceNode node, string scriptPath)
        {
            return SshExec(node, GetRunCommand(), scriptPath + " " + SshUnixCommands.SeparateThread);
        }

        protected override String GetTaskStateCommand()
        {
            return SshUnixCommands.GetTaskState;
        }

        protected override String GetNodeStateCommand()
        {
            return SshUnixCommands.GetNodeState;
        }

        protected override String GetRunCommand()
        {
            return SshUnixCommands.Run;
        }

        public new IEnumerable<NodeStateResponse> GetNodesState(Resource resource)
        {
            var nodeStates = new List<NodeStateResponse>();

            foreach(var node in resource.Nodes)
            {
                string responseString = SshExec(node, SshUnixCommands.GetNodeState).ToLowerInvariant();
                Log.Debug("Load response: " + responseString);


                /*
                string LOAD_STRING_START = "load average: ";
                string LOAD_STRING_END = ",";

                int loadStartPos = responseString.IndexOf(LOAD_STRING_START) + LOAD_STRING_START.Length;
                int loadEndPos = responseString.IndexOf(LOAD_STRING_END, loadStartPos);
                string parseStr = responseString.Substring(loadStartPos, loadEndPos - loadStartPos);
                Log.Debug("Parsing val = '" + parseStr + "'");

                //double val = double.Parse(responseString.Substring(0, responseString.Length - 1), CultureInfo.InvariantCulture);
                double val = double.Parse(parseStr, CultureInfo.InvariantCulture);
                Log.Debug("Load value = " + val.ToString());
                */

                var rx = new Regex(@": (\d+[,.]\d*), (\d+[,.]\d*), (\d+[,.]\d*)\s*$");
                string loadStr = rx.Match(responseString).Groups[1].Value.Replace(',', '.');
                Log.Debug("Load string: " + loadStr);
                
                double loadValue = double.Parse(loadStr, CultureInfo.InvariantCulture);
                Log.Debug("Load value: " + loadValue.ToString());

                var state = (loadValue < BUSY_RANGE)
                              ? new NodeStateResponse(node.NodeName) {State = NodeState.Available}
                              : new NodeStateResponse(node.NodeName) {State = NodeState.Busy};
                //todo nbutakov change
                state.Consumption = new List<NodeInfo>();
                state.Consumption.Add(GetCurrentResourceInfo(node));

                nodeStates.Add(state);
            }

            return nodeStates;
        }
    }
}
