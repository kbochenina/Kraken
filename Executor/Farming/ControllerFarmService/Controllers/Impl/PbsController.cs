using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Xml.Linq;
using System.Text;
using System.IO;
using ControllerFarmService;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;
using ControllerFarmService.Controllers.Error;
using MITP.Entity;
using Tamir.SharpSsh;

namespace MITP
{
    public class PbsController : ASshBasedController, IStatelessResourceController
    {
      
        private abstract class SshPbsCommands
        {
            public const string Run = "qsub"; 
                // todo : qsub -l nodes=node01:ppn=2+node02:ppn=4 see also ScriptPrepare for #PBS
                // http://www.clusterresources.com/torquedocs/2.1jobsubmission.shtml
            public const string Abort = "qdel";
            public const string GetTaskState = "qstat -f";
            public const string GetNodeState = "pbsnodes -x";
            public const string Ls = "ls -F --format=commas";
        }


        public object Run(TaskRunContext task)
        {
            var node = GetNode(task);
            var pack = PackageByName(node, task.PackageName);
            ulong taskId = task.TaskId;

            Log.Info("Locking operation");
            var operationHolder = LockOperation(task.TaskId, TaskLock.WRITE_OPERATION_EXECUTED);

            string fileNames;
            string clusterHomeFolder = CopyInputFiles(task, out fileNames);


            string cmdLine = String.Format(task.CommandLine, pack.AppPath, taskId, fileNames.Trim());
            Log.Info("cmdline = " + cmdLine);

            String scriptPath;

            Log.Info("Preparing script");
            ScriptPrepare(pack, cmdLine, node, clusterHomeFolder, out scriptPath);
            Log.Info("Script prepared. Executing it.");

            var result = ExecuteRun(node, scriptPath);
            
            string jobId = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).First();
            Log.Info("Exec done. Job id = " + jobId);

            UnLockOperation(task.TaskId, operationHolder);
            Log.Info("Operation unlocked");

            return jobId;
        }

        public virtual string ExecuteRun(ResourceNode node, string scriptPath)
        {
            return SshExec(node, GetRunCommand(), scriptPath);
        }

        protected virtual void ScriptPrepare(PackageOnNode pack, string cmdLine, ResourceNode node, String clusterHomeFolder, out String scriptPath)
        {
            var scriptDir = clusterHomeFolder.TrimEnd(new[] { '/', '\\' });
            scriptPath = scriptDir + "/run.sh";
            var scriptContent = new StringBuilder();
            scriptContent.Append("#!/bin/bash\n");
            scriptContent.Append("#PBS -l nodes=" + node.NodeName + "\n"); // String.Join("+", nodes.Select(n => n.NodeName))
            scriptContent.Append("cd " + scriptDir + "\n");
            scriptContent.Append(String.Format("date +%s%N > {0}\n", ClavireStartFileName));

            foreach (var pair in pack.EnvVars)
                scriptContent.AppendFormat("export {0}={1}\n", pair.Key, pair.Value);

            // -n = do not override files, so user's input file doesn't vanish => copying startup files in reverse order
            foreach (string path in pack.CopyOnStartup.Reverse())
                scriptContent.Append("cp -r -n " + path + " ./\n"); 

            scriptContent.Append(cmdLine);
            scriptContent.Append(String.Format("\n date +%s%N > {0}\n", ClavireFinishFileName));
            
            string scriptFilePathLocal = Path.GetTempFileName();
            
            File.WriteAllText(scriptFilePathLocal, scriptContent.ToString());
            //ScpCopy(node, scriptPath, scriptFilePathLocal);
            UploadFile(node, scriptPath, scriptFilePathLocal);

            // todo: File.Delete(scriptFilePathLocal);
        }

        public void Abort(TaskRunContext task)
        {
            try
            {
                var node = GetNode(task);
                SshExec(node, GetTaskStateCommand(), (string)task.LocalId); // todo : Abort, not GetTaskState?
            }
            catch (Exception e)
            {
                Log.Error(String.Format("Failed to abort task {1} on resource {2}: {3}{0}{4}",
                    Environment.NewLine,
                    task.TaskId, task.Resource.ResourceName,
                    e.Message, e.StackTrace
                ));
                // todo : throw;
            }
        }

        protected virtual bool GetFromResourceTaskStateInfo(TaskRunContext task, out string result)
        {
            var node = GetNode(task);
            result = SshExec(node, GetTaskStateCommand(), (string)task.LocalId, new PbsErrorResolver()).ToLowerInvariant();
            return result.Contains("job_state = R") || result.Contains("job_state = Q") || result.Contains("job_state = r") || result.Contains("job_state = q");
        }

        protected virtual String GetTaskStateCommand()
        {
            return SshPbsCommands.GetTaskState;
        }

        protected virtual String GetNodeStateCommand()
        {
            return SshPbsCommands.GetNodeState;
        }

        protected virtual String GetRunCommand()
        {
            return SshPbsCommands.Run;
        }

        public TaskStateInfo GetTaskStateInfo(TaskRunContext task)
        {
            var node = GetNode(task);
            var taskId = task.TaskId;
            
            string result;
            TaskStateInfo info = null;

            if (GetFromResourceTaskStateInfo(task, out result))
            {

                info = new TaskStateInfo(TaskState.Started, result);
                
            }
                
            // esle if (Aborted, Failed)
            else
            {
                CopyOutputsToExchange(task);

                info = new TaskStateInfo(TaskState.Completed, result);
            }

            //todo nbutakov change
            info.ProcessInfo = GetCurrentTaskInfo(task);
            info.NodeName = GetNode(task).NodeName;
            return info;
        }

        public HashSet<String> ImproveFiles(IEnumerable<String> files)
        {
            var result = new HashSet<String>();
            foreach (var file in files)
            {
                var res = (!file.StartsWith("/")) ? result.Add("/" + file) : result.Add(file);
            }

            result.Add("/" + ClavireStartFileName);
            result.Add("/" + ClavireFinishFileName);
            return result;
        }

        public IEnumerable<NodeStateResponse> GetNodesState(Resource resource)
        {
            string responseString = SshExec(resource.Nodes.First(), GetNodeStateCommand()).ToLowerInvariant();            
            var responseXml = XElement.Parse(responseString);

            var freeNodeNames = responseXml.Descendants("node")
                .Where(node => (node.Element("state").Value.Equals("free")))
                .Select(node => (node.Element("name").Value));
            
            /*var freeNodeNames = responseXml.Descendants("node")
                .Where(node => node.Element("status").Value.Contains("state=free"))
                .Select(node => node.Element("name").Value);*/

            var nodeStates = resource.Nodes.Select(node =>
                freeNodeNames.Contains(node.NodeName) ?
                    new NodeStateResponse(node.NodeName) { State = NodeState.Available } :
                    new NodeStateResponse(node.NodeName) { State = NodeState.Busy }
            );

            return nodeStates;
        }
    }
}