using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Threading;
using System.Linq;

using SSH = Tamir.SharpSsh;


namespace MITP
{
    public class HadoopProvider : AbstractResourceProvider
    {
        private const string User = "clavire";
        private const string Password = "7777777";

        private abstract class HadoopCommands
        {
            public const string Run = "nohup {0} &";
            public const string GetTaskState = "ps ax o pid | grep {0}";
            public const string Abort = "clavire.sh stop /user/yakushev/databases/test";
        }

        private static object _hadoopLock = new object();
        private static bool _nodeUsed = false;


        public override string Name
        {
            get { return "Hadoop"; }
        }

        private static string SshRun(string host, string command, bool ignoreErrors = false)
        {
            var ssh = new SSH.SshExec(host.Split(':')[0], User, Password);
            if (host.Contains(":"))
            {
                ssh.Connect(Int32.Parse(host.Split(':')[1]));
            }
            else
            {
                ssh.Connect();
            }

            string sshOut = "";
            string sshErr = "";
            string sshCommand = command;
            int sshResult = ssh.RunCommand(sshCommand, ref sshOut, ref sshErr);
            ssh.Close();

            if (!String.IsNullOrWhiteSpace(sshErr) && !ignoreErrors)
                throw new Exception(String.Format("Ssh execution error. Command: \"{0}\". Code: {1}, StdOut: {2}, StdErr: {3}", sshCommand, sshResult, sshOut, sshErr));

            return sshOut;
        }

        private static string SshShell(string host, string command)
        {
            var shell = new SSH.SshShell(host.Split(':')[0], User, Password);
            if (host.Contains(":"))
            {
                shell.Connect(Int32.Parse(host.Split(':')[1]));
            }
            else
            {
                shell.Connect();
            }

            shell.Expect();

            string result = "";

            foreach (string cmd in command.Split('\n'))
            {
                shell.WriteLine(command);
                Thread.Sleep(1000);
                result += shell.Expect();
            }
            

            shell.Close();

            return result;
        }

        private static void ScpPut(string host, string from, string to)
        {
            var scp = new SSH.Scp(host.Split(':')[0], User, Password);
            if (host.Contains(":"))
            {
                scp.Connect(Int32.Parse(host.Split(':')[1]));
            }
            else
            {
                scp.Connect();
            }

            scp.Recursive = true;
            scp.Put(from, to);
            scp.Close();
        }

        public override NodeState GetNodeState(ResourceNode node)
        {
            lock (_hadoopLock)
            {
                return new NodeState()
                {
                    CoresAvailable = _nodeUsed ? 0 : node.CoresCount
                };
            }
        }

        public override Tuple<TaskState, string> GetTaskState(ulong taskId, string providedTaskId, Resource resource, IEnumerable<NodeConfig> nodesConfig)
        {
            lock (_hadoopLock)
            {
                var node = GetDefaultNodeSettings(resource, nodesConfig);
                string host = node.Services.ExecutionUrl;

                string state = SshRun(host, String.Format(HadoopCommands.GetTaskState, providedTaskId));

                if (state != "")
                {
                    return new Tuple<TaskState, string>(TaskState.Started, "");
                }
                else
                {
                    _nodeUsed = false;
                    return new Tuple<TaskState, string>(TaskState.Completed, "");
                }              
            }
        }

        public override void Abort(string providedTaskId, Resource resource, IEnumerable<NodeConfig> nodesConfig)
        {
            lock (_hadoopLock)
            {
                var node = GetDefaultNodeSettings(resource, nodesConfig);
                string host = node.Services.ExecutionUrl;

                SshRun(host, HadoopCommands.Abort);
                _nodeUsed = false;                
            }
        }

        public override string Run(ulong taskId, IncarnationParams incarnation, Resource resource, IEnumerable<NodeConfig> nodesConfig)
        {
            lock (_hadoopLock)
            {
                var node = GetDefaultNodeSettings(resource, nodesConfig);
                var pack = node.PackageByName(incarnation.PackageName);                

                string workDir = IncarnationParams.IncarnatePath(node.DataFolders.LocalFolder, taskId, CopyPhase.None);
                string workScriptPath = workDir + "hadoop.sh";
                string workScriptInternalPath = workDir + "hadoop_internal.sh";

                string exchangeInDir = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromResource, taskId, CopyPhase.In);
                string exchangeOutDir = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromResource, taskId, CopyPhase.Out);

                string identity = exchangeInDir.Split('@')[0].Replace("ftp://", "");
                string user = identity.Split(':')[0];
                string pass = identity.Split(':')[1];

                string hostAndPath = exchangeInDir.Split('@')[1];

                string ftpHost = hostAndPath.Split(new char[] { '/' }, 2)[0];
                string ftpInPath = hostAndPath.Split(new char[] { '/' }, 2)[1];

                string ftpOutPath = exchangeOutDir.Split('@')[1].Split(new char[] { '/' }, 2)[1];

                //pack.Params.

                var cmd = new StringBuilder();
                cmd.AppendFormat("cd " + workDir + "\n");
                cmd.AppendFormat(String.Format(HadoopCommands.Run, workScriptInternalPath) + "\n");
                cmd.AppendFormat("echo $!" + "\n");
                string tempPath = Path.GetTempFileName();
                File.WriteAllText(tempPath, cmd.ToString());

                var cmd_internal = new StringBuilder();
                cmd_internal.AppendFormat("ftp -n {0} << END_SCRIPT\n", ftpHost);
                cmd_internal.AppendFormat("quote User {0}\n", user);
                cmd_internal.AppendFormat("quote PASS {0}\n", pass);
                foreach (TaskFileDescription fileDesc in incarnation.FilesToCopy)
                {
                    cmd_internal.AppendFormat("get {0}{2} {1}{2}\n", ftpInPath, workDir, fileDesc.FileName);
                }
                cmd_internal.AppendFormat("quit" + "\n");
                cmd_internal.AppendFormat("END_SCRIPT" + "\n");
                cmd_internal.AppendFormat(String.Format(incarnation.CommandLine, IncarnationParams.IncarnatePath(pack.AppPath, taskId, CopyPhase.None).TrimEnd('/')) + "\n");
                cmd_internal.AppendFormat("ftp -n {0} << END_SCRIPT\n", ftpHost);
                cmd_internal.AppendFormat("quote User {0}\n", user);
                cmd_internal.AppendFormat("quote PASS {0}\n", pass);
                cmd_internal.AppendFormat("mkdir {0}\n", ftpOutPath);
                foreach (string fileName in incarnation.ExpectedOutputFileNames)
                {
                    cmd_internal.AppendFormat("put {0}{2} {1}{2}\n", workDir, ftpOutPath, fileName);
                }               
                cmd_internal.AppendFormat("quit" + "\n");
                cmd_internal.AppendFormat("END_SCRIPT" + "\n");
                                
                string tempPathInternal = Path.GetTempFileName();
                File.WriteAllText(tempPathInternal, cmd_internal.ToString());                

                string host = node.Services.ExecutionUrl;

                SshRun(host, "mkdir " + workDir, true); // Need check, del
                SshRun(host, "rm " + workDir + "*", true); // Need check                

                foreach (string path in pack.CopyOnStartup) // todo : internal_script maybe?
                {
                    string toCopy = (path.EndsWith("/")? path + "*": path); // 'folder/*' needed for contents copy
                    // ^^^^^ doesn't work everywhere

                    SshRun(host, "cp -fpR " + toCopy + "/* " + workDir); 
                }

                /*
                SshRun(host, "cp " + "/home/clavire/hadrawler/clavire.sh " + workDir); // Need check, del 
                SshRun(host, "cp " + "/home/clavire/hadrawler/hadoop2.conf " + workDir); // Need check, del 
                */

                ScpPut(host, tempPath, workScriptPath);// Need del
                SshRun(host, "chmod 700 " + workScriptPath);

                ScpPut(host, tempPathInternal, workScriptInternalPath);// Need del
                SshRun(host, "chmod 700 " + workScriptInternalPath);

                File.Delete(tempPath);
                File.Delete(tempPathInternal);

                string pid = SshShell(host, workScriptPath); // проверка на то запустилось ли
                
                _nodeUsed = true;
                return pid.Split('\n')[2].TrimEnd('\r');
            }
        }
    }
}
