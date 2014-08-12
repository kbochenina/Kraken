using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.ServiceModel;
//using ControllerFarmService.ResourceBaseService;
using ControllerFarmService.Installation;
using ControllerFarmService.Installation.Impl;
using ControllerFarmService.Installation.Interfaces;
using ServiceProxies.ResourceBaseService;
using ControllerFarmService.RExService;
using PFX = System.Threading.Tasks;
using Config = System.Configuration.ConfigurationManager;

namespace MITP
{
    public class WindowsController : AbstractResourceController, IStatelessResourceController, IInstallationController
    {
        private const string DEBUG_PAUSE_PARAM_NAME = "Windows.Debug.Pause.BeforeLine";

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private RExServiceClient GetREx(string url)
        {
            var service = new RExServiceClient();
            service.Endpoint.Address = new EndpointAddress(url);
            return service;
        }

        public TaskStateInfo GetTaskStateInfo(TaskRunContext task)
        {
            string[] providedWords = ((string)task.LocalId).Split(new char[] { '\n' }); // todo : string -> string[]
            if (providedWords.Length > 2)
                logger.Warn("Too many sections in provided task id for win PC: {0}", task.LocalId);

            string pid = providedWords[0];
            string nodeName = providedWords[1];
            var node = task.Resource.Nodes.First(n => n.NodeName == nodeName);

            var rexService = GetREx(node.Services.ExecutionUrl);
            try
            {
                //rexService.InnerChannel.OperationTimeout = TimeSpan.FromSeconds(10);
                bool isRunning = rexService.IsProcessRunning(Int32.Parse(pid));
                rexService.Close();

                if (!isRunning)
                {
                    return new TaskStateInfo(TaskState.Completed, "");
                }

                return new TaskStateInfo(TaskState.Started, "");
            }
            catch (Exception e)
            {
                rexService.Abort();
                logger.WarnException(string.Format("Exception while getting task '{0}' state (local id = {1}): ", task.TaskId, task.LocalId), e);

                throw;
                //return new TaskStateInfo(TaskState.Started, "");
            }
        }


        public object Run(TaskRunContext task)
        {
            ulong taskId = task.TaskId;
            int coresToUse = (int)task.NodesConfig.Sum(cfg => cfg.Cores);
            var node = GetNode(task);

            string ftpFolder = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromSystem, taskId, CopyPhase.In);
            string jobFtpFolder = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromSystem, taskId, CopyPhase.None);
            string sharedInputFolder = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromResource, taskId, CopyPhase.In);
            string sharedOutputFolder = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromResource, taskId, CopyPhase.Out);
            string tmpFolder = IncarnationParams.IncarnatePath(node.DataFolders.LocalFolder, taskId, CopyPhase.None);

            IOProxy.Ftp.MakePath(ftpFolder);
            IOProxy.Ftp.MakePath(jobFtpFolder);

            string jobFileName = "job_" + taskId + ".cmd";

            logger.Info("Trying to exec task {0} on win PC {1}.{2}", taskId, node.ResourceName, node.NodeName);

            var pack = node.Packages.First(p => String.Equals(p.Name, task.PackageName, StringComparison.InvariantCultureIgnoreCase));
            string batchContent = "";
            batchContent += "mkdir " + tmpFolder.TrimEnd(new char[] { '/', '\\' }) + Environment.NewLine;

            if (Path.IsPathRooted(tmpFolder)) // change drive if needed
                batchContent += Path.GetPathRoot(tmpFolder).TrimEnd(new char[] { '/', '\\' }) + Environment.NewLine;

            batchContent += String.Format(
                @"cd {0}" + Environment.NewLine,
                tmpFolder.TrimEnd(new char[] { '/', '\\' })
            );

            batchContent += "echo %time% > clavire_script_started" + Environment.NewLine;

            foreach (string copyPath in pack.CopyOnStartup)
                batchContent += String.Format(
                    @"xcopy {0} {1}\ /z /s /e /c /i /h /r /y" + Environment.NewLine,
                    copyPath.TrimEnd(new char[] { '/', '\\' }),
                    tmpFolder.TrimEnd(new char[] { '/', '\\' })
                );

            batchContent += String.Format(
                //@"ping localhost -w 1000 -n 50" + Environment.NewLine +
                @"xcopy {0} {1}\ /z /s /e /c /i /h /r /y" + Environment.NewLine,
                sharedInputFolder.TrimEnd(new char[] { '/', '\\' }),
                tmpFolder.TrimEnd(new char[] { '/', '\\' })
            );

            foreach (var envVar in pack.EnvVars)
                batchContent += "set " + envVar.Key + "=" + envVar.Value + Environment.NewLine;

            string commandLine = task.CommandLine;
            //var pack = node.Packages.First(p => commandLine.StartsWith(p.Name, StringComparison.InvariantCultureIgnoreCase));
            //commandLine = pack.Params["appPath"] + commandLine.Substring(pack.Name.Length);
            commandLine = String.Format(task.CommandLine, pack.AppPath);
            //commandLine = String.Format(incarnation.CommandLine, pack.Params["appPath"]);

            batchContent += "echo %time% > clavire_task_started" + Environment.NewLine;
            batchContent += //"start \"" + jobFileName + " " + incarnation.PackageNameInConfig + "\" /wait /b" + 
                "cmd.exe /c " + commandLine + Environment.NewLine;
            batchContent += "echo %time% > clavire_task_finished" + Environment.NewLine;


            foreach (string copyPath in pack.CleanupIgnore)
            {
                batchContent += String.Format(
                    @"xcopy {1} {0} /z /s /e /c /i /h /r /y" + Environment.NewLine,
                    (sharedOutputFolder.TrimEnd(new char[] { '/', '\\' }) + "/" + copyPath.TrimStart(new char[] { '/', '\\' })).Replace("/", "\\"),
                    (tmpFolder.TrimEnd(new char[] { '/', '\\' }) + "/" + copyPath.TrimStart(new char[] { '/', '\\' })).Replace("/", "\\")
                );
            }

            foreach (string delPath in pack.Cleanup)
            {
                batchContent += String.Format(
                    @"rmdir /s /q {0}" + Environment.NewLine +
                    @"del /f /s /q {0}" + Environment.NewLine,
                    tmpFolder + delPath  // todo: delPath.TrimStart
                );
            }

            batchContent += String.Format(
                @"xcopy {1} {0}\ /z /s /e /c /i /h /r /y" + Environment.NewLine,
                sharedOutputFolder.TrimEnd(new char[] { '/', '\\' }),
                tmpFolder.TrimEnd(new char[] { '/', '\\' })
            );

            batchContent += String.Format(
                //@"ping localhost -n 3" + Environment.NewLine +
                @"echo %time% > clavire_script_finished" + Environment.NewLine +
                @"xcopy clavire_script_finished {1}\ /z /s /e /c /i /h /r /y" + Environment.NewLine +
                @"cd {0}" + Environment.NewLine +
                @"cd .." + Environment.NewLine +
                //@"rmdir /s /q {0}" + Environment.NewLine +
                "",
                tmpFolder.TrimEnd(new char[] { '/', '\\' }),
                sharedOutputFolder.TrimEnd(new char[] { '/', '\\' })
            );


            int pauseLine = -1;
            Int32.TryParse(Config.AppSettings[DEBUG_PAUSE_PARAM_NAME] ?? "-1", out pauseLine);
            if (pauseLine >= 0)
            {
                var batchLines = batchContent.Replace("\r", "").Split(new[] { '\n' });
                string newBatchContent =
                    String.Join(Environment.NewLine, batchLines.Take(pauseLine)) + Environment.NewLine +
                    "pause" + Environment.NewLine +
                    String.Join(Environment.NewLine, batchLines.Skip(pauseLine));
                batchContent = newBatchContent;
            }


            IOProxy.Ftp.UploadFileContent(batchContent, jobFtpFolder, jobFileName);


            var rexService = GetREx(node.Services.ExecutionUrl);     // todo : close service client!
            int pid = rexService.Exec(taskId);

            logger.Info("Task {0} ({1}) started on pc {2}.{3} with pid = {4}", taskId, pack.Name, node.ResourceName, node.NodeName, pid);

            return pid + "\n" + node.NodeName;
        }

        public void Abort(TaskRunContext task)
        {
            logger.Warn("Abort is not implemented on windows controller!");
        }

        public IEnumerable<NodeStateResponse> GetNodesState(Resource resource)
        {
            var nodesResp = new List<NodeStateResponse>();
            var respLock = new object();

            PFX.Parallel.ForEach(resource.Nodes, node =>
            {
                var rexService = GetREx(node.Services.ExecutionUrl);
                rexService.InnerChannel.OperationTimeout = TimeSpan.FromSeconds(5);

                try
                {
                    bool isRunning = rexService.IsProcessRunning(1024);
                    rexService.Close();

                    lock (respLock)
                    {
                        nodesResp.Add(new NodeStateResponse(node.NodeName)
                        {
                            State = NodeState.Available
                        });
                    }
                }
                catch (Exception e)
                {
                    rexService.Abort();

                    logger.Debug("Node {0}.{1} is down: {2}", node.ResourceName, node.NodeName, e);

                    lock (respLock)
                    {
                        nodesResp.Add(new NodeStateResponse(node.NodeName)
                        {
                            State = NodeState.Down
                        });
                    }
                }
            });

            /*
            var nodes = resource.Nodes.Select(n => new NodeStateResponse(n.NodeName)
            {
                State = NodeState.Available                
            });
            */

            return nodesResp;
        }

        public void InstallByTicket(InstallationTicket ticket, ResourceNode node, string localAddress)
        {
            //var installer = new WindowsInstallerImpl();
        }
    }
}
