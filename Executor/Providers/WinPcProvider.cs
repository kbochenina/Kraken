using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace MITP
{
    public class WinPcProvider : AbstractResourceProvider
    {
        private static object _pcLock = new object();
        private static Dictionary<string, bool> _nodeUsed = new Dictionary<string, bool>();
        private static bool _eulaAccepted = false;

        private const string PS_PID_START_MSG = "with process ID ";
        private const string PS_PROCESS_NOT_FOUND_MSG = " was not found on ";

        private void CheckNodeNamePresence(string nodeName)
        {
            lock (_pcLock)
            {
            }
        }

        [Obsolete("Don't use this method: use command line parameters instead.")]
        private void AcceptPsToolsEula()
        {
            lock (_pcLock)
            {
                if (!_eulaAccepted)
                {
                    Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\Software\Sysinternals\PsExec", "EulaAccepted", 1, Microsoft.Win32.RegistryValueKind.DWord);
                    Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\Software\Sysinternals\PsList", "EulaAccepted", 1, Microsoft.Win32.RegistryValueKind.DWord);
                    _eulaAccepted = true;
                    Log.Info("PsTools EULA accepted");
                }
            }
        }

        public override string Name
        {
            get { return CONST.Providers.WinPc; }
        }

        public override NodeState GetNodeState(ResourceNode node)
        {
            lock (_pcLock)
            {
                //CheckNodeNamePresence(node.NodeName);
                if (!_nodeUsed.ContainsKey(node.NodeName))
                    _nodeUsed[node.NodeName] = false;

                return new NodeState()
                {
                    CoresAvailable = (_nodeUsed[node.NodeName])? 0: node.CoresCount
                };
            }
        }

        public override Tuple<TaskState, string> GetTaskState(ulong taskId, string providedTaskId, Resource resource, IEnumerable<NodeConfig> nodesConfig)
        {
            lock (_pcLock)
            {
                try
                {
                    string[] providedWords = providedTaskId.Split(new char[] { '\n' });
                    if (providedWords.Length > 2)
                        Log.Warn(String.Format("Too many sections in provided task id for win PC: {0}", providedTaskId));

                    string pid = providedWords[0];
                    string nodeName = providedWords[1];
                    var node = resource.Nodes.First(n => n.NodeName == nodeName);

                    using (var rexService = EntryPointProxy.GetREx(node.Services.ExecutionUrl))
                    {
                        bool isRunning = rexService.IsProcessRunning(Int32.Parse(pid));

                        if (!isRunning)
                        {
                            _nodeUsed[node.NodeName] = false;
                            return Tuple.Create(TaskState.Completed, "");
                        }

                        return Tuple.Create(TaskState.Started, "");
                    }
                }
                catch (Exception e)
                {
                    Log.Warn(String.Format(
                        "Exception while getting task state (provided id = {0}): {1}\n{2}",
                        providedTaskId, e.Message, e.StackTrace
                    ));

                    return Tuple.Create(TaskState.Started, "");
                }
            }
        }

        private Tuple<TaskState, string> GetTaskStateByTaskList(string providedTaskId, Resource resource)
        {
            lock (_pcLock)
            {
                //AcceptPsToolsEula();

                string[] providedWords = providedTaskId.Split( new char[] {'\n'} );
                if (providedWords.Length > 2)
                    Log.Warn(String.Format("Too many sections in provided task id for win PC: {0}", providedTaskId));

                string pid = providedWords[0];
                string nodeName = providedWords[1];
                var node = resource.Nodes.First(n => n.NodeName == nodeName);

                // todo : state == defined?

                //string taskIdString = taskId.ToString();
                var taskListProcess = new Process();
                taskListProcess.StartInfo.UseShellExecute = false;
                taskListProcess.StartInfo.RedirectStandardOutput = true;
                taskListProcess.StartInfo.RedirectStandardError = true;

                /*
                taskListProcess.StartInfo.FileName = CONST.Path.PsList;
                taskListProcess.StartInfo.Arguments = String.Format(
                    @"\\{0} -u {1} -p {2} {3}",
                    node.NodeAddress, node.Credentials.Username, node.Credentials.Password, pid
                );
                */

                taskListProcess.StartInfo.FileName = "tasklist.exe";
                taskListProcess.StartInfo.Arguments = String.Format(
                    @"/s {0} /u {1} /p {2} /fo csv /nh /fi " + "\"PID eq {3}\"",
                    node.NodeAddress, node.Credentials.Username, node.Credentials.Password, pid
                );
                taskListProcess.Start();

                string table = taskListProcess.StandardOutput.ReadToEnd();
                taskListProcess.WaitForExit();

                Log.Debug(String.Format("Process table for machine {0}:\n{1}", node.NodeAddress, table));

                if (!String.IsNullOrWhiteSpace(table))
                {
                    int PID_COLUMN_NUM = 1;
                    var pids = table
                        .Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries) // rows
                        .Select(row => row
                            .Split(new char[] { ',' }) // cols
                            .ElementAt(PID_COLUMN_NUM) // "pid"
                            .Trim(new char[] { ' ', '\t', '\"' }) // pid
                        );

                    if (!pids.Contains(pid))
                    {
                        _nodeUsed[node.NodeName] = false;
                        return Tuple.Create(TaskState.Completed, "");
                    }
                }

                return Tuple.Create(TaskState.Started, "");

                /*
                string state = taskListProcess.StandardOutput.ReadToEnd() + " " + taskListProcess.StandardError.ReadToEnd();
                taskListProcess.WaitForExit();

                if (state.Contains(PS_PROCESS_NOT_FOUND_MSG))
                {
                    _nodeUsed[node.NodeName] = false;
                    return Tuple.Create(TaskState.Completed, "");
                }

                return Tuple.Create(TaskState.Started, state);
                */
            }
        }

        public override string Run(ulong taskId, IncarnationParams incarnation, Resource resource, IEnumerable<NodeConfig> nodesConfig)
        {
            lock (_pcLock)
            {
                //AcceptPsToolsEula();

                int coresToUse = nodesConfig.Sum(conf => conf.Cores);
                var node = GetDefaultNodeSettings(resource, nodesConfig);

                if (_nodeUsed[node.NodeName])
                    throw new Exception(String.Format("Could not run task {0} on node {1}: node used by another task", taskId, node.NodeName));

                string ftpFolder = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromSystem, taskId, CopyPhase.In);
                string jobFtpFolder = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromSystem, taskId, CopyPhase.None);
                string sharedInputFolder  = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromResource, taskId, CopyPhase.In);
                string sharedOutputFolder = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromResource, taskId, CopyPhase.Out);
                string tmpFolder = IncarnationParams.IncarnatePath(node.DataFolders.LocalFolder, taskId, CopyPhase.None);

                IOProxy.Ftp.MakePath(ftpFolder);
                IOProxy.Ftp.MakePath(jobFtpFolder);

                string jobFileName = "job_" + taskId + ".cmd";

                Log.Info(String.Format(
                    "Trying to exec task {0} on win PC {1}",
                    taskId, node.NodeName
                ));

                var pack = node.Packages.First(p => String.Equals(p.Name, incarnation.PackageName, StringComparison.InvariantCultureIgnoreCase));
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
                    sharedInputFolder.TrimEnd(new char[] {'/', '\\'}),
                    tmpFolder.TrimEnd(new char[] { '/', '\\' })
                );

                // todo : env vars on WinPc provider

                string commandLine = incarnation.CommandLine;
                //var pack = node.Packages.First(p => commandLine.StartsWith(p.Name, StringComparison.InvariantCultureIgnoreCase));
                //commandLine = pack.Params["appPath"] + commandLine.Substring(pack.Name.Length);
                commandLine = String.Format(incarnation.CommandLine, pack.AppPath);
                //commandLine = String.Format(incarnation.CommandLine, pack.Params["appPath"]);

                batchContent += "echo %time% > clavire_task_started" + Environment.NewLine;
                batchContent += //"start \"" + jobFileName + " " + incarnation.PackageNameInConfig + "\" /wait /b" + 
                    "cmd.exe /c " + commandLine + Environment.NewLine;
                batchContent += "echo %time% > clavire_task_finished" + Environment.NewLine;

                foreach (string delPath in pack.Cleanup)
                {
                    batchContent += String.Format(
                        @"rmdir /s /q {0}" + Environment.NewLine + 
                        @"del /f /s /q {0}" + Environment.NewLine,
                        tmpFolder + delPath
                    );
                }


                batchContent += String.Format(
                    @"xcopy {1} {0}\ /z /s /e /c /i /h /r /y" + Environment.NewLine,
                    sharedOutputFolder.TrimEnd(new char[] { '/', '\\' }),
                    tmpFolder.TrimEnd(new char[] { '/', '\\' })
                );

                batchContent += String.Format(
                    @"ping localhost -n 3" + Environment.NewLine +
                    @"echo %time% > clavire_script_finished" + Environment.NewLine +
                    @"xcopy clavire_script_finished {1}\ /z /s /e /c /i /h /r /y" + Environment.NewLine +
                    @"cd {0}" + Environment.NewLine +
                    @"cd .." + Environment.NewLine +
                    //@"rmdir /s /q {0}" + Environment.NewLine +
                    "", 
                    tmpFolder.TrimEnd(new char[] { '/', '\\' }),
                    sharedOutputFolder.TrimEnd(new char[] { '/', '\\' })
                );

                IOProxy.Ftp.UploadFileContent(batchContent, jobFtpFolder, jobFileName);

                //string cmdArgs = "/c " + CONST.Path.PsExec.Replace("PsExec.exe", "p.cmd");
                //string cmdArgs = "\\\\192.168.4.1 -u nano -p Yt1NyDpQNm -d cmd.exe /c \"\\\\192.168.4.1\\ftp_exchange\\Tasks\\10043\\job_10043.cmd\"";
                //Log.Debug(cmdArgs);
                //Process.Start(CONST.Path.PsExec, cmdArgs);

                //**/
                //var psexecProcess = new Process();
                //psexecProcess.StartInfo.UseShellExecute = false;
                ////psexecProcess.StartInfo.RedirectStandardOutput = true;
                ////psexecProcess.StartInfo.RedirectStandardError = true;
                //psexecProcess.StartInfo.FileName = CONST.Path.PsExec;
                //psexecProcess.StartInfo.Arguments = String.Format(
                //    "\\\\{0} -d -u {1} -p {2} cmd.exe /c {4}", // -d -w \"{3}\"  ^> C:\\Temp\\out
                //    //"-u nano -p Yt1NyDpQNm cmd.exe /c " + CONST.Path.PsExec.Replace("PsExec.exe", "p.cmd"),
                //    resParams.name, resParams.user, resParams.pass, 
                //    resParams.tempFolderOnMachine.Replace(@"\", @"\\"), 
                //    sharedJobFilePath
                //);

                //*
                //psexecProcess.StartInfo.UserName = "nano";
                //psexecProcess.StartInfo.Password = new System.Security.SecureString();
                //foreach (var c in "Yt1NyDpQNm".ToCharArray())
                //{
                //    psexecProcess.StartInfo.Password.AppendChar(c);
                //}
                //**/

                //Log.Debug("psexec args:\n" + psexecProcess.StartInfo.Arguments);
                ////psexecProcess.Start();
                //Log.Debug("psexec process started");

                //string execMessage = /*psexecProcess.StandardOutput.ReadToEnd() + " " +*/ "1 " + PS_PID_START_MSG + "5."; //psexecProcess.StandardError.ReadToEnd();
                //execMessage = execMessage.Trim();
                ////psexecProcess.WaitForExit();
                //System.Threading.Thread.Sleep(3000);
                //Log.Debug("psexec output:\n" + execMessage);

                //if (!execMessage.Contains(PS_PID_START_MSG))
                //    throw new Exception(String.Format(
                //        "Couldn't exec task {0} on win pc {1}",
                //        taskId, resParams.name
                //    ));

                //execMessage = execMessage.Remove(0, execMessage.IndexOf(PS_PID_START_MSG) + PS_PID_START_MSG.Length);
                //string pid = execMessage.Substring(0, execMessage.Length-1);

                var rexService = EntryPointProxy.GetREx(node.Services.ExecutionUrl);
                int pid = rexService.Exec(taskId);

                Log.Debug(String.Format(
                    "Task {0} ({1}) started on pc {2} with pid = {3}",
                    taskId, pack.Name, node.NodeName, pid
                ));

                _nodeUsed[node.NodeName] = true;

                //System.Threading.Thread.Sleep(1000);

                return pid + "\n" + node.NodeName;
            }
        }

        public override void Abort(string providedTaskId, Resource resource, IEnumerable<NodeConfig> nodesConfig)
        {
            lock (_pcLock)
            {
                string[] providedWords = providedTaskId.Split(new char[] { '\n' });
                if (providedWords.Length > 2)
                    Log.Warn(String.Format("Too many sections in provided task id for win PC: {0}", providedTaskId));

                string pid = providedWords[0];
                string nodeName = providedWords[1];

                _nodeUsed[nodeName] = false; // todo: do a real abortion
            }
        }
    }
}
