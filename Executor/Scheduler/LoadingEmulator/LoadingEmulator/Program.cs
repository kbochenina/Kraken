using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Configuration;
using System.Net;

using System.ServiceModel;
using Easis.Execution.Api;
using Easis.Execution.Api.TokenBroker;
using Easis.Execution.Api.FacadeService;

using System.Timers;
using Easis.Ui.ServiceClients;
using System.IO;
using Testing;
using System.Text.RegularExpressions;
using ModelEstimator;
using LoadingEmulator.SchedulerServiceReference;



namespace LoadingEmulator
{
    class Program
    {
        private string _dataServiceEndpoint;
        private string _tokenBrokerEndpoint;
        private string _facadeEndpoint;
        private string _serverName;
        private string ProxyAdress;
        private string ProxyUser;
        private string ProxyPass;
        private int UpdatePeriod = 500;

        private string UserName = null;
        private string Password { get; set; }
        private string SecId = null;
        
        private string DescriptionFile = "workflow.desc";
        private System.Threading.AutoResetEvent waitOne = new System.Threading.AutoResetEvent(false);
        private Dictionary<string, string> Statuses = new Dictionary<string, string>();
        private Dictionary<string, string> CalculationTimes = new Dictionary<string, string>();
        private Dictionary<string, string> CalculationTimesErr = new Dictionary<string, string>();
        private Dictionary<string, string> OverheadTimes = new Dictionary<string, string>();
        private Dictionary<string, string> OverheadTimesErr = new Dictionary<string, string>();
        private FileStream output;
        private string results;
        private DateTime started;
        private int finished;
        private int steps;
        private int workflows;
        private int mode;
        private Timer timer;

        private IJobMonitor jobMonitor;
        private WorkflowApi _api;
        private VMLauncher vmlauncher;


        static void Main(string[] args)
        {
            //var console = new CombinedWriter(@"d:\Work\CLAVIRE\LoadingEmulator\LoadingEmulator\bin\4.1\Logs\" + DateTime.Now.ToShortTimeString().Replace(":", "-") + ".txt", true, Encoding.UTF8, 108000, Console.Out);
            new Program().Run(args);
        }

        private void Run(string[] args){
            
            ReadConfiguration();
            ParseCommandLine(args);
            SetProxy();
            

            if (!Directory.Exists("results")) Directory.CreateDirectory("results");
            results = @"results\" + DateTime.Now.ToShortDateString() + "_" + DateTime.Now.ToShortTimeString().Replace(":", "-");
            output = File.Create(results+".txt");
            output.Close();
            
            //if (finished == 0) DeleteFiles();

            LogWrite("Connecting to " + ConfigurationManager.AppSettings["ServerName"] + "...");
            
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;
            var tokenBroker = new TokenBrokerClient(new BasicHttpBinding("TokenBrokerBinding"), new EndpointAddress(_tokenBrokerEndpoint));
            var token = tokenBroker.GetToken("facadeuser", "Yt1NyDpQNm");

            var user = WorkflowApi.ParseToken(token);
            var storage = new DStorage(_dataServiceEndpoint, user.UserId, SecId);
            
            _api = new WorkflowApi(storage, _facadeEndpoint, token);

            //Statuses.Add("e6352bf7-d5b2-4617-98a7-0481e5404aa1","Active");
            //CopyRows(new string[]{"ForecastSize"});
            //return;
            mode = 1;
            //Experiment1(mode);
            //Experiment2();
            Experiment3();
            //Experiment3PushBsm();
            //Experiment4(mode);
            //Experiment5(mode);
            //Experiment6();
            Console.ReadLine();
            
        }

        //3 modes
        private void Experiment1(int mode){

            LogWrite("Starting experiment mode " + mode.ToString());
            workflows = 2;
            steps = 20; //max 20
            
            int delay = 120;  //150

            var testpDescriptionReader = new DescriptionReader("testp.wf");
            var bsmDescriptionReader = new DescriptionReader("bsm.wf");
            foreach (var pair in bsmDescriptionReader.InputFiles)
            {
                var val = ConfigurationManager.AppSettings[pair.Key];
                _api.BindFile(pair.Key, val);
            }

            var timer = new Timer();
            int i = 1;
            timer.Elapsed += new ElapsedEventHandler((s, e) =>
            {
                Random random = new Random();
                if (i > workflows)
                {
                    i = 0;
                    timer.Stop();
                }
                else if (i == 1)
                {

                    StringBuilder sb = new StringBuilder();
                    sb.Append(testpDescriptionReader.Script);
                    for (int step = 1; step <= 2*steps; step++)
                    {
                        //if (step > amount/2) offset = 40;
                        sb.Append("\n step s" + step + " runs testp (\n in0=" + i + step + ",\n in1=1,\n timeToWait=" + (delay / i + random.Next(0, 20)).ToString() + "\n)\n");
                    }
                    _api.Script = sb.ToString();
                    jobMonitor = _api.CreateMonitor();
                    jobMonitor.UpdatePeriod = 1000 * 50;
                    jobMonitor.Finished += JobMonitorOnFinishedExp1;
                    jobMonitor.Active += JobMonitorOnActive;

                    LogWrite(DateTime.Now.ToLongTimeString() + " " + steps + " testp pushed: " + i + "/" + "; id:" + jobMonitor.JobId);
                    LogWrite(_api.Script.Substring(0, 57));
                    if (mode == 1 || mode==3) { 
                        timer.Interval = 20 * 1000;
                        LogWrite("Waiting 25 sec");
                    }
                    jobMonitor.Run();
                    
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    //sb.Append(bsmDescriptionReader.Script);
                    if (mode > 1) { //2,3
                        sb.Append("[flow:priority = @urgent]\n"); 
                        if (mode == 2) sb.Append("[flow:MinTime = " + '"' + "0" + '"' + "]\n[flow:MaxTime = " + '"' + "0" + '"' + "]\n");
                    }
                    if (mode > 2) sb.Append("[flow:MinTime = " + '"' + "0" + '"' + "]\n[flow:MaxTime = " + '"' + "0" + '"' + "]\n"); 
                    
                    for (int step = 1; step <=steps; step++)
                    {
                        sb.Append("\n step MaskedFullBSM_" + step + " runs bsm \n (\n inMeasurement = measurementFile,\n inHirlam = hirlam6,\n swan = swanFile,\n inBSH = BSHFile,\n useAssimilation = true,\n useSWAN = true,\n useBSH = true,\n useOldProject = false,\n useMask = false,\n startCalcDate = \"09/01/2007 12:00:00\",\n inAssFields = assFields,\n inProject = projects,\n controlPoints = inControlPoints,\n deleteDirs = true,\n ForecastSize = 3 \n)\n");
                    }
                    _api.Script = sb.ToString();
                    jobMonitor = _api.CreateMonitor();
                    jobMonitor.UpdatePeriod = 1000 * 5;
                    jobMonitor.Finished += JobMonitorOnFinishedExp1;
                    jobMonitor.Active += JobMonitorOnActive;
                    LogWrite(DateTime.Now.ToLongTimeString() + " " + steps + " bsm pushed: " + i + "/" + "; id:" + jobMonitor.JobId);
                    LogWrite(_api.Script.Substring(0, 70));
                    jobMonitor.Run();
                    //timer.Interval = 120 * 1000;
                    timer.Stop();
                }
                i++;

            });
            timer.Interval = 1000;
            timer.Start();
        }
        //virtual starting
        private void Experiment2(){
           
            vmlauncher = new VMLauncher();
            vmlauncher.StopAll(20);

            steps = 2;
            workflows = 25;
            int sleep = 30;
            int delay = 140;
           
            var testpDescriptionReader = new DescriptionReader("testp.wf");
            
            int i = 0;
            timer = new Timer();
            
            timer.Elapsed += new ElapsedEventHandler((s, e) =>
            {
               
                if (i >= workflows)
                {
                    timer.Stop();
                    LogWrite("Pusher stopped");
                }
                else{

                    StringBuilder sb = new StringBuilder();
                    sb.Append(testpDescriptionReader.Script);
                    if (i > 1) steps = 1;
                    for (int step = 0; step < steps; step++)
                    sb.Append("\n step s"+step+" runs testp (\n in0=" + step+",\n in1="+sleep+",\n timeToWait=" + (delay).ToString() + "\n)\n");
                    
                   _api.Script = sb.ToString();

                    jobMonitor = _api.CreateMonitor();
                    jobMonitor.UpdatePeriod = 1000 * 50;
                    jobMonitor.Finished += JobMonitorOnFinishedExp2;
                    jobMonitor.Active += JobMonitorOnActive;
                    LogWrite(DateTime.Now.ToLongTimeString() + " " + (i + 1) + "/" + workflows + " testp pushed; " + steps + " steps");
                    jobMonitor.Run();
                    timer.Interval = sleep*1000;
                    
                }
                i++;
                

            });
            LogWrite("Sending jobs to " + ConfigurationManager.AppSettings["ServerName"] + ". Press any key to stop monitoring...");
            timer.Interval = 100;
            timer.Start();

            vmlauncher.Run();

            Console.ReadLine();
        }
        //estimation learning
        private void Experiment3PushBsm()
        {
            if (finished == 0) DeleteFiles();
            var resourse = "b4.b4-131";
            LogWrite("Pushing BSM to resourse " + resourse);
            //workflows = 5;
            steps = 1; 
            int[] sizes = { 1,3,4,5,6 };
            var bsmDescriptionReader = new DescriptionReader("bsm.wf");
            _api.UploadFiles(bsmDescriptionReader.InputFiles);
           
            for (int i = 0; i < sizes.Length; i++)
            {

                StringBuilder sb = new StringBuilder();
                //sb.Append(bsmDescriptionReader.Script);

                
                int stepDiffer = 0;
                for (int step = 1; step <= steps; step++)
                //foreach(var size in sizes)
                {
                    var size = sizes[0];
                    //if (stepDiffer >= sizes.Length) stepDiffer = 0;
                    sb.Append("[Resource = " + '"' + resourse + '"' + "]\n");
                    sb.Append("step MaskedFullBSM_" + stepDiffer + " runs bsm " + (stepDiffer > 0 ? " after MaskedFullBSM_" + (stepDiffer - 1) : "") + " \n (\n inMeasurement = measurementFile,\n inHirlam = hirlam" + size + ",\n swan = swanFile,\n inBSH = BSHFile,\n useAssimilation = true,\n useSWAN = true,\n useBSH = true,\n useOldProject = false,\n useMask = false,\n startCalcDate = \"09/01/2007 12:00:00\",\n inAssFields = assFields,\n inProject = projects,\n controlPoints = inControlPoints,\n deleteDirs = true,\n ForecastSize = " + size + " \n)\n");
                    stepDiffer++;
                }
                _api.Script = sb.ToString();
                jobMonitor = _api.CreateMonitor();
                jobMonitor.UpdatePeriod = 1000 * 5;
                jobMonitor.Active += JobMonitorOnActive;
                jobMonitor.Finished += (sender, jobDecriptionEventArgs) =>
                {
                    JobMonitorStandardFinished(sender, jobDecriptionEventArgs);
                    CopyRows();
                };

                LogWrite(DateTime.Now.ToLongTimeString() + " " + steps + " bsm pushed: " + i + "/" + workflows + "; id:" + jobMonitor.JobId);
                LogWrite(_api.Script.Substring(0, 70));
                jobMonitor.Run();
                Statuses.Add(jobMonitor.JobId.ToString(), "Pushed");
                LogWrite(DateTime.Now.ToLongTimeString() + " " + jobMonitor.JobId + " pushed");
            }
        }
        private void Experiment3(){
            if (finished == 0)
                if (DeleteFiles()) {
                    finished++;
                    Experiment3PushBsm();
                    return;
                }
            var resourse = "b4.b4-131";
            LogWrite("Starting experiment 3 on resourse "+resourse);
            
            steps = 1; //max 20
            int[] sizes = { 1,3,4,5,6 };
            workflows = sizes.Length;
           
            /*
            foreach (var pair in bsmDescriptionReader.InputFiles)
            {
                var val = ConfigurationManager.AppSettings[pair.Key];
                _api.BindFile(pair.Key, val);
            }
            */
            IEnumerable<string> lines = new List<string>();
            IEnumerable<string> overlines = new List<string>();
            if (File.Exists(ConfigurationManager.AppSettings["HistoryFile"]))
            {
                lines = File.ReadAllLines(ConfigurationManager.AppSettings["HistoryFile"]).Where(line => line.Contains("bsm") && line.Contains(resourse));
                overlines = File.ReadAllLines(ConfigurationManager.AppSettings["HistoryFile"].Replace("model_coef", "over"));
            }
            var runs = new List<RunRecord>();
            //.Where(l=>l.Contains(@"{""ForecastSize"": ""1""}"))
            foreach (var line in lines)
            {
                var rows = line.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                var rows2 = overlines.Where(l => l.Contains(rows[0])).FirstOrDefault().Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                //Select(s => double.Parse(s)).ToArray();
                var rx = new Regex(@"""ForecastSize"": ""([^,]+)""");
                var match = rx.Match(rows[11]);
                if (match.Success)
                {
                    var rec = new RunRecord { ComputationTime = double.Parse(rows[7].Replace(".", ",")) };
                    rec.OverheadTime = TimeSpan.Parse(rows2[7]).TotalSeconds;   // База пакетов
                    rec.OverheadTime += TimeSpan.Parse(rows2[8]).TotalSeconds;  // Оценка ресурсов	
                    rec.OverheadTime += TimeSpan.Parse(rows2[9]).TotalSeconds;  // T_Scheduler	
                    rec.OverheadTime += TimeSpan.Parse(rows2[10]).TotalSeconds; // Коммуникация	
                    rec.OverheadTime += TimeSpan.Parse(rows2[11]).TotalSeconds; // T_InputFilesCopy
                    rec.OverheadTime += TimeSpan.Parse(rows2[12]).TotalSeconds; // T_OutputFilesCopy
                    rec.RunContext.Add("ForecastSize", double.Parse(match.Groups[1].Value));
                    runs.Add(rec);
                }
            }

            PerformanceModel model = new BsmModel();
            var sp = ParametersOptimizer.UpdateServiceComputationParameters(new Dictionary<string, double>(), runs, model);
            var spp = ParametersOptimizer.UpdateServiceOverheadParameters(new Dictionary<string, double>(), runs);
            
            foreach (var p in sp)
                LogWrite(String.Format("{0}: {1}", p.Key, p.Value.ToString("0.0000")));
            /*
            foreach (var p in spp)
                LogWrite(String.Format("{0}: {1}", p.Key, p.Value.ToString("0.0000")));
            */

            var bsmDescriptionReader = new DescriptionReader("bsm.wf");
            _api.UploadFiles(bsmDescriptionReader.InputFiles);

            int i = 0;
            foreach (var size in sizes)
            {
                LogWrite(String.Format("ForecastSize: {0}",size));
                var runRecord = new RunRecord();
                runRecord.RunContext.Add("ForecastSize", (double)size);

                var compuTime = model.GetComputationTime(sp, runRecord.RunContext, runRecord.ExecutionParams);
                var compuError = model.GetComputationErrorRelative(sp, runRecord.RunContext, runRecord.ExecutionParams);
                LogWrite(String.Format("Calculation time: {0}+/-{1}", compuTime.ToString("0.000"), compuError.ToString("0.000")));

                var overheadTime = model.GetOverheadTime(spp, runRecord.RunContext, runRecord.ExecutionParams);
                var overheadError = model.GetOverheadError(spp, runRecord.RunContext, runRecord.ExecutionParams);
                LogWrite(String.Format("Overhead time: {0}+/-{1}", overheadTime.ToString("0.000"), overheadError.ToString("0.000")));

                StringBuilder sb = new StringBuilder();
                //int stepDiffer = 0;
                for (int step = 1; step <= steps; step++){
                //if (stepDiffer >= sizes.Length) stepDiffer = 0;
                sb.Append("[Resource = " + '"' + resourse + '"' + "]\n");
                sb.Append("step MaskedFullBSM_"+step+" runs bsm \n (\n inMeasurement = measurementFile,\n inHirlam = hirlam" + size + ",\n swan = swanFile,\n inBSH = BSHFile,\n useAssimilation = true,\n useSWAN = true,\n useBSH = true,\n useOldProject = false,\n useMask = false,\n startCalcDate = \"09/01/2007 12:00:00\",\n inAssFields = assFields,\n inProject = projects,\n controlPoints = inControlPoints,\n deleteDirs = true,\n ForecastSize = " + size + " \n)\n");
                //stepDiffer++;
                }
                _api.Script = sb.ToString();
                jobMonitor = _api.CreateMonitor();
                jobMonitor.UpdatePeriod = 1000 * 5;
                jobMonitor.Active += JobMonitorOnActive;
                jobMonitor.Finished += (sender, jobDecriptionEventArgs) =>
                {
                    JobMonitorStandardFinished(sender, jobDecriptionEventArgs);
                    CopyRows(new string[]{"ForecastSize"});
                };

                LogWrite(DateTime.Now.ToLongTimeString() + " " + steps + " bsm pushed: " + i + "/" + workflows + "; id:" + jobMonitor.JobId);
                //LogWrite(_api.Script.Substring(0, 70));
                started = DateTime.Now;
                jobMonitor.Run();
                Statuses.Add(jobMonitor.JobId.ToString(), "Pushed");
                CalculationTimes.Add(jobMonitor.JobId.ToString(), compuTime.ToString("0.000"));
                CalculationTimesErr.Add(jobMonitor.JobId.ToString(), compuError.ToString("0.000"));
                OverheadTimes.Add(jobMonitor.JobId.ToString(), overheadTime.ToString("0.000"));
                OverheadTimesErr.Add(jobMonitor.JobId.ToString(), overheadError.ToString("0.000"));
                LogWrite(DateTime.Now.ToLongTimeString() + " " + jobMonitor.JobId + " pushed");
                i++;
            }
           
        }

        //cnm DefaultHeuristics
        private void Experiment4(int mode){
            
            if (finished == 0) DeleteFiles();
            
            started = DateTime.Now;
            LogWrite(started.ToLongTimeString()+" Starting experiment 4 mode " + mode);
            
            SwitchParameter("DefaultHeuristics", (mode == 2 ? "MinMin" : "Stub"));
            
            var wfDescriptionReader = new DescriptionReader("cnm.wf");
            _api.UploadFiles(wfDescriptionReader.InputFiles);

            _api.Script = wfDescriptionReader.Script;
            LogWrite(_api.Script);
            jobMonitor = _api.CreateMonitor();
            jobMonitor.UpdatePeriod = 1000 * 5;
            jobMonitor.Active += JobMonitorOnActive;
            jobMonitor.Finished += (sender, jobDecriptionEventArgs) =>
            {
                JobMonitorStandardFinished(sender, jobDecriptionEventArgs);
                CopyRows();
                if (mode == 2) Experiment4(mode);
            };
            
            jobMonitor.Run();
            Statuses.Add(jobMonitor.JobId.ToString(),"Pushed");
            LogWrite(DateTime.Now.ToLongTimeString() + " " + jobMonitor.JobId + " state: pushed");
        }
        //cnm DefaultUrgentHeuristics
        private void Experiment5(int mode)
        {
            if (finished == 0) DeleteFiles();
            
            started = DateTime.Now;
            LogWrite(started.ToLongTimeString() + "Starting experiment 5 mode " + mode);
            
            ParasiteLoading("b14.b14-113");
            ParasiteLoading("b14.b14-22");

            SwitchParameter(DateTime.Now.ToLongTimeString() + " DefaultUrgentHeuristics", (mode == 2 ? "UBestFirst" : "UGreedy"));
            
            var wfDescriptionReader = new DescriptionReader("cnm2.wf");
            _api.UploadFiles(wfDescriptionReader.InputFiles);

            _api.Script = wfDescriptionReader.Script;
            jobMonitor = _api.CreateMonitor();
            jobMonitor.UpdatePeriod = 1000 * 5;
            jobMonitor.Active += JobMonitorOnActive;
            jobMonitor.Finished += (sender, jobDecriptionEventArgs) =>
            {
                var monitor = (sender as JobMonitor);
                finished++;
                Statuses[monitor.JobId.ToString()] = "Finished";
                LogWrite(DateTime.Now.ToLongTimeString() + " " + monitor.JobId + " " + Statuses[monitor.JobId.ToString()] + " after " + TimeSpan.FromTicks(DateTime.Now.Ticks - started.Ticks).Minutes + " min " + TimeSpan.FromTicks(DateTime.Now.Ticks - started.Ticks).Seconds + " sec ");
                CopyRows();
                mode++;
                if (mode == 2) Experiment5(mode);
            };
            LogWrite(_api.Script);
            jobMonitor.Run();
            Statuses.Add(jobMonitor.JobId.ToString(), "Pushed");
            LogWrite(DateTime.Now.ToLongTimeString() + " " + jobMonitor.JobId + " pushed");
        }
        //Just one bsm
        private void Experiment6()
        {
            if (finished == 0) DeleteFiles();
            started = DateTime.Now;
            LogWrite(started.ToLongTimeString() + " Starting experiment 6 mode " + mode);

            ParasiteLoading("b14.b14-113");
            //ParasiteLoading("b14.b14-22");
            return;

            var wfDescriptionReader = new DescriptionReader("bsm.wf");
            _api.UploadFiles(wfDescriptionReader.InputFiles);

            _api.Script = wfDescriptionReader.Script;
            jobMonitor = _api.CreateMonitor();
            jobMonitor.UpdatePeriod = 1000 * 5;
            jobMonitor.Active += JobMonitorOnActive;
            jobMonitor.Finished += (sender, jobDecriptionEventArgs) =>
            {
                var monitor = (sender as JobMonitor);
                finished++;
                Statuses[monitor.JobId.ToString()] = "Finished";
                LogWrite(DateTime.Now.ToLongTimeString() + " " + monitor.JobId + " " + Statuses[monitor.JobId.ToString()] + " after " + TimeSpan.FromTicks(DateTime.Now.Ticks - started.Ticks).Minutes + " min " + TimeSpan.FromTicks(DateTime.Now.Ticks - started.Ticks).Seconds + " sec ");
                //CopyRows();
            };
            LogWrite(_api.Script);
            jobMonitor.Run();
            Statuses.Add(jobMonitor.JobId.ToString(), "Pushed");
            LogWrite(DateTime.Now.ToLongTimeString() + " " + jobMonitor.JobId + " pushed");
        }
        
        private void JobMonitorOnActive(object sender, JobDecriptionEventArgs jobDecriptionEventArgs)
        {
            if (Statuses[jobDecriptionEventArgs.JobInfo.ID.ToString()] != jobDecriptionEventArgs.JobInfo.State.ToString()) {
                Statuses[jobDecriptionEventArgs.JobInfo.ID.ToString()] = jobDecriptionEventArgs.JobInfo.State.ToString();
                LogWrite(DateTime.Now.ToLongTimeString() + " " + jobDecriptionEventArgs.JobInfo.ID + " "+jobDecriptionEventArgs.JobInfo.State);
            }
            
        }

        private void JobMonitorStandardFinished(object sender, JobDecriptionEventArgs jobDecriptionEventArgs){
            var monitor = (sender as JobMonitor);
            finished++;
            Statuses[monitor.JobId.ToString()] = "Finished";

            if (jobDecriptionEventArgs.JobInfo != null && jobDecriptionEventArgs.JobInfo.ErrorComment != null)
            {
                LogWrite(DateTime.Now.ToLongTimeString() + " " + monitor.JobId + " " + Statuses[monitor.JobId.ToString()] + " with error " + jobDecriptionEventArgs.JobInfo.ErrorComment);
                return;
            }
            LogWrite(DateTime.Now.ToLongTimeString() + " " + monitor.JobId + " " + Statuses[monitor.JobId.ToString()] + " after " + TimeSpan.FromTicks(DateTime.Now.Ticks - started.Ticks).Minutes + " min " + TimeSpan.FromTicks(DateTime.Now.Ticks - started.Ticks).Seconds + " sec ");
            mode++;
        }
        private void JobMonitorOnFinishedExp1(object sender, JobDecriptionEventArgs jobDecriptionEventArgs)
        {
            var monitor = (sender as JobMonitor);

            finished++;
            var jobInfo = jobDecriptionEventArgs.JobInfo;
            var result = finished.ToString() + " finished ";
            if (jobInfo != null)result+=" Wf_ID : "+jobInfo.ID;
            Console.WriteLine(result);
            
            if ((finished == 2 || finished == 4) && mode<3)
            {
                //System.Threading.Thread.Sleep(70 * 1000);
                mode++;
                Experiment1(mode);
            }
           
            if (!IsErrorState(jobInfo) && monitor != null)
            {
                //monitor.DownloadResults(jobInfo);
                //Console.WriteLine(jobInfo.);
            }

            //Environment.Exit(0);
        }

        private void JobMonitorOnFinishedExp2(object sender, JobDecriptionEventArgs jobDecriptionEventArgs)
        {
            
            var monitor = (sender as JobMonitor);

            var jobInfo = jobDecriptionEventArgs.JobInfo;
            if (jobInfo == null) return;
            finished++;
            Console.WriteLine(jobInfo.FinishedAt.Value.ToLongTimeString()+" "+finished.ToString() + " finished Wf_ID: "+jobInfo.ID);
            if (finished == workflows && vmlauncher != null)
            {
                vmlauncher.StopAll();
                
            }
            if (!IsErrorState(jobInfo) && monitor != null)
            {
                //monitor.DownloadResults(jobInfo);
                //Console.WriteLine(jobInfo.);
            }

            //Environment.Exit(0);
        }

        private bool DeleteFiles(bool both=false) {

            Console.WriteLine("Delete existing files on "+_serverName+"? Y/N");
            var a = Console.ReadLine();
            if (a.ToLower() == "y" || a.ToLower() == "д")
            {
                if (File.Exists(@"\\192.168."+_serverName+@"\ExecutionLogs\model_coef.csv"))
                {

                    File.Copy(@"\\192.168."+_serverName+@"\ExecutionLogs\model_coef.csv", @"\\192.168."+_serverName+@"\ExecutionLogs\model_coef_prev.csv", true);
                    File.Delete(@"\\192.168."+_serverName+@"\ExecutionLogs\model_coef.csv");
                }
                if(both)
                if (File.Exists(@"\\192.168."+_serverName+@"\ExecutionLogs\over.csv"))
                {
                    File.Copy(@"\\192.168."+_serverName+@"\ExecutionLogs\over.csv", @"\\192.168."+_serverName+@"\ExecutionLogs\over_prev.csv", true);
                    File.Delete(@"\\192.168."+_serverName+@"\ExecutionLogs\over.csv");
                }
                return true;
            }
            return false;
        }
        private void SwitchParameter(string paramName, string value){

            SchedulerServiceClient client = new SchedulerServiceClient();
            if (paramName == "DefaultUrgentHeuristics")
            {
                client.SetDefaultUHName(value);
                LogWrite(paramName + "=" + value);
            }
            else if (paramName == "DefaultHeuristics")
            {
                client.SetDefaultHName(value);
                LogWrite(paramName + "=" + value);
            }
            

            /*
            var configPath = ConfigurationManager.AppSettings["SchedulerConfig"];
            string[] lines = File.ReadAllLines(configPath.Replace(".config", "-backup-smirnp.config"));
            var line = lines.Where(l => l.Contains(paramName)).FirstOrDefault();
            var newline = "<add key=" + '"' + paramName + '"' + " value=" + '"' + value + '"' + "/>";
            
            using (StreamWriter file = new StreamWriter(configPath))
            {
                foreach (var line2 in lines)
                {
                    if (line2 == line) file.WriteLine(newline);
                    else file.WriteLine(line2);
                }
            }
            */
        }
        private void ParasiteLoading(string resourse=null){

            LogWrite(DateTime.Now.ToLongTimeString() + " Loading "+(resourse!=null?resourse:"system")+" by parasite testp task");
            var parasiteWfDescriptionReader = new DescriptionReader("parasite.wf");
            _api.Script = (resourse!=null?"[Resource = \""+resourse+"\"]\n":"")+parasiteWfDescriptionReader.Script;
            jobMonitor = _api.CreateMonitor();
            jobMonitor.Active += (sender, jobDecriptionEventArgs) =>{
                if (Statuses[jobDecriptionEventArgs.JobInfo.ID.ToString()] != jobDecriptionEventArgs.JobInfo.State.ToString())
                {
                    Statuses[jobDecriptionEventArgs.JobInfo.ID.ToString()] = jobDecriptionEventArgs.JobInfo.State.ToString();
                    if (Statuses[jobDecriptionEventArgs.JobInfo.ID.ToString()] == "Active")
                    {
                        //var sleep = 15;
                        //LogWrite(DateTime.Now.ToLongTimeString() + " Sleep " + sleep + " sec");
                        //System.Threading.Thread.Sleep(sleep * 1000);
                        waitOne.Set();
                    }
                }
            };
            jobMonitor.Finished += (sender, jobDecriptionEventArgs) =>
            {
                var monitor = (sender as JobMonitor);
                Statuses[monitor.JobId.ToString()] = "Finished";
                
                if (jobDecriptionEventArgs.JobInfo != null && jobDecriptionEventArgs.JobInfo.ErrorComment != null) {
                    LogWrite(DateTime.Now.ToLongTimeString() + " " + monitor.JobId + " parasite " + Statuses[monitor.JobId.ToString()]+(resourse != null ? " on " + resourse : "")+" with error "+jobDecriptionEventArgs.JobInfo.ErrorComment);
                    return;
                }
                LogWrite(DateTime.Now.ToLongTimeString() + " " + monitor.JobId + " parasite " + Statuses[monitor.JobId.ToString()]+(resourse != null ? " on " + resourse : ""));
                CopyRows();
            };
            jobMonitor.Run();
            Statuses.Add(jobMonitor.JobId.ToString(), "Pushed");
            LogWrite(DateTime.Now.ToLongTimeString() + " " + jobMonitor.JobId + " parasite pushed " + (resourse != null ? " on "+resourse : ""));
            LogWrite(DateTime.Now.ToLongTimeString() + " Waiting till parasite WF become active");
            waitOne.WaitOne();
           
        
        }
        private void LogWrite(string str) { 
          Console.WriteLine(str);
          try
          {
              using (var file = new StreamWriter(output.Name, true))
              {
                  file.WriteLine(str);
              }
          }
          catch (Exception ex) { 
          
          }

        }
        private void CopyRows(string[] parameters=null) {

            //string[] logLines = File.ReadAllLines(@"\\192.168." + _serverName + @"\ExecutionLogs\log.txt");
            var lastlines = ReadLastLines(@"\\192.168." + _serverName + @"\ExecutionLogs\over.txt", 1000);  
            if (File.Exists(@"\\192.168." + _serverName + @"\ExecutionLogs\over.csv"))
            {
                string[] lines2 = File.ReadAllLines(@"\\192.168." + _serverName + @"\ExecutionLogs\over.csv");
                var ret_lines = new List<string>();
                var header = "WF id;Task Id;Package;Resource;Status;T_Calculation;T_WaitInQueue;CalcEstim;CalcEstimErr;OverheadEstim;OverheadEstimErr;";
                if (parameters != null) header += String.Join(";", parameters);
                ret_lines.Add(header);

                foreach (var pair in Statuses)
                {
                    foreach (var line in lines2.Where(l => l.Contains(pair.Key))) {
                        var vals = line.Split(';');       
                        
                        var rx = new Regex(String.Format(@"""TaskId"":{0},.*""NodeName"":([^,]+),", vals[1]));
                        var match = rx.Match(lastlines);
                        if (match.Success)vals[3]=match.Groups[1].Value;
                       
                        var vals2 = vals.Take(7).ToList();
                        vals2[5] = TimeSpan.Parse(vals2[5]).TotalSeconds.ToString("0.000");
                        //vals2[6] = TimeSpan.Parse(vals2[7]).TotalSeconds.ToString("0.000");
                        if (CalculationTimes.Keys.Contains(pair.Key)) vals2.Add(CalculationTimes[pair.Key]);
                        if (CalculationTimesErr.Keys.Contains(pair.Key)) vals2.Add(CalculationTimesErr[pair.Key]);
                        if (OverheadTimes.Keys.Contains(pair.Key)) vals2.Add(OverheadTimes[pair.Key]);
                        if (OverheadTimesErr.Keys.Contains(pair.Key)) vals2.Add(OverheadTimesErr[pair.Key]);
                        if (parameters != null)
                            foreach (var param in parameters)
                            {
                                ///"Key":"ForecastSize","Value":"6"
                                rx = new Regex(String.Format(@"""Key"":""{0}"",""Value"":""([^""]+)"".*""TaskId"":{1},.*", param, vals[1]));
                                match = rx.Match(lastlines);
                                if (match.Success)
                                    vals2.Add(match.Groups[1].Value+";");
                            }
                        ret_lines.Add(String.Join(";",vals2));
                    }
                }
                
                if (!File.Exists("results/" + results + ".csv")) {
                    try
                    {
                        var csv = File.Create(results + ".csv");
                        csv.Close();
                    }
                    catch (Exception e) {
                        //LogWrite(e.Message);
                    }
                }
                try
                {
                    File.WriteAllText(results + ".csv", String.Join("\n", ret_lines).Replace(".", ","));
                }
                catch (Exception e) {
                    System.Threading.Thread.Sleep(2000);
                    File.WriteAllText(results + ".csv", String.Join("\n", ret_lines).Replace(".", ","));
                }
            }
        }

        public static String ReadLastLines(String path,int amount)
        {
            var reader = new StreamReader(path, Encoding.ASCII);
            reader.BaseStream.Seek(0, SeekOrigin.End);
            var count = 0;
            while (count <= amount)
            {
                if (reader.BaseStream.Position <= 0) break;
                reader.BaseStream.Position--;
                int c = reader.Read();
                if (reader.BaseStream.Position <= 0) break;
                reader.BaseStream.Position--;
                if (c == '\n')
                {
                    ++count;
                }
            }
            var ret = reader.ReadToEnd();
            reader.Close();
            return ret;
        }

        private void ReadConfiguration()
        {
            var settings = ConfigurationManager.AppSettings;
            _serverName = settings["ServerName"]; 
            _dataServiceEndpoint = settings["DataServiceEndpoint"];
            _tokenBrokerEndpoint = settings["TokenBrokerEndpoint"];
            _facadeEndpoint = settings["FacadeEndpoint"];

            if (settings.AllKeys.Contains("UpdatePeriod"))
            {
                int res;
                if (Int32.TryParse(settings["UpdatePeriod"], out res))
                    UpdatePeriod = res;
            }

            if (settings.AllKeys.Contains("Username"))
                UserName = settings["Username"];
            if (settings.AllKeys.Contains("Password"))
                Password = settings["Password"];
            if (settings.AllKeys.Contains("SecId"))
                SecId = settings["SecId"];
            if (settings.AllKeys.Contains("ProxyAdress"))
                ProxyAdress = settings["ProxyAdress"];
            if (settings.AllKeys.Contains("ProxyUser"))
                ProxyUser = settings["ProxyUser"];
            if (settings.AllKeys.Contains("ProxyPassword"))
                ProxyPass = settings["ProxyPassword"];
        }

        private void SetProxy()
        {
            ICredentials credentials = null;
            if (ProxyUser != null && ProxyPass != null)
                credentials = new NetworkCredential(ProxyUser, ProxyPass);

            if (ProxyAdress != null)
                if (credentials != null)
                    WebRequest.DefaultWebProxy = new WebProxy(ProxyAdress, false, null, credentials);
                else
                    WebRequest.DefaultWebProxy = new WebProxy(ProxyAdress);
        }

        private void ParseCommandLine(string[] args)
        {
            for (int i = 0; i < args.Length; i += 2)
            {
                if (args[i].ToLowerInvariant() == "/d")
                    DescriptionFile = args[i + 1];
                if (args[i].ToLowerInvariant() == "/u")
                    UserName = args[i + 1];
                if (args[i].ToLowerInvariant() == "/p")
                    Password = args[i + 1];
            }
        }

        private bool IsErrorState(JobDescription jobInfo)
        {
            if (jobInfo == null)
            {
                Console.WriteLine("Finished with error: Can't obtain monitoring information. Check you configuration!");
                return true;
            }
            if (jobInfo.State == JobState.Error)
            {
                Console.WriteLine("Finished with error: {0}\n{1}\n{2}", jobInfo.VerboseErrorComment, jobInfo.ErrorComment, jobInfo.ErrorException);
                return true;
            }
            return false;
        }

        


    }
}
