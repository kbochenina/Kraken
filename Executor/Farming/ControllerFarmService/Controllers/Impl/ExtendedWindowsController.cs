using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using Common;
using CommonDataTypes.RExService.Service.Entity.Info;
using ControllerFarmService.ExtFileService;
using ControllerFarmService.ExtRExService;
using ControllerFarmService.Installation;
using ControllerFarmService.Installation.Impl;
using ControllerFarmService.Installation.Interfaces;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;
using ControllerFarmService.RExService;
using MITP;
using ServiceProxies.ExtStatService;
using ServiceProxies.PackageInstallationService;
using ServiceProxies.RExInstallationService;
using PFX = System.Threading.Tasks;
using MITP.Entity;

namespace ControllerFarmService.Controllers.Impl
{
    public class ExtendedWindowsController : AbstractResourceController, IStatelessResourceController, IStatisticalCacheableController, IInstallationController
    {
        public virtual object Run(TaskRunContext task)
        {
            var node = GetNode(task);
            var pack = PackageByName(node, task.PackageName);

            var taskId = task.TaskId;

            var farmId = task.Resource.Controller.FarmId;

            var esService = GetExecuteServiceClient(node);

            var resorceHomeFolder = IncarnationParams.IncarnatePath(node.DataFolders.LocalFolder, task.TaskId, farmId, CopyPhase.None);

            PrepareEnviroment(esService, pack, resorceHomeFolder, farmId);

            CopyInputFiles(task, resorceHomeFolder);

            string cmdLine = String.Format(task.CommandLine, pack.AppPath, taskId);
            
            Log.Info("cmdline = " + cmdLine);

            var result = esService.ExecuteTaskOnFarm(taskId, farmId, cmdLine);

            Log.Info("Exec done. Job id = " + result);

            esService.Close();

            return result + "\n" + node.NodeName;
        }

        private void PrepareEnviroment(ExecuteServiceClient esService, PackageOnNode pack, string resorceHomeFolder, string farmId)
        {
            esService.CopyOnStartPaths(pack.CopyOnStartup, farmId, resorceHomeFolder);
        }

        private void CopyInputFiles(TaskRunContext task, string resorceHomeFolder)
        {
            var node = GetNode(task);
            
            Log.Info("Copying input files for task " + task.TaskId);

            foreach (var file in task.InputFiles)
            {
                var tmpFile = Path.GetTempFileName();
                try {
                    IOProxy.Storage.Download(file.StorageId, tmpFile);
                } catch(Exception exp)
                {
                    Log.Error("Error " + exp);
                }
                var fileOnResource = resorceHomeFolder.TrimEnd(new[] { '/', '\\' }) + "\\" + file.FileName;

                Log.Info("Copying file " + fileOnResource);
                
                UploadFile(node, fileOnResource, tmpFile, task.TaskId.ToString(), task.Resource.Controller.FarmId);

                File.Delete(tmpFile);
            }

            Log.Info(String.Format("Copying input files for task {0} finished.", task.TaskId));
        }

        private static ExecuteServiceClient GetExecuteServiceClient(ResourceNode node)
        {
            var binding = new BasicHttpBinding { MaxReceivedMessageSize = 16777216 };
            

            var address = new EndpointAddress("http://" + node.NodeAddress + "/RExService");

            return new ExecuteServiceClient(binding, address);
        }
        
        public void Abort(TaskRunContext task)
        {
            var node = GetNode(task);

            var esService = GetExecuteServiceClient(node);

            var providedWords = ((string)task.LocalId).Split(new[] { '\n' }); // todo : string -> string[]
            if (providedWords.Length > 2)
                Log.Warn(String.Format("Too many sections in provided task id for win PC: {0}", task.LocalId));

            string pid = providedWords[0];

            esService.StopTaskRunning(int.Parse(pid));
        }

        public TaskStateInfo GetTaskStateInfo(TaskRunContext task)
        {
            string[] providedWords = ((string)task.LocalId).Split(new[] { '\n' }); // todo : string -> string[]
            if (providedWords.Length > 2)
                Log.Warn(String.Format("Too many sections in provided task id for win PC: {0}", task.LocalId));

            string pid = providedWords[0];
            string nodeName = providedWords[1];

            var farmId = task.Resource.Controller.FarmId;

            
            var node = task.Resource.Nodes.First(n => n.NodeName == nodeName);
            Log.Info(String.Format("Getting task {0} info...", pid));
            var esClient = GetExecuteServiceClient(node);

            try
            {
                var isRunning = esClient.IsTaskRunning((int.Parse(pid)));
                esClient.Close();

                if (!isRunning)
                {
                    CopyOutputsToExchange(task, farmId);
                    return new TaskStateInfo(TaskState.Completed, "");
                }

                Log.Info(String.Format("task {0} running is : {1} ", pid, isRunning));

                return new TaskStateInfo();
            }
            catch (Exception e)
            {
                esClient.Abort();
                Log.Warn(String.Format(
                    "Exception while getting task '{0}' state (local id = {1}): {2}",
                    task.TaskId, task.LocalId, e
                ));

                throw;
            }
        }

        public IEnumerable<NodeStateResponse> GetNodesState(Resource resource)
        {
            var nodesResp = new List<NodeStateResponse>();
            var respLock = new object();

            PFX.Parallel.ForEach(resource.Nodes, node =>
            {
                var esClient = GetExecuteServiceClient(node);
                esClient.InnerChannel.OperationTimeout = TimeSpan.FromSeconds(5);

                try
                {
                    bool isRunning = esClient.IsTaskRunning(1024);
                    esClient.Close();

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
                    esClient.Abort();

                    Log.Warn(String.Format(
                        "Node {0}.{1} is down: {2}",
                        node.ResourceName, node.NodeName,
                        e
                    ));

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

        public void CopyOutputsToExchange(TaskRunContext task, string farmId)
        {
            ulong taskId = task.TaskId;
            var node = GetNode(task);
            var pack = PackageByName(node, task.PackageName);

            // temporary hack: files are not pushed from resource => using path from resource for scp copying
            string outFolderFromSystem = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromResource, taskId, CopyPhase.Out);
            //string outFolderFromSystem = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromSystem, taskId, CopyPhase.Out);
            bool copyingOutsToFtp = outFolderFromSystem.StartsWith("ftp://");
            if (copyingOutsToFtp && !outFolderFromSystem.EndsWith("/"))
                outFolderFromSystem += '/';
            if (!copyingOutsToFtp && !outFolderFromSystem.EndsWith("\\"))
                outFolderFromSystem += '\\';

            string clusterFolder = IncarnationParams.IncarnatePath((!String.IsNullOrEmpty(pack.LocalDir)) ? String.Format(pack.LocalDir, task.TaskId) : node.DataFolders.LocalFolder, taskId, CopyPhase.Out);
            if (!clusterFolder.EndsWith("\\"))
                clusterFolder += "\\";

            var exClient = GetExecuteServiceClient(node);

            string[] fileNames = exClient.GetAllFileNames(farmId, taskId);

            foreach(var output in task.ExpectedOutputFileNames)
            {
                Log.Info(output);
            }


            //IOProxy.Ftp.MakePath(ftpOutFolderFromSystem);
            var dirStructure = fileNames
                .Where(name => name.Contains('/') || name.Contains('\\')) // inside subdir
                .Select(name => name.Remove(name.LastIndexOfAny(new[] { '\\', '/' })))
                .Distinct()
                .Select(file => outFolderFromSystem + file)
                .Union(new[] { outFolderFromSystem });
            foreach (string dir in dirStructure)
            {
                if (copyingOutsToFtp)
                    IOProxy.Ftp.MakePath(dir);
                else
                {
                    Log.Debug("Creating dir " + dir);
                    Directory.CreateDirectory(dir);
                }
            }


            Log.Info("Copying output files");
            //System.Threading.Tasks.Parallel.ForEach(fileNames, (fileName) =>
            foreach (string fileName in fileNames)
            {
                //if (files.Contains(fileName)) 
                {
                    string tmpFile = Path.GetTempFileName();
                    try
                    {
                        Log.Info("Copying file " + clusterFolder + fileName);
                        //ScpGet( node, clusterFolder + fileName, tmpFile, false);
                        DownloadFile(node, clusterFolder + fileName, tmpFile, taskId, farmId);

                        if (copyingOutsToFtp)
                            IOProxy.Ftp.UploadLocalFile(tmpFile, outFolderFromSystem, fileName, shouldCreatePath: false);
                        else
                            File.Copy(tmpFile, outFolderFromSystem + fileName);

                        File.Delete(tmpFile);
                        Log.Info("File copied " + fileName);
                    }
                   
                    catch (Exception e)
                    {
                        Log.Warn(String.Format("Exception on file '{0}' copy: {1}", clusterFolder + fileName, e));
                    }
                }
            }//);
        }

        private void UploadFile(ResourceNode node, string remotePath, string localPath, string taskId, string farmId)
        {

            var binding = new BasicHttpBinding { MaxReceivedMessageSize = 16777216 };

            binding.MessageEncoding = WSMessageEncoding.Mtom;

            var address = new EndpointAddress("http://" + node.NodeAddress + "/FileService");

            var fsservice = new FileStreamServiceClient(binding, address);

            FileStream fs = null;

            try
            {
                fs = new FileStream(localPath, FileMode.Open, FileAccess.Read);

                var lgth = fs.Length;
                var chunkSize = 67108864;
                var itr = lgth / chunkSize;
                if (itr * chunkSize < lgth)
                {
                    itr++;
                }
                for (int i = 0; i < itr; i++)
                {
                    var sm = new MemoryStream();
                    var leftBytes = Math.Min(fs.Length - chunkSize * i, chunkSize);
                    sm.SetLength(leftBytes);
                    fs.Read(sm.GetBuffer(), 0, (int)leftBytes);
                    fs.Flush();

                    fsservice.SendFileChunkAsStream(new ChunkedFileStreamedMessage(chunkSize, farmId, remotePath, i,
                        taskId, sm));
                    //CopyFile(fs, i, chuckSize);
                    Log.Info("send " + i);
                    sm.Close();
                }

            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                throw;
            }
            finally
            {
                if (fs != null)
                {
                    fs.Close();
                }
            }
        }


        private void DownloadFile(ResourceNode node, string remotePath, string localPath, ulong taskId, string farmId)
        {
            var binding = new BasicHttpBinding { MaxReceivedMessageSize = 16777216 };

            binding.MessageEncoding = WSMessageEncoding.Mtom;

            var address = new EndpointAddress("http://" + node.NodeAddress + "/FileService");

            var fsservice = new FileStreamServiceClient(binding, address);
            
            int m = 0;
            bool finished = false;

            while (!finished)
            {

                const int bufferSize = 2048;
                var buffer = new byte[bufferSize];

                var chunkSize = 67108864;
                using (var outputStream = new FileStream(localPath, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    while (!finished)
                    {

                        //(chunkSize, farmId, remotePath, i, taskId, sm))
                        Stream resFileStream = fsservice.GetFileChunkAsStream(new GetFileChunkAsStreamRequest(remotePath,taskId,m,chunkSize)).GetFileChunkAsStreamResult;

                        //resFileStream.Position = m;
                        var bytesRead = resFileStream.Read(buffer, 0, bufferSize);
                        var readFilebytes = 0;
                        while (bytesRead > 0 && readFilebytes < chunkSize)
                        {
                            outputStream.Write(buffer, 0, bytesRead);
                            bytesRead = resFileStream.Read(buffer, 0, bufferSize);
                            readFilebytes += bufferSize;
                        }
                        if (bytesRead == 0 && readFilebytes < chunkSize)
                        {
                            finished = true;
                        }
                        m++;
                        Console.WriteLine(m);
                        resFileStream.Close();
                    }
                    outputStream.Close();
                }
            }
        }

        //todo nbutakov change
        private static StatisticsServiceClient GetStatisticsServiceClient(ResourceNode node)
        {
            var binding = new BasicHttpBinding { MaxReceivedMessageSize = 16777216 };

            var address = new EndpointAddress("http://" + node.NodeAddress + "/Statistics");

            return new StatisticsServiceClient(binding, address);
        }

        //todo it seems to be possible to do a factory for controllers and IStaticGetter
        public Dictionary<string, NodeInfo> GetCurrentResourceInfo(Resource resource)
        {
            Dictionary<string, NodeInfo> nodes = new Dictionary<string, NodeInfo>();

            foreach (var node in resource.Nodes)
            {
                using (var service = GetStatisticsServiceClient(node))
                {
                    var info = service.GetAllResourceInfoStartedWith(IntervalForNow());

                    if (info.Count() > 0)
                    {
                        nodes.Add(node.NodeName, info.Last());
                    }
                }
            }

            return nodes;
        }

        private static DateTime IntervalForNow()
        {
            return DateTime.Now.Subtract(new TimeSpan(0, 0, seconds: 5));
        }

        public Dictionary<string,List<ProcessStatInfo>> GetTaskInfoStartWith(ulong taskId, DateTime startTime, TaskRunContext task)
        {
            //todo ask about getting all nodes
            var node = GetNode(task);

            var client = GetStatisticsServiceClient(node);

            var result = new Dictionary<string, List<ProcessStatInfo>>();

            try
            {
                var data = client.GetAllTaskInfoStartedWith((int) taskId, startTime);
                result.Add(node.NodeName,data);
            }
            catch (FaultException ex)
            {
                //todo this Exception can be linked with state of a task
                //resolve it later
                Log.Error(" Exception while trying to get cacheable task info  " + ex.ToString());
                //throw ex;
            }
            catch (Exception ex)
            {
                Log.Error(" Exception while trying to get cacheable task info  " + ex.ToString());
                throw ex;
            }

            return result;
        }

        public Dictionary<string, List<NodeInfo>> GetResourceInfoStartWith(Resource resource, DateTime startTime)
        {
            Dictionary<string, List<NodeInfo>> nodes = new Dictionary<string, List<NodeInfo>>();

            foreach (var node in resource.Nodes)
            {
                using (var service = GetStatisticsServiceClient(node))
                {
                    var info = service.GetAllResourceInfoStartedWith(startTime);
                    nodes.Add(node.NodeName, new List<NodeInfo>(info));
                }
            }

            return nodes;
        }

        public void InstallByTicket(InstallationTicket ticket, ResourceNode node, string localAddress)
        {
            var installer = new WindowsInstallerImpl(this.UploadFile);
            installer.InstallByTicket(ticket, node, localAddress);
        }
    }
}
