using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MITP;
using MITP.Entity;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;

using SSH = Tamir.SharpSsh;

namespace ControllerFarmService
{
    public class NGridController : ASshBasedController, IStatelessResourceController
    {
        private static object _gridLock = new object();

        // todo : { "Name": "ORCA", "Version": "v1", "AppPath": "/grid/software/orca-2.6.35-nompi/bin/" },


        //private const string CLUSTER_NAME = "tb06.ngrid.ru";
        private const string HELPER_SSH_HOST = "192.168.4.50";
        private const string HELPER_SSH_USER = "nano";
        private const string HELPER_SSH_PASS = @"{c(Q=ix#-eYqr(AaqTDtcP5jHZadgZbm";

        private const string JOBS_FOLDER_ON_HELPER = @"/home/nano/GridNNS/Jobs/";

        //private const string FTP_PATH = @"ftp://nano:DerPar0le@192.168.1.189/";
        //private const string FTP_PATH_OUTSIDE = @"ftp://nano:DerPar0le@194.85.163.231/srv/ftp/";        

        //private static readonly string DEFAULT_JOB_DESCR_PATH = CONST.Path.GridJobsFolder + "_orca.js";
        //private static readonly string DEFAULT_JOB_LAUNCHER_PATH = CONST.Path.GridJobsFolder + "_orca.sh";

        //private const string GRIDFTP_PATH = @"
        // gsiftp://nnn1.pnpi.nw.ru/home/gridui060/files/Jobs/
        // gsiftp://tb05.ngrid.ru/home/boukhanovsky/Jobs/
        //private const string GRIDFTP_PATH_TOKEN = @"%GRIDFTP_PATH%";

        private const string USER_CERTIFICATE_PATH_ON_HELPER = @"/home/nano/GridNNS/Certs/user.cert";        
        private const string SYSTEM_CERTIFICATE_PATH_ON_HELPER = @"/home/nano/GridNNS/Certs/system.cert";
        private const string SYSTEM_CERTIFICATE_KEY_PATH_ON_HELPER = @"/home/nano/GridNNS/Certs/system.key";
        private const string SYSTEM_CERTIFICATE_GRID_PASS_PHRASE = @"656+)z?z}hw4--S7ltWp2:DJUbcPL]>"""; //@"tU\sY1uzBu4{4:a7m|g8BNu$e9{,6ItZ$nv:E{A]"; //@"QslW9g}Pp%_$DD++DAcIrZMa=L2C:Gtz";

        private abstract class PilotCommands
        {
            public const string GenerateProxyCertificate =
                "echo '" + SYSTEM_CERTIFICATE_GRID_PASS_PHRASE + "' | " +
                "voms-proxy-init -rfc -voms gridnnn -pwstdin" +
                " -cert " + SYSTEM_CERTIFICATE_PATH_ON_HELPER +
                " -key " + SYSTEM_CERTIFICATE_KEY_PATH_ON_HELPER;
            public const string SubmitJob = "pilot-job-submit";
            public const string CancelJob = "pilot-job-cancel";
            public const string JobStatus = "pilot-task-status";
            public const string CopyFilesToGridFtp = "globus-url-copy -r -notpt";
            public const string MakeFilesExecutableOnGridFtp = "uberftp -chmod 777";
            public const string MakeFolderOnGridFtp = "uberftp -mkdir";
            public const string RefreshCertificate = "voms-proxy-info -all | grep timeleft";
            public const string WriteJobFileToHelper = @"echo ""{0}"" | {1}";
            public const string SetPermissionsOnProxyCertFile = "chmod 600 /tmp/x509up_u500";
        }

        private static string SshExec(string command, string args = "", string pilotUrl = null)
        {
            lock (_gridLock)
            {
                string pilotUrlOrEmpty = command.ToLower().StartsWith("pilot") ? @" --url '" + pilotUrl + "'" : "";

                var sshExec = new SSH.SshExec(HELPER_SSH_HOST, HELPER_SSH_USER, HELPER_SSH_PASS);
                sshExec.Connect();

                string sshOut = "";
                string sshErr = "";
                string sshCommand = command + " " + args + pilotUrlOrEmpty;
                int sshResult = sshExec.RunCommand(sshCommand, ref sshOut, ref sshErr);
                sshExec.Close();

                sshErr = sshErr.Replace('.', ' '); // Cert creation emits many dots
                if (!String.IsNullOrWhiteSpace(sshErr))
                    throw new Exception(String.Format("Ssh execution error. Command: \"{0}\". Code: {1}, StdOut: {2}, StdErr: {3}", sshCommand, sshResult, sshOut, sshErr));

                return sshOut;
            }
        }

        public static void RefreshCertificate()
        {
            lock (_gridLock)
            {
                bool needToRefresh = false;

                try
                {
                    string timeLeft = SshExec(PilotCommands.RefreshCertificate, "");
                    if (String.IsNullOrWhiteSpace(timeLeft) || timeLeft.Contains("0:00:") || !timeLeft.Contains("timeleft"))
                        needToRefresh = true;
                }
                catch
                {
                    needToRefresh = true;
                }

                try
                {
                    if (needToRefresh)
                    {
                        Log.Debug("Creating new grid proxy certificate");
                        SshExec(PilotCommands.GenerateProxyCertificate, "");
                    }
                }
                catch (Exception e)
                {
                    Log.Warn(String.Format(
                        "Creation of grid proxy certificate has failed: {0}\n{1}",
                        e.Message, e.StackTrace
                    ));
                }
            }
        }

        public void Abort(TaskRunContext task)
        {
            lock (_gridLock)
            {
                RefreshCertificate();

                string localId = (string) task.LocalId;
                localId = localId.EndsWith("/a") ? localId.Remove(localId.Length - 2) : localId;
                string sshOut = SshExec(PilotCommands.CancelJob, localId);
            }
        }

        public TaskStateInfo GetTaskStateInfo(TaskRunContext task)
        {
            lock (_gridLock)
            {
                RefreshCertificate();

                ulong taskId = task.TaskId;
                string localId = (string) task.LocalId;
                string state = SshExec(PilotCommands.JobStatus, localId).ToLower();

                if (state.Contains("is new"))
                    return new TaskStateInfo(TaskState.Started, state);
                //return Tuple.Create(TaskState.Scheduled, state);

                if (state.Contains("is running") || state.Contains("is starting"))
                    return new TaskStateInfo(TaskState.Started, state);

                var node = GetNode(task);
                string ftpOutFolderFromSystem = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromSystem, taskId, CopyPhase.Out);
                string ftpOutFolderFromResource = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromResource, taskId, CopyPhase.Out);
                string gridFolder = IncarnationParams.IncarnatePath(node.DataFolders.LocalFolder, taskId, CopyPhase.None);

                IOProxy.Ftp.MakePath(ftpOutFolderFromSystem);
                SshExec(PilotCommands.CopyFilesToGridFtp, gridFolder + " " + ftpOutFolderFromResource);

                if (state.Contains("is finished"))
                    return new TaskStateInfo(TaskState.Completed, state);
                else
                    return new TaskStateInfo(TaskState.Failed, state);
            }
        }

        public IEnumerable<NodeStateResponse> GetNodesState(Resource resource)
        {
            lock (_gridLock)
            {
                var response = resource.Nodes.Select(n => new NodeStateResponse(n.NodeName)
                {
                    State = NodeState.Available
                });

                return response.ToArray();
            }
        }

        public object Run(TaskRunContext task)
        {
            lock (_gridLock)
            {
                RefreshCertificate();
                //var incarnation = task.Incarnation;

                string tmpFileName = null;
                if (task.UserCert != null)
                {
                    Log.Info("Using user's certificate");
                    tmpFileName = Path.GetTempFileName();
                    IOProxy.Storage.Download(task.UserCert, tmpFileName);

                    var scpForCert = new SSH.Scp(HELPER_SSH_HOST, HELPER_SSH_USER, HELPER_SSH_PASS);
                    scpForCert.Connect();
                    scpForCert.Recursive = true;
                    scpForCert.Put(tmpFileName, "/tmp/x509up_u500");
                    scpForCert.Close();

                    File.Delete(tmpFileName);
                    SshExec(PilotCommands.SetPermissionsOnProxyCertFile);
                }
                else
                {
                    Log.Info("Using system's certificate");
                }

                try
                {
                    long coresToUse = task.NodesConfig.Sum(cfg => cfg.Cores);
                    var node = GetNode(task);
                    var pack = node.PackageByName(task.PackageName);

                    // todo : remove
                    string commandLine = task.CommandLine;
                    commandLine = commandLine.Replace("java -jar ", "");
                    if (task.PackageName.ToLowerInvariant() == "cnm")
                        commandLine = commandLine.Replace("{0}", "ru.ifmo.hpc.main.ExtendedModel");
                    else
                    if (task.PackageName.ToLowerInvariant() == "ism")
                        commandLine = commandLine.Replace("{0}", "ru.ifmo.hpc.main.SpreadModel");
                    else
                        //if (task.PackageName.ToLowerInvariant() == "orca")
                        commandLine = commandLine.Replace("{0}", "");


                    string ftpFolderFromSystem = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromSystem, task.TaskId, CopyPhase.In);
                    string ftpFolderFromResource = IncarnationParams.IncarnatePath(node.DataFolders.ExchangeUrlFromResource, task.TaskId, CopyPhase.In);

                    string gridFtpFolder = IncarnationParams.IncarnatePath(node.DataFolders.LocalFolder, task.TaskId, CopyPhase.None);
                    SshExec(PilotCommands.MakeFolderOnGridFtp, gridFtpFolder);

                    string endl = "\n";

                    // Сначала дописываем недостающий входной файл (скрипт запуска пакета на кластере)

                    string scriptName = pack.AppPath;

                    //if (pack.EnvVars.Any())
                    {
                        // Файл с установкой переменных окружения, если пакет их использует

                        scriptName = "run.sh";
                        var scriptContent = new StringBuilder();
                        scriptContent.Append("#!/bin/bash" + endl);
                        foreach (var pair in pack.EnvVars)
                            scriptContent.AppendFormat("export {0}={1}" + endl, pair.Key, pair.Value);

                        scriptContent.Append(pack.AppPath);

                        /*
                        if (task.PackageName.ToLowerInvariant() == "orca")
                        {
                            string[] args = commandLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0; i < args.Length; i++)
                            {
                                if (args[i] == "orca.out")
                                    scriptContent.Append(" >");

                                scriptContent.Append(" $" + (i + 1).ToString());
                            }
                        }
                        else*/
                        {
                            scriptContent.Append(" " + commandLine);
                        }

                        string scriptLocalPath = Path.GetTempFileName();
                        File.WriteAllText(scriptLocalPath, scriptContent.ToString());
                        IOProxy.Ftp.UploadLocalFile(scriptLocalPath, ftpFolderFromSystem, scriptName);

                        File.Delete(scriptLocalPath);
                    }

                    //IOProxy.Ftp.UploadLocalFile(DEFAULT_JOB_LAUNCHER_PATH, GetFtpInputFolder(taskId), Path.GetFileName(DEFAULT_JOB_LAUNCHER_PATH));

                    // Копируем входные файлы с ФТП на ГридФТП

                    SshExec(PilotCommands.CopyFilesToGridFtp, ftpFolderFromResource + " " + gridFtpFolder);
                    SshExec(PilotCommands.MakeFilesExecutableOnGridFtp, gridFtpFolder + "*");

                    // Формируем описание задания для грида

                    var jobFileContent = new StringBuilder();

                    jobFileContent.AppendFormat(@"{{ ""version"": 2, ""description"": ""{0}""," + endl, task.TaskId);
                    jobFileContent.AppendFormat(@"  ""default_storage_base"": ""{0}""," + endl, gridFtpFolder);
                    jobFileContent.AppendFormat(@"  ""tasks"": [ {{ ""id"": ""a"", ""description"": ""task"", ""definition"": {{ ""version"": 2," + endl);
                    jobFileContent.AppendFormat(@"      ""executable"": ""{0}""," + endl, scriptName);
                    //jobFileContent.AppendFormat(@"      ""arguments"": [ ""{0}"" ]," + endl, String.Join(@""", """, args));

                    jobFileContent.AppendFormat(@"      ""input_files"": {{" + endl);
                    if (scriptName == "run.sh") // todo : if no input files?
                        jobFileContent.AppendFormat(@"          ""run.sh"": ""run.sh""," + endl);
                    jobFileContent.AppendFormat(@"          " + String.Join(
                        "," + endl + "          ",
                        task.InputFiles.Select(
                            file => String.Format(@"""{0}"": ""{0}""", file.FileName)
                        )
                    ));
                    jobFileContent.AppendFormat(endl + @"      }}," + endl);

                    jobFileContent.AppendFormat(@"      ""output_files"": {{" + endl);

                    //if (task.PackageName.ToLowerInvariant() == "cnm")
                    //    jobFileContent.AppendFormat(@"          ""output.dat"": ""output.dat""" + endl);
                    //else
                    if (task.PackageName.ToLowerInvariant() == "ism")
                        jobFileContent.AppendFormat(@"          ""output.dat"": ""output.dat""" + endl);
                    else
                    if (task.PackageName.ToLowerInvariant() == "orca")
                    {
                        jobFileContent.AppendFormat(@"          ""orca.out"":    ""orca.out""," + endl);
                        jobFileContent.AppendFormat(@"          ""eldens.cube"": ""eldens.cube""" + endl);
                    }
                    else
                    {
                        jobFileContent.AppendFormat(@"          " + String.Join(
                            "," + endl + "          ",
                            task.ExpectedOutputFileNames
                                .Where(name => name != "std.out" && name != "std.err")
                                .Select(
                                    name => String.Format(@"""{0}"": ""{0}""", name)
                                )
                        ) + endl);
                    }

                    jobFileContent.AppendFormat(@"      }}," + endl);

                    jobFileContent.AppendFormat(@"      ""stdout"": ""std.out"", ""stderr"": ""std.err"", " + endl);
                    jobFileContent.AppendFormat(@"      ""count"": {0}" + endl, coresToUse);

                    if (pack.Params.ContainsKey("requirements"))
                        jobFileContent.AppendFormat(@"      ,""requirements"": {0}" + endl, pack.Params["requirements"]);

                    jobFileContent.AppendFormat(@"  }} }} ]," + endl);

                    jobFileContent.AppendFormat(@"  ""requirements"": {{ ""hostname"": [""{0}""]", node.NodeAddress);

                    //if (pack.Params.ContainsKey("requirements"))
                    //    jobFileContent.AppendFormat(@", {0}" + endl, pack.Params["requirements"]);

                    jobFileContent.AppendFormat(@"}}" + endl + "}}", node.NodeAddress);

                    Log.Debug(String.Format("Task's '{0}' grid job JSON: ", task.TaskId, jobFileContent));

                    string jobFileName = "job_" + task.TaskId.ToString() + ".js";
                    string jobFilePathOnHelper = JOBS_FOLDER_ON_HELPER + jobFileName;

                    //string jobFileContent = File.ReadAllText(DEFAULT_JOB_DESCR_PATH).Replace(GRIDFTP_PATH_TOKEN, taskFolderOnGridFtp);
                    string jobFilePathLocal = Path.GetTempFileName();
                    File.WriteAllText(jobFilePathLocal, jobFileContent.ToString());

                    // Записываем его на сервер с Пилотом

                    var scp = new SSH.Scp(HELPER_SSH_HOST, HELPER_SSH_USER, HELPER_SSH_PASS);

                    /*
                    var notifier = new JobDescriptionUploadNotifier(TaskId, Cluster, RunParams);
                    scp.OnTransferEnd += new SSH.FileTransferEvent(notifier.OnFinish); // todo : необязательно
                    */

                    scp.Connect();
                    scp.Recursive = true;
                    scp.Put(jobFilePathLocal, jobFilePathOnHelper);
                    scp.Close();

                    File.Delete(jobFilePathLocal); // todo : remove files on helper and gridftp

                    // Запускаем

                    Log.Info(String.Format(
                        "Trying to exec task {0} on grid cluster {1}",
                        task.TaskId, node.NodeName
                    ));

                    string launchResult = SshExec(PilotCommands.SubmitJob, jobFilePathOnHelper, pilotUrl: node.Services.ExecutionUrl);
                    int urlPos = launchResult.IndexOf("https://");
                    string jobUrl = launchResult.Substring(urlPos).Trim() + "a";
                    Log.Debug(jobUrl);

                    Log.Info(String.Format(
                        "Task {0} launched on grid with jobUrl = {1}",
                        task.TaskId, jobUrl
                    ));

                    return jobUrl;
                }
                catch (Exception e)
                {
                    Log.Error(String.Format(
                        "Error while starting task {0} in grid: {1}\n{2}",
                        task.TaskId, e.Message, e.StackTrace
                    ));

                    throw;
                }
                finally
                {
                    if (task.UserCert != null)
                    {
                        Log.Info("Wiping user's certificate");
                        tmpFileName = Path.GetTempFileName();
                        File.WriteAllText(tmpFileName, "Wiped by Easis system");

                        var scpForCert = new SSH.Scp(HELPER_SSH_HOST, HELPER_SSH_USER, HELPER_SSH_PASS);
                        scpForCert.Connect();
                        scpForCert.Recursive = true;
                        scpForCert.Put(tmpFileName, "/tmp/x509up_u500");
                        scpForCert.Close();

                        File.Delete(tmpFileName);
                        SshExec(PilotCommands.SetPermissionsOnProxyCertFile);
                    }
                }
            }
        }
    }
}


