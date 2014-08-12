using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Runtime.Serialization.Formatters.Binary;
using ServiceProxies.ResourceBaseService;

namespace MITP
{
	[DataContract]
	public class Task : TaskDescription
	{
        [DataMember]
        public Dictionary<string, string> OutputParams { get; private set; }

        //[DataMember]
        public PackageEngineState PackageEngineState { get; protected set; }

        //public Estimation CurrentEstimation { get; set; }

        //[DataMember]
        //public TaskSchedule CurrentSchedule;

        [DataMember]
        public Dictionary<string, double> Estimations { get; set; }

        /* */
        private TaskSchedule _currentSchedule = null;
        [DataMember]
        public TaskSchedule CurrentSchedule 
        {
            get 
            { 
                return _currentSchedule; 
            }

            set 
            { 
                // todo : if value != null
                _currentSchedule = new TaskSchedule(value);
                //_currentSchedule = value;

                AssignedResource = ResourceTotals.FromSchedule(CurrentSchedule);
                AssignedNodes = NodeTotals.FromSchedule(CurrentSchedule);

                //Time.EstimatedStart  = _currentSchedule.EstimatedStartTime;
                //Time.EstimatedFinish = _currentSchedule.EstimatedFinishTime;
            }
        }
        /* */
        private bool _inputsProcessed = false;
        private string _inputsProcessingError = null;
        public bool AreInputsProcessed { get { return _inputsProcessed; } }

        private Eventing.EventType? _lastEvent = null;
        private string _failReason = "";

        [DataMember]
        public TaskTimeMeasurement Time { get; protected set; }

		[DataMember] 
        public TaskState State { get; protected set; }

        public bool IsFinished()
        {
            if (State == TaskState.Aborted || State == TaskState.Completed || State == TaskState.Failed)
                return true;

            return false;
        }

        public bool IsFake()
        {
            if (ExecParams.ContainsKey("RunMode") && ExecParams["RunMode"].ToLowerInvariant() == "prebilling")
                return true;

            return false;
        }

        public IncarnationParams Incarnation { get; protected set; }

        /*
        public ResourceConfig AssignedTo
        {
            private set { } // don't want to overwrite value of CurrentEstimation by deserializer
            //get { return CurrentEstimation; }  // damn WCF is too stupid for this construct
            get
            {
                return (CurrentEstimation == null)?
                    null:
                    new ResourceConfig(
                        CurrentEstimation.ProviderName,
                        CurrentEstimation.ResourceName,
                        CurrentEstimation.Cores
                    );
            }
        }
        */

        [DataMember]
        public ResourceTotals AssignedResource
        {
            get;
            private set; 
            /*
            get
            {
                lock (_taskLock)
                {
                    return ResourceTotals.FromSchedule(CurrentSchedule);
                }
            }
            */
        }

        [DataMember]
        public IEnumerable<NodeTotals> AssignedNodes
        {
            get;
            private set; // { }

            /*
            get
            {
                lock (_taskLock)
                {
                    var res = NodeTotals.FromSchedule(CurrentSchedule);
                    return res;
                }
            }
            */
        }

        #region Constructors

        public Task(TaskDescription description)
            : base(description)
        {
            if (Priority == TaskPriority.Urgent)
            {
                if (!ExecParams.ContainsKey("MinTime"))
                    ExecParams["MinTime"] = "0";                
                
                if (!ExecParams.ContainsKey("MaxTime"))
                    ExecParams["MaxTime"] = ExecParams["MinTime"];
            }

            OutputParams = new Dictionary<string, string>();

            Time = new TaskTimeMeasurement();
            Time.Started(TaskTimeMetric.Postponed);
            if (this.IsFake())
                Time.Edge(started: TaskTimeMetric.Queued, finished: TaskTimeMetric.Postponed);

            Estimations = null;
            CurrentSchedule = null;
            Incarnation = new IncarnationParams();

            State = TaskState.Defined;

            string stepName = ExecParams.ContainsKey("StepName")? ExecParams["StepName"]: null;
            string storageRoot = IOProxy.Storage.BuildPath(UserId, WfId, stepId: TaskId.ToString(), stepName: stepName);
            this.PackageEngineState = new PackageEngineState(description, storageRoot); // todo : measure PackageEngineState expenses
        }

        public Task(Task otherTask)
            : base(otherTask)
        {
            if (otherTask.OutputParams != null)
                OutputParams = new Dictionary<string, string>(otherTask.OutputParams);

            if (otherTask.PackageEngineState != null)
                this.PackageEngineState = (PackageEngineState) otherTask.PackageEngineState.Clone();

            Time = new TaskTimeMeasurement(otherTask.Time);
            Incarnation = new IncarnationParams(otherTask.Incarnation);

            if (otherTask.Estimations != null)
                Estimations = new Dictionary<string, double>(otherTask.Estimations);

            CurrentSchedule = null;
            if (otherTask.CurrentSchedule != null)
            {
                CurrentSchedule = new TaskSchedule(otherTask.CurrentSchedule);

                // immutable: 
                this.AssignedResource = otherTask.AssignedResource;
                this.AssignedNodes = otherTask.AssignedNodes;
            }

            State = otherTask.State;

            _inputsProcessed = otherTask._inputsProcessed;
            _inputsProcessingError = otherTask._inputsProcessingError;

            _failReason = otherTask._failReason;
            if (otherTask._lastEvent != null && otherTask._lastEvent.HasValue)
                _lastEvent = otherTask._lastEvent.Value;
        }

        #endregion

        #region Save/Load and Serialize

        private static string GetTaskPersistenceFilePath(ulong taskId)
		{
			if (!Directory.Exists(CONST.Path.PersistenceFileNames.Folder))
				Directory.CreateDirectory(CONST.Path.PersistenceFileNames.Folder);

			return String.Format(CONST.Path.PersistenceFileNames.Task, taskId);
		}


		[NonSerialized]
		private volatile object _taskLock = new object();

        public void Save() // todo : Task.Save()
		{
			lock (_taskLock)
			{				
				var formatter = new BinaryFormatter();
				string filePath = GetTaskPersistenceFilePath(TaskId);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
					formatter.Serialize(fileStream, this);
				}

				Log.Debug("Задача " + this.TaskId.ToString() + " сохранена");
			}
		}

        public static Task Load(ulong taskId) // todo : Task.Load()
		{
			//lock (_taskLock)
			{
				var formatter = new BinaryFormatter();
				string filePath = GetTaskPersistenceFilePath(taskId);

				Task loadedTask = null;
				using (var fileStream = new FileStream(filePath, FileMode.Open))
                {
					loadedTask = (Task) formatter.Deserialize(fileStream);
				}

				return loadedTask;
			}
		}

        #endregion

        private string GetFtpFolder(NodeConfig nodeConfig, Resource resource, CopyPhase phase)
        {
            string ftpFolder = resource.Nodes.First(n => n.NodeName == nodeConfig.NodeName).DataFolders.ExchangeUrlFromSystem;
            string incarnatedFtpFolder = IncarnationParams.IncarnatePath(ftpFolder, TaskId, phase);
            return incarnatedFtpFolder;
        }

        //private void ApplyAdapters(IEnumerable<AbstractAdapter> adapters, string ftpFolder)
        //{
        //    foreach (var adapter in adapters)
        //    {
        //        if (adapter.Mathces(this))
        //        {
        //            Log.Info(String.Format(
        //                "Задача {1}, запущен адаптер {0}", adapter.ToString(), this.TaskId
        //            ));

        //            if (LaunchMode == TaskLaunchMode.Manual)
        //                adapter.OnManualStart(this, ftpFolder);
        //            else
        //                adapter.OnStart(this, ftpFolder);
        //        }
        //    }
        //}

        public void MarkAsReadyToExecute()
        {
            lock (_taskLock)
            {
                this.State = TaskState.ReadyToExecute;
                Time.Edge(started: TaskTimeMetric.Queued, finished: TaskTimeMetric.Postponed);

                if (_inputsProcessed && _inputsProcessingError != null)
                    throw new Exception("Inputs processed with error: " + _inputsProcessingError);
            }
        }

        public bool ProcessInputs() // todo : should be private. Fix after PackageBase fix
        {
            lock (_taskLock)
            {
                bool isProcesedNow = false;
                if (!_inputsProcessed)
                {
                    if (this.Priority == TaskPriority.Urgent && (!this.ExecParams.ContainsKey("MinTime") || !this.ExecParams.ContainsKey("MaxTime")))
                        throw new Exception("Urgent tasks should have 'MinTime' and 'MaxTime' params");

                    var inputFilesTime = TimeSpan.Zero;
                    Time.AddToOverheads(TaskTimeOverheads.PackageBase, () =>
                    {
                        Log.Debug("Processing inputs for task " + TaskId.ToString());

                        try
                        {
                            this.Params = PackageBaseProxy.UpdateInputs(this.PackageEngineState, this.Params);
                            this.Params = PackageBaseProxy.UpdateInputs(this.PackageEngineState, this.InputFiles);
                            this.Incarnation = PackageBaseProxy.ProcessInputFiles(this.PackageEngineState, out inputFilesTime);

                            bool expectGroups;
                            var expectedOutFiles = PackageBaseProxy.ListExpectedOutputs(this.PackageEngineState, out expectGroups);
                            this.Incarnation.ExpectedOutputFileNames = expectedOutFiles.ToArray();
                            this.Incarnation.CanExpectMoreFiles = expectGroups;

                            Log.Debug(String.Format("Expected outputs for task {2} are {0} {1}",
                                String.Join(", ", expectedOutFiles.Select(name => "'" + name + "'")),
                                expectGroups ? "with groups" : "",
                                TaskId
                            ));
                        }
                        catch (Exception e)
                        {
                            _inputsProcessingError = e.Message;
                            Log.Error(String.Format("Error while processing inputs for task {0}: {1}", this.TaskId, e));

                            if (this.State == TaskState.ReadyToExecute)
                                throw;
                        }
                    });

                    Time.OverheadsSpecial["pb/inputFiles"] = inputFilesTime;

                    _inputsProcessed = true;
                    isProcesedNow = true;
                }

                return isProcesedNow;
            }
        }

        public void Run(TaskSchedule schedule, IEnumerable<Resource> resources)
		{
            lock (_taskLock)
            {
                try
                {
                    var execStarted = DateTime.Now;
                    CurrentSchedule = schedule;

                    Params[CONST.Params.Method] = Method;

                    var resource = resources.First(r => r.ResourceName == schedule.ResourceName);
                    string incarnatedFtpFolder = GetFtpFolder(schedule.Nodes.First(), resource, CopyPhase.In);

                    if (PackageBaseProxy.GetSupportedPackageNames()
                            .Any(name => String.Equals(name, Package, StringComparison.InvariantCultureIgnoreCase))
                       )
                    {
                        //ProcessInputs();

                        Time.AddToOverheads(TaskTimeOverheads.InputFilesCopy, () =>
                        {
                            Log.Debug("Uploading incarnated inputs");
                            foreach (var file in Incarnation.FilesToCopy)
                            {
                                Log.Debug(file.FileName + ": started");
                                IOProxy.Ftp.MakePath(incarnatedFtpFolder + Path.GetDirectoryName(file.FileName).Replace("\\", "/"));
                                Log.Debug(file.FileName + ": path been made");
                                IOProxy.Storage.Download(file.StorageId, incarnatedFtpFolder + file.FileName);
                                Log.Debug(file.FileName + ": downloaded");
                            }
                            Log.Debug("Uploading incarnated inputs done");
                        });
                    }
                    else
                    {
                        //ApplyAdapters(Broker.Adapters.Where(a => a.Type == AdapterType.Machine), incarnatedFtpFolder);
                        //ApplyAdapters(Broker.Adapters.Where(a => a.Type == AdapterType.Package), incarnatedFtpFolder);
                        //ApplyAdapters(Broker.Adapters.Where(a => a.Type == AdapterType.Mixed), incarnatedFtpFolder);
                    }

                    Incarnation.PackageName = Package;
                    Incarnation.UserCert = UserCert;

                    if (String.IsNullOrWhiteSpace(Incarnation.CommandLine))
                        throw new Exception("Impossible to run task with empty command line");

                    if (!Incarnation.CommandLine.Contains("{0}") && 
                        Incarnation.CommandLine.StartsWith(Package, StringComparison.InvariantCultureIgnoreCase))
                    {
                        Incarnation.CommandLine = "{0}" + Incarnation.CommandLine.Substring(Package.Length);
                    }
                
                    Log.Stats("T_adapters", this.WfId, this.TaskId, DateTime.Now - execStarted);

                    Time.AddToOverheads(TaskTimeOverheads.Provider, () =>
                    {
                        //var provider = Broker.ProviderByName(resource.ProviderName);
                        var controller = Discovery.GetControllerFarm(resource);

                        try
                        {
                            //Incarnation.ProvidedTaskId = provider.Run(this.TaskId, this.Incarnation, resource, schedule.Nodes);
                            var runContext = new ServiceProxies.ControllerFarmService.TaskRunContext()
                            {
                                TaskId      = this.TaskId,

                                //Incarnation = this.Incarnation,
                                UserCert = this.UserCert,
                                PackageName = this.Incarnation.PackageName,
                                CommandLine = this.Incarnation.CommandLine,

                                InputFiles  = this.Incarnation.FilesToCopy.Select(f => new ServiceProxies.ControllerFarmService.FileContext()
                                {
                                    FileName  = f.FileName,
                                    StorageId = f.StorageId,
                                }).ToArray(),
                                
                                ExpectedOutputFileNames = this.Incarnation.ExpectedOutputFileNames.ToArray(),
                                

                                NodesConfig = schedule.Nodes.Select(n => new ServiceProxies.ControllerFarmService.NodeRunConfig()
                                {
                                    ResourceName = n.ResourceName,
                                    NodeName     = n.NodeName,
                                    Cores        = n.Cores
                                }).ToArray()
                            };

                            Log.Debug("Running task on controller: " + TaskId.ToString());
                            controller.Run(runContext);
                            Log.Debug("Run done: " + TaskId.ToString());

                            controller.Close();
                        }
                        catch (Exception e)
                        {
                            controller.Abort();
                            Log.Error("Exception on Task.Run for task " + this.TaskId + ": " + e.ToString());

                            throw;
                        }
                    });                        

                    State = TaskState.Started;
                    Time.Started(TaskTimeMetric.Calculation);

                    Log.Stats("T_clust_start", this.WfId, this.TaskId, DateTime.Now);
                    _lastEvent = Eventing.EventType.TaskStarted;
                }
                catch (Exception e)
                {
                    Log.Error(String.Format("Error on executing task {0}: {1}\n{2}",
                        TaskId, e.Message, e.StackTrace
                    ));

                    Fail(reason: e.Message);
                }
            }
		}

        
		public void Abort(Resource resource)
		{
            lock (_taskLock)
            {
                if (State == TaskState.Started)
                {
                    State = TaskState.Aborted;

                    Time.AddToOverheads(TaskTimeOverheads.Provider, () =>
                    {
                        //var provider = Broker.ProviderByName(resource.ProviderName);
                        var controller = Discovery.GetControllerFarm(resource);

                        try
                        {
                            //provider.Abort(this.Incarnation.ProvidedTaskId, resource, CurrentSchedule.Nodes);
                            controller.Abort(this.TaskId); // service's method
                            controller.Close();
                        }
                        catch (Exception e)
                        {
                            controller.Abort(); // drop connection

                            Log.Error(String.Format("Exception while aborting task {0} on {1}: {2}",
                                TaskId, resource.ResourceName, e
                            ));
                        }
                    });

                    Time.Finished(TaskTimeMetric.Calculation);
                    //Time.Finished(TaskTimeMetric.Brokering);
                } 
                else
                {
                    Log.Warn("Aborting non-started task " + TaskId.ToString());
                    State = TaskState.Aborted;
                }

                //todo: _lastEvent = Aborted; !!!!!!!!!!!!!!!!!!111
                _failReason = "Aborted";
                _lastEvent = Eventing.EventType.TaskFailed;
                //Over();

                Log.Info(String.Format("Task {0} aborted", TaskId));
            }
		}

        #region Updating state

        public void SendEvents()
        {
            lock (_taskLock)
            {
                if (_lastEvent != null && _lastEvent.HasValue)
                {
                    if (_lastEvent.Value == Eventing.EventType.TaskCompleted || 
                        _lastEvent.Value == Eventing.EventType.TaskFailed)
                    {
                        Over();
                    }

                    Eventing.Send(_lastEvent.Value, this.WfId, this.TaskId, _failReason);
                    _lastEvent = null;
                    //_failReason = "";
                }
            }
        }

        public void Update(IEnumerable<Resource> resources)
        {
            lock (_taskLock)
            {
                string failReason = null;
                ServiceProxies.ControllerFarmService.TaskStateInfo stateInfo = null;

                var resource = resources.FirstOrDefault(r => r.ResourceName == CurrentSchedule.ResourceName);
                if (resource == null) // resource was available, but now isn't
                {
                    failReason = "Resource became unreachable (check if ControllerFarm is still running)"; // todo : retries on Task.Update
                }
                else
                {
                    //var provider = Broker.ProviderByName(resource.ProviderName);
                    var controller = Discovery.GetControllerFarm(resource);

                    try
                    {
                        /*
                        Tuple<TaskState, string> stateTuple = null;
                        //Time.AddExpenses(TaskTimeExpenses.Provider, () => // todo: time measures on other provider's actions
                        //{
                            stateTuple = provider.GetTaskState(TaskId, Incarnation.ProvidedTaskId, resource, CurrentSchedule.Nodes);
                        //});

                        var state = stateTuple.Item1;
                        string reason = stateTuple.Item2;
                        */

                        stateInfo = controller.GetTaskStateInfo(TaskId);
                        controller.Close();
                    }
                    catch (Exception e)
                    {
                        controller.Abort();
                        Log.Warn(String.Format("Exception on updating task's {0} state: {1}", this.TaskId, e));

                        failReason = "Controller failed while updating task: " + e.Message;
                    }
                }

                if (stateInfo != null)
                {
                    var state = stateInfo.State;
                    string reason = stateInfo.StateComment;

                    if (state != this.State)
                        Log.Debug(String.Format("Task {0}: new state = {1}{2}", TaskId, state, String.IsNullOrEmpty(reason) ? "" : ", reason = " + reason));

                    // todo : log new task state

                    if (state == TaskState.Completed)
                    {
                        try
                        {
                            Time.Finished(TaskTimeMetric.Calculation);
                            string incarnatedFtpFolder = GetFtpFolder(CurrentSchedule.Nodes.First(), resource, CopyPhase.Out);
                            Complete(incarnatedFtpFolder);
                        }
                        catch (Exception e)
                        {
                            Log.Error(String.Format(
                                "Couldn't complete task {0}: {1}",
                                TaskId, e.ToString()
                            ));

                            //Fail(errMsg);
                            failReason = e.Message;
                        }
                    }
                    else
                    if (state == TaskState.Failed)
                    {
                        failReason = reason;
                    }
                }

                if (!String.IsNullOrEmpty(failReason))
                {
                    Time.Finished(TaskTimeMetric.Calculation);
                    Fail(failReason);
                }
            }
        }

        internal void Complete(string ftpFolder)
		{
            lock (_taskLock)
            {
                /*
                foreach (var adapter in Broker.Adapters)  // todo: adapters priority based on it's type OnFinish()?
                    if (adapter.Mathces(this))
                    {
                        Log.Info(String.Format(
                            "Задача {1}, запущен адаптер {0}", adapter.ToString(), this.TaskId
                        ));

                        adapter.OnFinish(this, ftpFolder);
                    }
                */

                if (!String.IsNullOrEmpty(ftpFolder))
                {
                    var outputFilesTime = TimeSpan.Zero;
                    Time.AddToOverheads(TaskTimeOverheads.OutputFilesCopy, () =>  // todo : PB time here
                    {
                        IEnumerable<TaskFileDescription> outFiles = null;
                        var outParams = PackageBaseProxy.ProcessOutputs(this.PackageEngineState, ftpFolder, out outFiles, out outputFilesTime);

                        this.OutputFiles = outFiles.ToArray();
                        this.OutputParams = outParams;

                        //foreach (var pair in outParams)
                        //    this.Params[pair.Key] = pair.Value;
                    });

                    Time.OverheadsSpecial["pb/outputFiles"] = outputFilesTime;
                }

                State = TaskState.Completed;
                //Over();

                Log.Info(String.Format("Task {0} completed", this.TaskId));
                _lastEvent = Eventing.EventType.TaskCompleted;
            }
		}

        internal void Fail(string reason = "") // todo: task.FailReason
		{
            lock (_taskLock)
            {
                State = TaskState.Failed; // todo: Time.Edges + OversLog on Task.State changes?
                //Over();

                Log.Info(String.Format("Task {0} failed. Reason: {1}", this.TaskId, reason));
                _lastEvent = Eventing.EventType.TaskFailed;
                _failReason = reason;
            }
		}

        private static object _csvFilesLock = new object();

        private void WriteModelCoefs()
        {
            lock (_csvFilesLock)
            {
                try
                {
                    if (this.CurrentSchedule != null &&
                        this.CurrentSchedule.Estimation != null &&
                        this.CurrentSchedule.Estimation.ByModel != null &&
                        this.CurrentSchedule.Estimation.ByModel != null &&
                        this.CurrentSchedule.Estimation.ByModel.CalculationTime != null &&
                        this.CurrentSchedule.Estimation.ByModel.CalculationTime.IsSet)
                    {
                        try
                        {
                            // todo : remove history hack
                            PackageBaseProxy.AddHistorySample(new HistorySample(
                                this.Package, this.CurrentSchedule.ResourceName,
                                this.CurrentSchedule.Nodes.ToArray(),
                                new Dictionary<string, string>(this.Params),
                                new Dictionary<string, double>(this.CurrentSchedule.Estimation.ModelCoeffs),
                                this.Time.Duration[TaskTimeMetric.Calculation],
                                new Easis.PackageBase.Engine.PackageEngine(
                                    (Easis.PackageBase.Engine.CompiledModeDef)this.PackageEngineState.CompiledDef,
                                    (Easis.PackageBase.Engine.PackageEngineContext)this.PackageEngineState.EngineContext.Clone())
                            ));
                        }
                        catch (Exception e)
                        {
                            Log.Error("Could not add history sample: " + e.ToString());
                        }


                        double estimated = this.CurrentSchedule.Estimation.ByModel.CalculationTime.Value;
                        double real = this.Time.Duration[TaskTimeMetric.Calculation].TotalSeconds;

                        var invariantCulture = System.Globalization.CultureInfo.InvariantCulture;

                        var table = new List<string[]>()
                        {
                            new[] { "TaskId",  this.TaskId.ToString() },
                            new[] { "Package", this.Package },
                            new[] { "State",   this.State.ToString() },

                            new[] { "Resource.Node", 
                                this.CurrentSchedule.ResourceName + "." + String.Join("+", this.CurrentSchedule.Nodes.Select(n => n.NodeName)) },
                            
                            new[] { "DateTimeStarted", this.Time.WhenStarted[TaskTimeMetric.Calculation].ToString() },
                            new[] { "DateTimeEnded",   this.Time.WhenFinished[TaskTimeMetric.Calculation].ToString() },
                            
                            new[] { "EstimatedTime",  estimated.ToString(invariantCulture) },
                            new[] { "RealTime",       real.ToString(invariantCulture) },
                            
                            new[] { "DiffAbs",  (estimated - real).ToString(invariantCulture) },
                            new[] { "DiffRel",  ((estimated - real) / real).ToString(invariantCulture) },

                            new[] { "Coeffs", 
                                    "[" +
                                        String.Join(", ",
                                            this.CurrentSchedule.Estimation.ModelCoeffs
                                                .Select(pair => String.Format(invariantCulture, "{{\"{0}\": \"{1}\"}}", pair.Key, pair.Value))
                                        ) +
                                    "]"
                                  },

                            new[] { "Params", 
                                    "[" + String.Join(", ",
                                            this.Params.Select(pair => String.Format("{{\"{0}\": \"{1}\"}}", pair.Key, pair.Value))
                                     ) + "]"
                                  },
                        };

                        string headerFileName = CONST.Path.ModelCoefHeadersFile;
                        string headerContents = String.Join(";", table.Select(row => row.First())) + Environment.NewLine;
                        File.WriteAllText(headerFileName, headerContents);

                        string csvFileName = CONST.Path.ModelCoefCsvFile;
                        string csvLine = String.Join(";", table.Select(row => row.Last())) + Environment.NewLine;
                        File.AppendAllText(csvFileName, csvLine);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Could not write model params to file: " + e.ToString());
                }
            }
        }

        private void Over()
        {
            WriteModelCoefs();

            Log.Debug("Task " + TaskId.ToString() + " is over");

            var duration = this.Time.Duration.ToArray();
            var overTotals = this.Time.OverheadTotals.ToArray().ToDictionary(p => p.Key, p => p.Value);
            var overAvgs = this.Time.OverheadAverages.ToArray().ToDictionary(p => p.Key, p => p.Value);

            Time.Finished(TaskTimeMetric.Brokering);
            Log.Debug("Finished brokering");

            string timeStat = "";
            timeStat += "Duration:" + Environment.NewLine;
            timeStat += String.Join(Environment.NewLine, duration.Select(pair => "    " + pair.Key.ToString() + " = " + pair.Value.ToString())) + Environment.NewLine;
            timeStat += "Overheads:" + Environment.NewLine;
            timeStat += String.Join(Environment.NewLine, overTotals.Select(pair => "    " + pair.Key.ToString() + " = " + pair.Value.ToString()));

            Log.Over(
                "{0} -- {1} {2} #{3} {4}{7}{5}{7}{6}",
                DateTime.Now, Package, State.ToString(), TaskId,
                (CurrentSchedule == null)?
                    "":
                    CurrentSchedule.ResourceName + 
                        " [" + String.Join(", ", CurrentSchedule.Nodes.Select(n => n.Cores.ToString())) + "]", 
                timeStat,
                ToJsonString(),
                Environment.NewLine
            );

            try
            {
                var sb = new StringBuilder();
                var head = new StringBuilder();
                sb.AppendFormat("{0};", WfId);
                head.Append("WfId;");

                sb.AppendFormat("{0};", TaskId);
                head.Append("TaskId;");

                sb.AppendFormat("{0};", Package);
                head.Append("Package;");

                sb.AppendFormat("{0};", CurrentSchedule.ResourceName);
                head.Append("ResourceName;");

                sb.AppendFormat("{0};", State);
                head.Append("State;");

                sb.AppendFormat("{0};", duration.First(p => p.Key == TaskTimeMetric.Calculation).Value);
                head.Append("T_Calculation;");

                var avgSb = new StringBuilder(sb.ToString());

                var overNames = new[] {
                    TaskTimeOverheads.WaitInQueue,

                    TaskTimeOverheads.PackageBase,
                    TaskTimeOverheads.Estimation,
                    TaskTimeOverheads.Scheduler,        
                    TaskTimeOverheads.Provider,         
                    TaskTimeOverheads.InputFilesCopy,
                    TaskTimeOverheads.OutputFilesCopy,                    

                    TaskTimeOverheads.Other,
                    TaskTimeOverheads.All,
                };

                foreach (var overName in overNames)
                {
                    if (!overTotals.ContainsKey(overName))
                        overTotals[overName] = TimeSpan.Zero;

                    if (!overAvgs.ContainsKey(overName))
                        overAvgs[overName] = TimeSpan.Zero;
                }

                foreach (var overName in overNames)
                {
                    sb.AppendFormat("{0};", overTotals[overName]);
                    avgSb.AppendFormat("{0};", overAvgs[overName]);
                    head.AppendFormat("T_{0};", overName);
                }

                //sb.AppendFormat("{0};", AssignedResource.ResourceName);
                //sb.AppendFormat("{0};", Time.Duration[TaskTimeMetric.Calculation]);
                //sb.AppendFormat("{0};", Time.Duration[TaskTimeMetric.Postponed]);
                //sb.AppendFormat("{0};", Time.Duration[TaskTimeMetric.Queued]);
                //sb.AppendFormat("{0};", Time.Overheads[TaskTimeOverheads.All]);
                //sb.AppendFormat("{0};", Time.Overheads[TaskTimeOverheads.Other]);
                //sb.AppendFormat("{0};", Time.Overheads[TaskTimeOverheads.PackageBase]);
                //sb.AppendFormat("{0};", Time.Overheads[TaskTimeOverheads.Scheduler]);
                //sb.AppendFormat("{0};", Time.Overheads[TaskTimeOverheads.Provider]);
                //sb.AppendFormat("{0};", Time.Overheads[TaskTimeOverheads.InputFilesCopy]);
                //sb.AppendFormat("{0};", Time.Overheads[TaskTimeOverheads.OutputFilesCopy]);
                //sb.AppendFormat("{0};", 0);

                Time.OverheadsSpecial["pb"] = Time.OverheadTotals[TaskTimeOverheads.PackageBase] + Time.OverheadTotals[TaskTimeOverheads.OutputFilesCopy];
                var specialLine = new List<string>() { TaskId.ToString() };
                var specialHeader = new List<string>() { "TaskId" };
                foreach (string key in Time.OverheadsSpecial.Keys)
                {
                    specialHeader.Add(key);
                    specialLine.Add(Time.OverheadsSpecial[key].TotalSeconds.ToString());
                }

                lock (_csvFilesLock)
                {
                    File.AppendAllText(CONST.Path.OverCsvFile, sb.ToString() + Environment.NewLine);
                    File.AppendAllText(CONST.Path.OverAvgCsvFile, avgSb.ToString() + Environment.NewLine);
                    File.WriteAllText(CONST.Path.OverHeadersFile, head.ToString() + Environment.NewLine);

                    File.AppendAllText(CONST.Path.OverSpecFile, String.Join(";", specialLine) + Environment.NewLine);
                    File.WriteAllText(CONST.Path.OverSpecHeadersFile, String.Join(";", specialHeader) + Environment.NewLine);
                }
            }
            catch (Exception e)
            {
                Log.Warn(String.Format("Problem with tasks over: {0}\n{1}", e.Message, e.StackTrace));
            }

        }

        #endregion
	}
}
