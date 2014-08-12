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
    public class SlurmController : ASshBasedController, IStatelessResourceController
    {
        private const string PARTITION_NAME_PARAM = "Partition";

        private abstract class SshCommands
        {
            public const string Run = "sbatch";
            public const string Abort = "scancel";
            public const string GetTaskState = "scontrol show job"; // instead of squeue
            public const string GetPartitionState = "scontrol show partition"; //instead of sinfo
            //public const string Ls = "ls -F --format=commas";
        }

        private string GetPartition(ResourceNode node)
        {
            return node.StaticHardwareParams[PARTITION_NAME_PARAM];
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
            Log.Debug("cmdline = " + cmdLine);

            Log.Info("Preparing script");
            string scriptPath = MakeScript(pack, cmdLine, node, clusterHomeFolder);

            Log.Info("Script prepared. Executing it.");
            var result = SshExec(node, SshCommands.Run, scriptPath);

            UnLockOperation(task.TaskId, operationHolder);
            Log.Info("Operation unlocked");

            string jobId = result.Split(new[] { '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Last();
            Log.Info("Exec done. Job id = " + jobId);
            return jobId;
        }

        protected virtual string MakeScript(PackageOnNode pack, string cmdLine, ResourceNode node, string clusterHomeFolder)
        {
            string workDir = clusterHomeFolder.TrimEnd(new[] { '/', '\\' });
            string scriptPath = workDir + "/run.sh";

            var scriptContent = new StringBuilder();
            scriptContent.Append("#!/bin/bash\n");
            scriptContent.Append("#SBATCH --partition=" + GetPartition(node) + "\n");
            scriptContent.Append(@"#SBATCH --workdir=""" + workDir + "\"\n");
            scriptContent.Append(@"#SBATCH --output=std.out" + "\n");
            //scriptContent.Append("#SBATCH --immediate\n"); // не ставить в очередь, а упасть, если нету сразу доступных ресурсов
            //scriptContent.Append("#SBATCH --no-requeue\n"); // не перезапускать в случа падения
            //scriptContent.Append("#SBATCH --wait-all-nodes=1\n"); // не запускать, пока не будут готовы все узлы
            
            //scriptContent.Append("#SBATCH --gres\n"); // запрос ресурсов
            //scriptContent.Append("#SBATCH --nodes=1-1\n"); //min-max
            //scriptContent.Append("#SBATCH --nodelist=" + node.NodeName + "\n"); // String.Join(",", nodes.Select(n => n.NodeName))

            scriptContent.Append("cd " + workDir + "\n");
            scriptContent.AppendFormat("date +%s%N > {0}\n", ClavireStartFileName);

            foreach (var pair in pack.EnvVars)
                scriptContent.AppendFormat("export {0}={1}\n", pair.Key, pair.Value);

            // -n = do not override files, so user's input file doesn't vanish => copying startup files in reverse order
            foreach (string path in pack.CopyOnStartup.Reverse())
                scriptContent.Append("cp -r -n " + path + " ./\n"); 

            scriptContent.Append(cmdLine);
            scriptContent.AppendFormat("\ndate +%s%N > {0}\n", ClavireFinishFileName);

            string scriptFilePathLocal = Path.GetTempFileName();
            File.WriteAllText(scriptFilePathLocal, scriptContent.ToString());
            //ScpCopy(node, scriptPath, scriptFilePathLocal);
            UploadFile(node, scriptPath, scriptFilePathLocal);
            File.Delete(scriptFilePathLocal);

            return scriptPath;
        }

        public void Abort(TaskRunContext task)
        {
            try
            {
                var node = GetNode(task);
                SshExec(node, SshCommands.Abort, (string) task.LocalId); // todo : Abort, not GetTaskState?
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

        public TaskStateInfo GetTaskStateInfo(TaskRunContext task)
        {
            var node = GetNode(task);

            string result = SshExec(node, SshCommands.GetTaskState, (string)task.LocalId);
            string result_UPPER = result.ToUpperInvariant();

            string[] runningTokens   = new[] { "CONFIGURING", "COMPLETING", "PENDING", "RUNNING", "SUSPENDED" };
            string[] abortedTokens   = new[] { "CANCELLED", "TIMEOUT" };
            string[] failedTokens    = new[] { "FAILED", "NODE_FAIL", "PREEMPTED" };
            string[] completedTokens = new[] { "COMPLETED", "Invalid job id specified".ToUpperInvariant() };

            if (runningTokens.Any(st => result_UPPER.Contains(st)))
                return new TaskStateInfo(TaskState.Started, result);
            else 
            if (abortedTokens.Any(st => result_UPPER.Contains(st)))
                return new TaskStateInfo(TaskState.Aborted, result);
            else 
            if (failedTokens.Any(st => result_UPPER.Contains(st)))
                return new TaskStateInfo(TaskState.Failed, result);
            else 
            if (completedTokens.Any(st => result_UPPER.Contains(st)))
            {
                CopyOutputsToExchange(task);
                return new TaskStateInfo(TaskState.Completed, result);
            }
            else
            {
                Log.Warn("Wnknown responce from SLURM. Hoping task was completed: " + result);

                CopyOutputsToExchange(task);
                return new TaskStateInfo(TaskState.Completed, result);
            }
        }

        public IEnumerable<NodeStateResponse> GetNodesState(Resource resource)
        {
            var partitionNames = resource.Nodes.Select(n => GetPartition(n)).Distinct();
            var badPartitions = new List<string>();

            foreach (string partitionName in partitionNames)
            {
                string result = SshExec(resource.Nodes.First(), SshCommands.GetPartitionState, partitionName);
                string result_LOWER = result.ToLowerInvariant();
                if (!result_LOWER.Contains("state=up") ||
                    result_LOWER.Contains("totalnodes=0 ") ||
                    result_LOWER.Contains("totalcpus=0 "))
                {
                    badPartitions.Add(partitionName);
                }
            }

            var nodeStates = resource.Nodes.Select(n =>
                badPartitions.Contains(GetPartition(n)) ?
                    new NodeStateResponse(n.NodeName) { State = NodeState.Down } :
                    new NodeStateResponse(n.NodeName) { State = NodeState.Available }
            );

            return nodeStates;
        }
    }
}

