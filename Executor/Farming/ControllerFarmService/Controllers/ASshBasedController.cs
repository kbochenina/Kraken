using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using CommonDataTypes.RExService.Service.Entity.Info;
using ControllerFarmService.Controllers.Entity;
using ControllerFarmService.Controllers.Error;
using ControllerFarmService.Installation;
using ControllerFarmService.Installation.Interfaces;
using MITP;
using MITP.Entity;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using Renci.SshNet;
using Renci.SshNet.Security;
using ServiceProxies.ExtStatService;
using ServiceProxies.PackageInstallationService;
using ServiceProxies.SchedulerService;
using Tamir.SharpSsh.java;
using Tamir.SharpSsh.java.lang;
using Ssh = Tamir.SharpSsh;
using Ssh2 = Renci.SshNet;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;
using Exception = System.Exception;
//using Resource = ControllerFarmService.ResourceBaseService.Resource;
using Resource = ServiceProxies.ResourceBaseService.Resource;
using String = System.String;
using Thread = System.Threading.Thread;

namespace ControllerFarmService
{
    public class ASshBasedController : AbstractResourceController, IStatisticalController, IInstallationController
    {
        private const string SSH_FIND_COMMAND = "find -type f -size -500M"; 
            // '-500M' == 'less than 500 MB'
            // todo : REMOVE RESTRICTION FOR FILES MORE THAN 500 MB IN SIZE

        private SshConnectionPool _sshPool = new SshConnectionPool();
        private CopierConnectionPool _copierPool = new CopierConnectionPool();
        public static String ClavireStartFileName = "clavire_start_time.txt";
        public static String ClavireFinishFileName = "clavire_finish_time.txt";
        
        protected string SshExec(ResourceNode node, string command, string args = "", AErrorResolver errorResolver = null)
        {
            int sshResult = 0;
            string sshOut = "";
            string sshErr = "";
            string sshCommand = command + " " + args;

            var sshCommands = new[] {sshCommand};

            if (sshCommand.Contains('\n'))
            {
                sshCommands = sshCommand.Split('\n');
            }
            /*
            var connectionInfo = new Ssh2.ConnectionInfo(node.Services.ExecutionUrl, node.Credentials.Username, new Ssh2.AuthenticationMethod[] { 
                new Ssh2.KeyboardInteractiveAuthenticationMethod(node.Credentials.Username), 
                new Ssh2.PasswordAuthenticationMethod(node.Credentials.Username, node.Credentials.Password)
            });
            */


            var sshExec = _sshPool.GetSshSession(true, node);
            
                //Ssh.SshExec(node.NodeAddress, node.Credentials.Username, node.Credentials.Password);
            //Log.Info("Ssh command to execute : " + command + " ");
            try
            {
                foreach (var s in sshCommands)
                {
                    if (!s.Contains("pbsnodes") && !s.Contains("qstat"))
                        Log.Info("Ssh command to execute: " + s);

                    /*
                    sshExec.Connect();
                    sshResult = sshExec.RunCommand(sshCommand, ref sshOut, ref sshErr);
                    /**/
                    
                    var ssh = sshExec.RunCommand(s); // todo : using (var ssh = new...)
                    //ssh.Execute();

                    sshResult = ssh.ExitStatus;
                    sshErr = ssh.Error;
                    sshOut = ssh.Result;

                    if (!String.IsNullOrWhiteSpace(sshErr))
                    {
                        break;
                    }
                }

                /**/
            }
            catch (Exception e)
            {
                Log.Warn(e.Message);
                throw;
            }
            finally
            {
                /*/*
                sshExec.Close();
                /*
                if (sshExec.IsConnected)
                    sshExec.Disconnect();
                /***/
                _sshPool.PushSession(sshExec);
            }

            //sshErr = sshErr.Replace('.', ' '); // Cert creation emits many dots
            if (errorResolver == null && sshResult != 0 /*!String.IsNullOrWhiteSpace(sshErr)*/)
            {
                throw new Exception(String.Format("Ssh execution error. Command: \"{0}\". Code: {1}, StdOut: {2}, StdErr: {3}", sshCommand, sshResult, sshOut, sshErr));
            }
            if (errorResolver != null && !String.IsNullOrWhiteSpace(sshErr))
            {
                var resIn = new Dictionary<String, Object>();
                resIn[AErrorResolver.SSH_RESULT] = sshOut;
                resIn[AErrorResolver.SSH_EXIT_CODE] = sshResult;
                resIn[AErrorResolver.SSH_COMMAND] = sshCommand;
                resIn[AErrorResolver.SSH_ERROR] = sshErr;

                errorResolver.Resolve(resIn);
            }

            if (!command.Contains("pbsnodes") && !command.Contains("qstat"))
                Log.Info("ssh execution result : " + sshOut);

            return sshOut;
        }

        protected void UploadFile(ResourceNode node, string remotePath, string localPath)
        {
            SecureCopier copier = null;

            try
            {
                copier = _copierPool.GetSshSession(true, node);
                copier.UploadFile(node, remotePath, localPath);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                if (copier != null)
                    _copierPool.PushSession(copier);
            }
        }

        protected void DownloadFile(ResourceNode node, string remotePath, string localPath)
        {
            SecureCopier copier = null;

            try
            {
                copier = _copierPool.GetSshSession(true, node);
                copier.DownloadFile(node, remotePath, localPath);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                if (copier != null)
                    _copierPool.PushSession(copier);
            }
        }

        public string CopyInputFiles(TaskRunContext task, out string fileNames)
        {
            var node = GetNode(task);

            //string ftpFolder = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromSystem, taskId, CopyPhase.In);
            //string jobFtpFolder = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromSystem, taskId, CopyPhase.None);
            //string ftpInputFolder = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromResource, taskId, CopyPhase.In);
            //string ftpOutputFolder = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromResource, taskId, CopyPhase.Out);
            string clusterHomeFolder = IncarnationParams.IncarnatePath(node.DataFolders.LocalFolder, task.TaskId, CopyPhase.None);

            //IOProxy.Ftp.MakePath(ftpInputFolder);
            //IOProxy.Ftp.MakePath(ftpOutputFolder);

            try
            {
                Log.Info(Thread.CurrentThread.ManagedThreadId + " entered.");

                SshExec(node, "mkdir " + clusterHomeFolder);

                Log.Info(Thread.CurrentThread.ManagedThreadId + " exited.");
            }
            catch (Exception e)
            {
                Log.Warn(e.ToString());
            }

            Log.Info("Copying input files for task " + task.TaskId.ToString());
            fileNames = ""; //String.Join(" ", incarnation.FilesToCopy.Select(f => f.FileName));
            foreach (var file in task.InputFiles)
            {
                string tmpFile = Path.GetTempFileName();
                IOProxy.Storage.Download(file.StorageId, tmpFile);

                string fileOnCluster = clusterHomeFolder.TrimEnd(new[] { '/', '\\' }) + "/" + file.FileName;
                fileNames += " " + fileOnCluster;

                Log.Info("Copying file " + fileOnCluster);
                //ScpCopy(node, fileOnCluster, tmpFile);
                UploadFile(node, fileOnCluster, tmpFile);
                File.Delete(tmpFile);
            }

            return clusterHomeFolder;
        }

        public void CopyOutputsToExchange(TaskRunContext task)
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
            if (!clusterFolder.EndsWith("/"))
                clusterFolder += "/";

            //var files = ImproveFiles(task.Incarnation.ExpectedOutputFileNames);
            /*                var fileNames =
                                SshExec(node, SshPbsCommands.Find, clusterFolder)
                                    .Split(new[] { ", ", "," }, StringSplitOptions.RemoveEmptyEntries)
                                    .Where(st => !st.Contains("/"))
                                    .Select(st => st.Replace("*", "").Replace("|", "").Replace("\n",""))
                                    .Where(st => !st.Contains(".rst") && !st.Contains(".err") && !st.Contains(".esav"));*/

            var fileNames = SshExec(node, "cd " + clusterFolder + "; " + SSH_FIND_COMMAND, "")
                            .Replace("./", "/").Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
                            .Where(st => !st.Contains(".rst") /*&& !st.Contains(".err")*/ && !st.Contains(".esav"))
                            .Select(st => st.Trim(new[] { '/', '\\' }));

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
                        //ScpGet(node, clusterFolder + fileName, tmpFile, false);
                        DownloadFile(node, clusterFolder + fileName, tmpFile);

                        if (copyingOutsToFtp)
                            IOProxy.Ftp.UploadLocalFile(tmpFile, outFolderFromSystem, fileName, shouldCreatePath: false);
                        else
                            File.Copy(tmpFile, outFolderFromSystem + fileName);

                        File.Delete(tmpFile);
                        Log.Info("File copied " + fileName);
                    }
                    catch (Ssh.SshTransferException e)
                    {
                        Log.Warn(String.Format("During coping file {0} for task {1} from error was happend: {2}", fileName, taskId, e)); // todo : lolwut?
                    }
                    catch (Exception e)
                    {
                        Log.Warn(String.Format("Exception on file '{0}' copy: {1}", clusterFolder + fileName, e));
                    }
                }
            }//);
        }


        //todo nbutakov change
        public NodeInfo GetCurrentResourceInfo(ResourceNode node)
        {
//            Dictionary<string, NodeInfo> resourceInfo = new Dictionary<string, NodeInfo>();
            string sshCommand = GetNodeInfoCommand();

//            foreach (var node in resource.Nodes)
//            {
                var data = SshExec(node, sshCommand);

                //resourceInfo.Add(node.NodeName, ObtainInfo(data));
//            }

            return (NodeInfo)ObtainInfo(data, typeof(NodeInfo)); //resourceInfo;
        }

        public ProcessStatInfo GetCurrentTaskInfo(TaskRunContext task)
        {
            var node = GetNode(task);
            var result = SshExec(node, GetTaskInfoCommand());

            return (ProcessStatInfo) ObtainInfo(result, typeof (ProcessStatInfo));
        }

        protected string GetNodeInfoCommand()
        {
            return SshStatisticCommand.GetInfo;
        }

        protected object ObtainInfo(string data, Type type)
        {
            List<Tuple<string, string>> keyValues = data.Split(';').Where(x => !(string.IsNullOrEmpty(x) || x.Equals("\n"))).Select(x =>
            {
                string[] keyValue = x.Split('=');
                return new Tuple<string, string>(keyValue[0], keyValue[1]);
            }).ToList();

            return constructInfo(keyValues, type);
        }

        private object constructInfo(List<Tuple<string, string>> keyValues, Type type)
        {
            var result = Activator.CreateInstance(type);

            foreach (var keyValue in keyValues)
            {
                var property = type.GetProperty(keyValue.Item1);
                object value = keyValue.Item2;

                if (property.PropertyType.FullName.Contains("Int64"))
                {
                    value = long.Parse(keyValue.Item2);
                }
                else if (property.PropertyType.FullName.Contains("Double"))
                {
                    value = double.Parse(keyValue.Item2);
                }
                else if (property.PropertyType.FullName.Contains("Boolean"))
                {
                    value = bool.Parse(keyValue.Item2);
                }
                else if (property.PropertyType.FullName.Contains("DateTime"))
                {
                    value = DateTime.Parse(keyValue.Item2);
                }

                property.SetValue(result, value, null);
            }

            return result;
        }

        protected string GetTaskInfoCommand()
        {
            return SshStatisticCommand.GetTaskInfo;
        }

        protected static class SshStatisticCommand
        {
            //todo just a stub
            public const string GetInfo = "DATE=$(date +%F' '%H:%M:%S);echo 'DiskAvailableFreeSpace=100500;DiskUsage=40,0;MemAvailable=15,0;MemUsage=85,0;Net=25,0;Offline=false;ProcUsage=75,5;SandBoxTotalSize=100000;TimeSnapshot='$DATE';';";
            public const string GetTaskInfo = "DATE=$(date +%F' '%H:%M:%S);echo 'TotalProcTime=100500;ProcUsage=40;PhysicalMem=15;Net=850;FileCount=1400;WorkDirSize=300;TimeSnapshot='$DATE';';";
        }

        protected static class SshInstallationCommand
        {
            //todo remake this stub later.
            public const string InstallUploadedPackage = "mkdir {1}/{2}; tar -xf {0} -C {1}/{2}; {1}/{2}/make.sh;";
            //"echo 'Hello, World!'";
        }

        public void InstallByTicket(InstallationTicket ticket, ResourceNode node, string localAddress)
        {
                   var fileName = Path.GetFileName(localAddress);
                   var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(localAddress);
                   var remotePath = Path.Combine(ticket.FolderToInstall, fileName).Replace('\\','/');
                   this.UploadFile(node, remotePath, localAddress);

                   string result = SshExec(node,
                       string.Format(SshInstallationCommand.InstallUploadedPackage, remotePath, ticket.FolderToInstall, fileNameWithoutExtension));
        }      
    }
}

