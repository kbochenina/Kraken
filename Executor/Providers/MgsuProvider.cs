using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Linq;
using Ssh = Tamir.SharpSsh;
using Ssh2 = Renci.SshNet;

namespace MITP
{
    public class MgsuController : AbstractResourceProvider
    {
        private static Dictionary<string, bool> _nodeUsed = new Dictionary<string, bool>();

        private readonly object _lock = new object();
        public object Lock
        {
            get { return _lock; }
        }

        public override string Name
        {
            get { return "Linux"; }
        }

        public ResourceNode GetNode(Resource resource, IEnumerable<NodeConfig> nodesConfig)
        {
            if (nodesConfig.Count() > 1)
            {
                Log.Warn(String.Format(
                    "More than one node scheduled, but resource provider doesn’t support different settings on different nodes. Using settings from node '{0}'.",
                    nodesConfig.First().NodeName
                ));
            }

            var node = resource.Nodes.First(n => n.NodeName == nodesConfig.First().NodeName);
            return node;
        }

        private abstract class SshCommands
        {
            public const string Abort = "qdel {0}";
            public const string GetTaskState = "qstat -f {0}";
            public const string GetNodeState = "pbsnodes -x";
            public const string Ls = "ls -F --format=commas";
        }

        private string SshExec(ResourceNode node, string command, string args = "")
        {
            lock (_lock)
            {
                int sshResult = 0;
                string sshOut = "";
                string sshErr = "";
                string sshCommand = command + " " + args;

                var sshExec = new Ssh2
                    .SshClient(node.NodeAddress, node.Credentials.Username, node.Credentials.Password);
                    //.SshExec(node.NodeAddress, node.Credentials.Username, node.Credentials.Password);

                try
                {
                    sshExec.Connect();
                    //sshResult = sshExec.RunCommand(sshCommand, ref sshOut, ref sshErr);
                    var ssh = sshExec.RunCommand(sshCommand);
                    ssh.Execute();

                    sshResult = ssh.ExitStatus;
                    sshErr = ssh.Error;
                    sshOut = ssh.Result;
                }
                catch (Exception e)
                {
                    Log.Warn(e.Message);
                    throw;
                }
                finally
                {
                    if (sshExec.IsConnected)
                        sshExec.Disconnect();
                }

                sshErr = sshErr.Replace('.', ' '); // Cert creation emits many dots
                if (!String.IsNullOrWhiteSpace(sshErr))
                    throw new Exception(String.Format("Ssh execution error. Command: \"{0}\". Code: {1}, StdOut: {2}, StdErr: {3}", sshCommand, sshResult, sshOut, sshErr));

                return sshOut;
            }
        }

        private void ScpCopy(ResourceNode node, string remotePath, string localPath)
        {
            var scp = new Ssh.Scp(node.NodeAddress, node.Credentials.Username, node.Credentials.Password);

            scp.Connect();
            scp.Recursive = true;
            scp.Put(localPath, remotePath);
            scp.Close();
        }

        private void ScpGet(ResourceNode node, string remotePath, string localPath)
        {
            var scp = new Ssh.Scp(node.NodeAddress, node.Credentials.Username, node.Credentials.Password);

            scp.Connect();
            scp.Recursive = true;
            scp.Get(remotePath, localPath);
            scp.Close();
        }

        public override string Run(ulong taskId, IncarnationParams incarnation, Resource resource, IEnumerable<NodeConfig> nodesConfig)
        {
            lock (_lock)
            {
                int coresToUse = nodesConfig.Sum(conf => conf.Cores);
                var node = GetDefaultNodeSettings(resource, nodesConfig);
                var pack = node.PackageByName(incarnation.PackageName);

                //string ftpFolder = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromSystem, taskId, CopyPhase.In);
                //string jobFtpFolder = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromSystem, taskId, CopyPhase.None);
                string ftpInputFolder  = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromResource, taskId, CopyPhase.In);
                string ftpOutputFolder = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromResource, taskId, CopyPhase.Out);
                string clusterHomeFolder = IncarnationParams.IncarnatePath(node.DataFolders.LocalFolder, taskId, CopyPhase.None);

                //IOProxy.Ftp.MakePath(ftpInputFolder);
                //IOProxy.Ftp.MakePath(ftpOutputFolder);

                try
                {
                    SshExec(node, "mkdir " + clusterHomeFolder);
                }
                catch (Exception e)
                {
                    Log.Warn(e.Message);
                }

                string fileNames = ""; //String.Join(" ", incarnation.FilesToCopy.Select(f => f.FileName));
                foreach (var file in incarnation.FilesToCopy)
                {
                    string tmpFile = System.IO.Path.GetTempFileName();
                    IOProxy.Storage.Download(file.StorageId, tmpFile);

                    string fileOnCluster = clusterHomeFolder.TrimEnd(new[] {'/', '\\'}) + "/" + file.FileName;
                    fileNames += " " + fileOnCluster;

                    ScpCopy(node, fileOnCluster, tmpFile);
                    System.IO.File.Delete(tmpFile);
                }

                string cmdLine = String.Format(incarnation.CommandLine, pack.AppPath, "clavire" + taskId.ToString(), fileNames.Trim());

                string result = SshExec(node, cmdLine);
                string jobId = result.Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries).First();

                _nodeUsed[node.NodeName] = true;

                return jobId;
            }
        }

        public override void Abort(string providedTaskId, Resource resource, IEnumerable<NodeConfig> nodesConfig)
        {
            lock (_lock)
            {
                foreach (var config in nodesConfig)
                {
                    _nodeUsed[config.NodeName] = false;   // todo : AND WHAT ABOUT DIFFERENT RESOURCES???!!!!!!!111
                }
            }
        }

        public override Tuple<TaskState, string> GetTaskState(ulong taskId, string providedTaskId, Resource resource, IEnumerable<NodeConfig> nodesConfig)
        {
            lock (_lock)
            {
                string result = SshExec(resource.Nodes.First(), SshCommands.GetTaskState, providedTaskId).ToLowerInvariant();

                if (result.Contains("job_state = R") || result.Contains("job_state = Q"))
                    return new Tuple<TaskState, string>(TaskState.Started, result);
                // esle if (Aborted, Failed)
                else
                {
                    var node = GetDefaultNodeSettings(resource, nodesConfig);
                    string ftpOutFolderFromSystem = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromSystem, taskId, CopyPhase.Out);
                    string clusterFolder = "share/ANSYS_JOBS/clavire" + taskId.ToString() + "_0001/";

                    IOProxy.Ftp.MakePath(ftpOutFolderFromSystem);

                    var fileNames = 
                        SshExec(node, SshCommands.Ls, clusterFolder)
                            .Split(new[] {", "}, StringSplitOptions.RemoveEmptyEntries)
                            .Where(st => !st.Contains("/"))
                            .Select(st => st.Replace("*", "").Replace("|", ""))
                            .Where(st => !st.Contains(".rst") && !st.Contains(".err") && !st.Contains(".esav"));

                    foreach (string fileName in fileNames)
                    {
                        string tmpFile = System.IO.Path.GetTempFileName();
                        ScpGet(node, clusterFolder + fileName, tmpFile);
                        IOProxy.Ftp.UploadLocalFile(tmpFile, ftpOutFolderFromSystem, fileName);
                        System.IO.File.Delete(tmpFile);
                    }

                    _nodeUsed[node.NodeName] = false;

                    return new Tuple<TaskState, string>(TaskState.Completed, result);
                }

            }
        }

        public override NodeState GetNodeState(ResourceNode node)
        {
            lock (_lock)
            {
                if (!_nodeUsed.ContainsKey(node.NodeName))
                    _nodeUsed[node.NodeName] = false;

                return new NodeState()
                {
                    CoresAvailable = (_nodeUsed[node.NodeName]) ? 0 : node.CoresCount,
                    CoresCount = node.CoresCount,
                };
            }
        }
    }
}