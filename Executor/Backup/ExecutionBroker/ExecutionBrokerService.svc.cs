using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.IO;
using PFX = System.Threading.Tasks;
using ServiceProxies.ResourceBaseService;

namespace MITP
{
    [ServiceBehavior(Namespace = "http://escience.ifmo.ru/nano/services/", InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public partial class ExecutionBrokerService : IExecutionBrokerService // todo : fully implement ExecutionBrokerService
    {
        private static List<Task> _tasksQueue = new List<Task>();
        private static List<TaskDependency> _taskDependencies = new List<TaskDependency>();
        private static readonly object _queueLock = new object();
        private static readonly object _updateLock = new object();

        private static readonly HashSet<ulong> _readyMarks   = new HashSet<ulong>();
        private static readonly List<Task>     _definesQueue = new List<Task>();

        private static readonly object _briefsLock = new object();
        private static BriefTaskInfo[] _briefs = new BriefTaskInfo[0];

        private static readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(0.3);
        private static DateTime _lastUpdateTime = DateTime.Now - _updateInterval;
        private static bool _updating = false;

        private static object _constructorLock = new object();
        ExecutionBrokerService()
        {
            lock (_constructorLock)
            {
                /*
                //System.Net.ServicePointManager.MaxServicePoints = 4;
                System.Net.ServicePointManager.DefaultConnectionLimit = 48;
                System.Net.ServicePointManager.MaxServicePointIdleTime = 10000; // milliseconds
                System.Net.ServicePointManager.UseNagleAlgorithm = true;

                //System.Threading.ThreadPool.SetMaxThreads(100, 100);
                */
            }
        }


        #region IExecutionBrokerService Members

        public ulong GetNewTaskId()
        {
            //lock (_updateLock)
            lock (_queueLock)
            {
                Log.Debug("Generating new id...");
                ulong generatedId = Broker.GetNewTaskId();
                Log.Debug("Generated id = " + generatedId.ToString());
                return generatedId;
            }
        }

        public IEnumerable<BriefTaskInfo> GetBriefTaskList()
        {
            /*
            lock (_briefsLock)
            {
                return _briefs;
            }
            */
            /**/
            lock (_queueLock)
            {
                //var resources = ResourceBase.GetAllResources();

                var briefs = _tasksQueue.Select(task => new BriefTaskInfo
                {
                    WfId   = task.WfId,
                    TaskId = task.TaskId,
                    UserId = task.UserId,

                    Package = task.Package,

                    State  = task.State,

                    ResourceName = 
                        (task.CurrentSchedule == null)? 
                            null: 
                            task.CurrentSchedule.ResourceName,

                    NodeAddresses = 
                        (task.CurrentSchedule == null || String.IsNullOrWhiteSpace(task.CurrentSchedule.ResourceName))?
                            null:
                            task.AssignedNodes.Select(n => n.NodeAddress).ToArray()
                            /*
                            resources
                                .First(r => r.ResourceName == task.CurrentSchedule.ResourceName)
                                .Nodes.Join(
                                    task.CurrentSchedule.Nodes,
                                        node => node.NodeName, config => config.NodeName,
                                        (node, config) => node.NodeAddress
                                ).ToArray()
                           */       
                                //.NodesInConfig(task.AssignedTo)
                                //.Where(tup => tup.Item1 != null)
                                //.Select(tup => tup.Item1.NodeAddress).ToArray()                        
                }).ToArray();

                return briefs;
            }
            /**/
        }

        // obsolete
        private void Define(IEnumerable<TaskDescription> tasks, IEnumerable<TaskDependency> dependencies)
        {
            try
            {
                Log.Info(String.Format("Trying to define tasks [{0}]", String.Join(", ", tasks.Select(t => t.TaskId))));
                var supportedPackages = PackageBaseProxy.GetSupportedPackageNames();

                foreach (var taskDesription in tasks)
                {
                    try
                    {
                        if (!supportedPackages.Any(p => String.Equals(p, taskDesription.Package, StringComparison.InvariantCultureIgnoreCase)))
                            throw new Exception(String.Format("Package {0} is not supported.", taskDesription.Package));

                        string descrJson = taskDesription.ToJsonString();
                        Log.Info(String.Format("Trying to define task {0}: {1}", taskDesription.TaskId, descrJson));

                        var task = new Task(taskDesription);
                        string taskJson = task.ToJsonString();
                        Log.Info(String.Format("Task defined {0}: {1}", task.TaskId, taskJson));

                        lock (_queueLock)
                        {
                            _tasksQueue.Add(task);
                            Log.Info(String.Format("Task {0} ({1}) queued", task.TaskId, task.Package));
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(String.Format("Error defining task: {0}\n{1}", e.Message, e.StackTrace));
                        throw;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(String.Format("Error while tasks defining: {0}\n{1}", e.Message, e.StackTrace));
                throw;
            }
        }
        
        public void DefineTask(TaskDescription taskDesription)
        {
            try
            {
                string descrJson = taskDesription.ToJsonString();
                Log.Info(String.Format("Trying to define task {0}: {1}", taskDesription.TaskId, descrJson));

                var supportedPackages = PackageBaseProxy.GetSupportedPackageNames();
                if (!supportedPackages.Any(p => String.Equals(p, taskDesription.Package, StringComparison.InvariantCultureIgnoreCase)))
                    throw new Exception(String.Format("Package {0} is not supported in Package Base", taskDesription.Package));

//                lock (_updateLock)
                {
                    lock (_queueLock)
                    {
                        var task = new Task(taskDesription);

                        _definesQueue.Add(task);
                        Log.Info(String.Format("Task {0} ({1}) def-queued", task.TaskId, task.Package));
                        /*
                        _tasksQueue.RemoveAll(t => t.TaskId == task.TaskId);
                        _tasksQueue.Add(task);
                        Log.Info(String.Format("Task {0} ({1}) queued", task.TaskId, task.Package));
                        */
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(String.Format("Error defining task {2}: {0}\n{1}", e.Message, e.StackTrace, taskDesription.TaskId));
                throw;
            }
        }

        public void DefineDependencies(IEnumerable<TaskDependency> dependencies)
        {
            try
            {
                string depsList = String.Join(", ", dependencies.Select(d => String.Format("{0}->{1}", d.ChildTaskId, d.ParentTaskId)));
                Log.Info("Trying to define dependencies (child->parent): " + depsList);

                var selfDependencies = dependencies.Where(d => d.ParentTaskId == d.ChildTaskId);
                if (selfDependencies.Any())
                {
                    Log.Warn(
                        "Ignoring self-dependencies detected for tasks " + 
                        String.Join(", ", selfDependencies.Select(d => d.ChildTaskId))
                    );

                    dependencies = dependencies.Where(d => d.ParentTaskId != d.ChildTaskId).ToArray();
                }

                lock (_queueLock)
                {
                    _taskDependencies.AddRange(dependencies);
                    Log.Info(String.Format("{0} dependencies defined", dependencies.Count()));
                }
            }
            catch (Exception e)
            {
                Log.Error(String.Format("Error defining dependencies: {0}\n{1}", e.Message, e.StackTrace));
                throw;
            }
        }

        public void Execute(IEnumerable<ulong> taskIds)
        {
            try
            {
                Log.Info(String.Format(
                    "Received command to execute tasks: [{0}]",
                    String.Join(", ", taskIds.Select(id => id.ToString()))
                ));

//                lock (_updateLock)
                {
                    lock (_queueLock)
                    {
                        // todo : check dependencies

                        foreach (ulong currentTaskId in taskIds)
                        {
                            _readyMarks.Add(currentTaskId);

                            /*
                            var task = _tasksQueue.Single(t => t.TaskId == currentTaskId);

                            try
                            {
                                task.MarkAsReadyToExecute();
                                //task.ProcessInputs();
                            }
                            catch (Exception te)
                            {
                                Log.Error(String.Format(
                                    "Error on execute task {2}: {0}\n{1}",
                                    te.Message, te.StackTrace, task.TaskId
                                ));

                                task.Fail(te.Message);
                                task.SendEvents();

                                throw;
                            }
                            */
                        }
                    }
                }
            }
            catch (Exception e) // on exec in general
            {
                Log.Error(String.Format(
                    "Error on execute tasks [{2}]: {0}\n{1}",
                    e.Message, e.StackTrace,
                    String.Join(", ", taskIds)
                ));

                /*
                ulong wfId = 0; // todo: think
                foreach (ulong taskId in taskIds)
                    Eventing.Send(Eventing.EventType.TaskFailed, wfId, taskId, "Error on execute: " + e.Message);
                */

                throw;
            }
        }

        public void Abort(IEnumerable<ulong> taskId)
        {
            lock (_updateLock)
            {
                lock (_queueLock)
                {
                    try
                    {
                        var resources = ResourceBase.GetAllResources();

                        foreach (ulong id in taskId)
                        {
                            var task = _tasksQueue.FirstOrDefault(t => t.TaskId == id);

                            if (task != null)
                            {
                                var resource = resources.First(r => r.ResourceName == task.CurrentSchedule.ResourceName);
                                task.Abort(resource);
                            }

                            _definesQueue.RemoveAll(t => t.TaskId == id);
                            _readyMarks.RemoveWhere(m => m == id);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(String.Format(
                            "Error on tasks [{1}] abort: {0}", e,
                            String.Join(", ", taskId.Select(id => id.ToString()))
                        ));

                        throw;
                    }
                }
            }
        }

        public Task GetInfo(ulong taskId)
        {
            //Log.Debug("GetInfo: " + taskId.ToString());
            lock (_queueLock)
            {
                try
                {
                    //Log.Debug("GetInfo inside lock: " + taskId.ToString());
                    //Update(); // todo : think about Update() in GetInfo()

                    var task = new Task(
                        _tasksQueue.FirstOrDefault(t => t.TaskId == taskId) ?? 
                        _definesQueue.First(t => t.TaskId == taskId)
                    );

                    return task;
                }
                catch (Exception e)
                {
                    Log.Error(String.Format(
                        "Error on task get info: {0}\n{1}",
                        e.Message, e.StackTrace
                    ));

                    throw;
                }
            }
        }

        public void Update()
        {
            if (!_updating)
            {
                lock (_updateLock)
                {
                    if (!_updating && (
                            (DateTime.Now > _lastUpdateTime + _updateInterval) || 
                            (DateTime.Now < _lastUpdateTime))) // system time was decreased
                    {
                        lock (_queueLock)
                        {
                            if (!_readyMarks.Any() && !_definesQueue.Any() && _tasksQueue.All(t => t.IsFinished() || (t.State == TaskState.Defined && !t.IsFake())))
                                return;
                        }

                        Log.Debug("Updating queue...");
                        _updating = true;

                        try
                        {
                            var resources = ResourceBase.GetAllResources(updateThem: true);

                            // Copy tasks from queue and process them
                            var selectedTasks = new List<Task>();
                            //object selectLock = new object();
                            TaskDependency[] dependencies;

                            lock (_queueLock)
                            {
                                //Log.Info("Processing inputs and cloning tasks");
                                //var _taskToProcessInputs = _tasksQueue
                                //    .Where(t => !t.AreInputsProcessed)
                                //    .OrderBy(t => t.TaskId)
                                //    .Take(30); // todo : blocks other wfs

                                //PFX.Parallel.ForEach(_taskToProcessInputs, task =>
                                //{
                                //    try
                                //    {
                                //        if (!task.IsFinished())
                                //        {
                                //            task.ProcessInputs(); // todo : moved from Execute() because of models estimations. Need to think more.

                                //            /*
                                //            lock (selectLock)
                                //            {
                                //                selectedTasks.Add(new Task(task));
                                //            }
                                //            */
                                //        }                                        
                                //    }
                                //    catch (Exception e)
                                //    {
                                //        Log.Warn("Exception on ProcessInputs: " + e.ToString());
                                //        if (task.State != TaskState.Defined)
                                //        {
                                //            task.Fail(reason: e.Message);
                                //            task.SendEvents();
                                //        }
                                //    }
                                //});

                                while (_definesQueue.Any())
                                {
                                    var task = _definesQueue[0];
                                    _definesQueue.RemoveAt(0);

                                    _tasksQueue.RemoveAll(t => t.TaskId == task.TaskId);
                                    _tasksQueue.Add(task);

                                    Log.Info(String.Format("Task {0} ({1}) oper-queued", task.TaskId, task.Package));
                                }

                                while (_readyMarks.Any())
                                {
                                    ulong idToReady = _readyMarks.First();

                                    try
                                    {
                                        _readyMarks.Remove(idToReady);
                                        var task = _tasksQueue.First(t => t.TaskId == idToReady);

                                        try
                                        {
                                            task.MarkAsReadyToExecute();
                                        }
                                        catch (Exception innerExec)
                                        {
                                            Log.Error(String.Format("Error on execute task {1}: {0}", innerExec, idToReady));

                                            task.Fail(reason: innerExec.Message);
                                            task.SendEvents();
                                        }
                                    }
                                    catch (Exception outerExec)
                                    {
                                        Log.Error(String.Format("Error on execute task {1}: {0}", outerExec, idToReady));
                                    }
                                }

                                /**/
                                foreach (var task in _tasksQueue)
                                {
                                    if (!task.IsFinished()) // && task.State != TaskState.Defined)
                                    {
                                        selectedTasks.Add(new Task(task));
                                    }
                                }
                                /**/

                                //selectedTasks = _tasksQueue.Where(t => t.AreInputsProcessed && !t.IsFinished()).Select(t => new Task(t)).ToList();
                                //selectedTasks = _tasksQueue.Where(t => !t.IsFinished()).Select(t => new Task(t)).ToList();
                                dependencies = _taskDependencies.Select(d => d).ToArray();
                            }

                            Log.Info("Tasks and dependencies cloned");

                            if (selectedTasks.Count > 0)
                                ProcessTasks(selectedTasks, resources, dependencies);
                            else
                                Log.Warn("No tasks to process");
                        }
                        catch (Exception e)
                        {
                            Log.Error("Error on updating: " + e.ToString());
                            // todo : kill non-started tasks if the same queueUpdate fail repeats
                        }
                        finally
                        {
                            _lastUpdateTime = DateTime.Now;
                            _updating = false;

                            Log.Info("Update finished");
                        }
                    }
                }
            }
        }

        private void ProcessTasks(
            IEnumerable<Task> tasks, IEnumerable<Resource> resources, 
            IEnumerable<TaskDependency> dependencies)
        {
            lock (_updateLock)
            {
                var taskSchedules = Enumerable.Empty<TaskSchedule>();

                #region updateTaskInQueue lambda definition
                var updateTaskInQueue = new Action<Task>(task =>
                {
                    try
                    {
                        var taskCopy = new Task(task);

                        lock (_queueLock)
                        {
                            Log.Info("Updating queue for task " + task.TaskId.ToString());
                            if (_tasksQueue.Any(t => t.TaskId == taskCopy.TaskId)) // if was not removed, should always be true because of locks on updating
                            {
                                _tasksQueue.RemoveAll(t => t.TaskId == taskCopy.TaskId);
                                _tasksQueue.Add(taskCopy);
                                taskCopy.SendEvents();
                            }
                            Log.Info("Updating queue finished for task " + task.TaskId.ToString());
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(String.Format(
                            "Error occurred while updating task '{0}' in queue: {1}\n{2}",
                            task.TaskId, e.Message, e.StackTrace
                        ));
                    }
                });
                #endregion

                #region processMultipleTasks lambda definition
                var processMultipleTasks = new Action<IEnumerable<Func<Task>>>(processingFunctions =>
                {
                    //foreach (var processingFunc in processingFunctions) // todo : parallel
                    PFX.Parallel.ForEach(processingFunctions, (processingFunc) =>
                    {
                        try
                        {
                            var newTaskState = processingFunc();

                            if (newTaskState != null)
                                updateTaskInQueue(newTaskState);
                        }
                        catch (Exception e)
                        {
                            Log.Error(String.Format(
                                "Error occurred while processing some task: {0}\n{1}",
                                e.Message, e.StackTrace
                            ));
                        }
                    });

                    // todo : barrier
                });
                #endregion

                // Process pairs of (task, schedule) and update task queue if needed
                #region applySchedule lambda definition
                var applySchedule = new Action<Func<Task, TaskSchedule, bool>>(processingFunc =>
                {
                    var taskActions = tasks.Join (taskSchedules.Where(ts => ts != null), 
                        task => task.TaskId, schedule => schedule.TaskId, // joins by equality of task's id
                        
                        (task, schedule) => new Func<Task>(() =>
                        {
                            if (processingFunc(task, schedule))
                                return task;

                            return null;
                        })
                    );

                    processMultipleTasks(taskActions);
                });
                #endregion

                /*
                 * no longer "DATA RACE!!!!!!!!!!!!" because of _readyMarks
                 *
                processMultipleTasks(tasks.Select(task => new Func<Task>(() =>
                {
                    lock (_queueLock)
                    {
                        try
                        {
                            if (_readyMarks.Contains(task.TaskId))
                            {
                                _readyMarks.Remove(task.TaskId);
                                task.MarkAsReadyToExecute();
                                return task;
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(String.Format("Error on execute task {1}: {0}", e, task.TaskId));
                            task.Fail(reason: e.Message);
                            return task;
                        }

                        return null;
                    }
                })));
                 */

                processMultipleTasks(tasks.Select(task => new Func<Task>(() =>
                {
                    try
                    {
                        if (task.ProcessInputs()) // todo : moved from Execute() because of models estimations. Need to think more.
                            return task;
                    }
                    catch (Exception e)
                    {
                        Log.Error("Error while processing inputs for task " + task.TaskId + ": " + e.ToString());
                        task.Fail(reason: e.Message);
                        return task;
                    }

                    return null;
                })));
                /**/

                if (tasks.Any(t => t.State == TaskState.Started))
                {                    
                    Log.Debug(String.Format("Checking state of running tasks: [{0}]",
                        String.Join(",", tasks.Where(t => t.State == TaskState.Started).Select(t => t.TaskId.ToString()))
                    ));

                    bool taskStateChanged = false;

                    processMultipleTasks(tasks.Select(task => new Func<Task>(() =>
                    {
                        Task newTaskState = null;
                        if (task.State == TaskState.Started)
                        {
                            task.Update(resources);
                            newTaskState = task;

                            if (newTaskState.State != TaskState.Started)
                                lock (_queueLock)
                                {
                                    taskStateChanged = true;
                                }
                        }
                        return newTaskState;
                    })));

                    Log.Debug("Task's state check finished");
                    
                    if (taskStateChanged)
                        return; // todo : [!] reload resources if any task was completed
                }

                if (tasks.Any(t => !t.IsFinished()))// (t => t.State == TaskState.Scheduled || t.State == TaskState.ReadyToExecute))
                {
                    Log.Info("Building new schedule");

                    var estimations = new Dictionary<ulong, Dictionary<NodeConfig, Estimation>>();
                    taskSchedules = TaskSchedule.Build(tasks.Where(t => !t.IsFinished()), resources, dependencies, out estimations);

                    if (estimations != null && estimations.Any())
                    {
                        Log.Info(String.Format("Assigning {0} estimations", estimations.Count));
                        processMultipleTasks(tasks.Select(task => new Func<Task>(() =>
                        {
                            if (estimations.ContainsKey(task.TaskId))
                            {
                                task.Estimations = estimations[task.TaskId]
                                    .GroupBy(p => p.Key.ResourceName)
                                    .ToDictionary(g => g.Key, g => g.Average(p => p.Value.CalcDuration.TotalSeconds));
                                return task;
                            }
                            else
                                return null;
                        })));
                    }

                    string schedString = String.Join("; ", taskSchedules.Select(sched => String.Format(
                        "task '{0}' action = {1}{2}",
                            sched.TaskId,
                            sched.Action.ToString(),
                            (sched.ResourceName != null && sched.Nodes != null && sched.Nodes.Any())?
                                " on '" + sched.ResourceName + "'" +
                                    ", nodes [" + String.Join(", ", sched.Nodes.Select(n => n.NodeName)) + "]" /*+
                                    String.Format(", time = [{0} - {1}]",
                                        sched.EstimatedStartTime.GetValueOrDefault(),
                                        sched.EstimatedFinishTime.GetValueOrDefault()                        
                                    ) */ // todo : log estimations in schedule
                                : ""
                        ))
                    );
                    if (String.IsNullOrWhiteSpace(schedString))
                        schedString = "(empty)";

                    Log.Debug("Schedule: " + schedString); // todo : beautify schedule log
                }
                
                if (taskSchedules != null && taskSchedules.Any())
                {
                    Log.Info("Assigning schedules");
                    applySchedule((task, schedule) =>
                    {
                        task.CurrentSchedule = schedule;
                        //task.Time.AddToOverheads(TaskTimeOverheads.Scheduling, schedulingTime);

                        /*
                        if (task.State != TaskState.Started && !task.IsFinished())
                            task.Time.AddToOverheads(TaskTimeOverheads.Provider, resourceUpdateTime);
                        */
                        return true; // needToUpdateQueue
                    });

                    if (taskSchedules.Any(s => s.Action == ScheduledAction.Abort)) // todo : simplify
                    {
                        Log.Info("Aborting tasks according to schedule");
                        applySchedule((task, schedule) =>
                        {
                            bool needToUpdateQueue = false;
                            if (schedule.Action == ScheduledAction.Abort)
                            {
                                if (task.State != TaskState.Started)
                                {
                                    Log.Warn(String.Format(
                                        "Task {0} was scheduled to abort, but it isn’t running (state = {1}). Ignoring schedule for this task.",
                                        task.TaskId, task.State
                                    ));
                                }
                                else
                                {
                                    task.Abort(resources.First(r => r.ResourceName == task.CurrentSchedule.ResourceName));
                                    needToUpdateQueue = true;
                                }
                            }

                            return needToUpdateQueue;
                        });
                    }

                    if (taskSchedules.Any(s => s.Action == ScheduledAction.Fail))
                    {
                        Log.Info("Applying failed state to tasks according to schedule");
                        applySchedule((task, schedule) =>
                        {
                            bool needToUpdateQueue = false;
                            if (schedule.Action == ScheduledAction.Fail)
                            {
                                task.Fail(schedule.FailReason);
                                needToUpdateQueue = true;
                            }
                            return needToUpdateQueue;
                        });
                    }

                    if (taskSchedules.Any(s => s.Action == ScheduledAction.Run))
                    {
                        Log.Info("Trying to run scheduled tasks");
                        applySchedule((task, schedule) =>
                        {
                            bool needToUpdateQueue = false;
                            if (schedule.Action == ScheduledAction.Run)
                            {
                                if (task.State != TaskState.ReadyToExecute)
                                {
                                    Log.Warn(String.Format(
                                        "Task {0} was scheduled to run, but it isn’t ready (state = {1}). Ignoring schedule for this task.",
                                        task.TaskId, task.State
                                    ));
                                }
                                else
                                {
                                    string json = task.ToJsonString();
                                    Log.Info(String.Format("Trying to run task '{0}' ({1}): {2}", task.TaskId, task.Package, json));

                                    task.Time.Edge(started: TaskTimeMetric.Brokering, finished: TaskTimeMetric.Queued);
                                    try
                                    {
                                        task.Run(schedule, resources);
                                        needToUpdateQueue = true;
                                    }
                                    catch
                                    {
                                        task.Time.EdgeUndo(
                                             wronglyStarted: TaskTimeMetric.Brokering,
                                            wronglyFinished: TaskTimeMetric.Queued
                                        );

                                        throw;
                                    }
                                }
                            }

                            return needToUpdateQueue;
                        });
                    }
                }

                if (tasks.Any(t => t.IsFake() && !t.IsFinished()))
                {
                    Log.Info("Completing fake tasks");
                    processMultipleTasks(tasks.Select(task => new Func<Task>(() =>
                    {
                        if (task.IsFake() && !task.IsFinished() && task.Estimations != null)
                        {
                            task.Complete(null);
                            return task;
                        }
                        else
                            return null;
                    })));
                }

            } // lock

            /*
                string json = task.ToJsonString();
                Log.Info(String.Format("Trying to execute task {0}: {1}", task.TaskId, json));

                task.Time.Edge(started: TaskTimeMetric.Brokering, finished: TaskTimeMetric.Queued);

                Estimation estim = null;
                string failReason = "";
                bool canRunInTheFuture = RightsProxy.HasMoney(task.UserId, ref failReason);

                if (canRunInTheFuture)
                {
                    task.Time.AddToOverheads(TaskTimeOverheads.Scheduler, () =>
                    {
                        string[] allowedResourceNames = RightsProxy.GetAllowedResourceNames(task, resources);
                        Log.Info(String.Format("Allowed resources for task {0}: [{1}]", task.TaskId, String.Join(", ", allowedResourceNames)));

                        var allowedResources = resources.Where(r => allowedResourceNames.Contains(r.ResourceName));
                        var useableResources = allowedResources.Where(r => r.Nodes.Any(n => n.HasPackage(task.Package)));
                        Log.Info(String.Format("Useable resources for task {0}: [{1}]", task.TaskId, String.Join(", ", useableResources.Select(r => r.ResourceName))));

                        if (useableResources.Any())
                            estim = AssignTask(task, useableResources);
                        else
                        {
                            canRunInTheFuture = false;
                            failReason = "There is no appropriate resource for specified task: package is not installed on resources available to user.";
                        }
                    });
                }

                if (estim != null)
                    task.Run(estim, resources);
                else
                {
                    task.Time.EdgeUndo(
                            wronglyStarted: TaskTimeMetric.Brokering, 
                        wronglyFinished: TaskTimeMetric.Queued
                    );

                    if (!canRunInTheFuture)
                    {
                        task.Time.Finished(TaskTimeMetric.Queued);
                        task.Fail(failReason);
                    }
                }

                break;

            default:
                throw new Exception("Unexpected state to process");
            */
        }

        //private Estimation AssignTask(Task task, IEnumerable<Resource> resources)
        //{
        //    Estimation minEst = null;

        //    try
        //    {
        //        /*
        //        var resources = Broker.Providers.SelectMany(
        //            prov => prov.GetResourceNames().Select(
        //                name => new { Provider = prov, Name = name, Nodes = prov.GetNodes(name) }
        //            )
        //        );
        //        */

        //        foreach (var resource in resources)
        //        {
        //            var relevantNodes = resource.Nodes.Where(node => node.HasPackage(task.Package));

        //            if (relevantNodes.Any())
        //            {
        //                foreach (LaunchModel model in Broker.LaunchModels)
        //                {
        //                    Estimation curEst = model.EstimateIfMatches(task, resource);

        //                    if (curEst != null && (minEst == null || curEst.ExecutionTime < minEst.ExecutionTime))
        //                        minEst = curEst;

        //                    Log.Info(String.Format(
        //                        "Задача {1}, на кластере {2} получена оценка в {3} очков от модели запуска {0}",
        //                            model.ToString(), task.TaskId, resource.ResourceName,
        //                            (curEst == null)? "--" : "" + ((int) curEst.ExecutionTime.TotalSeconds).ToString()
        //                    ));
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        string failReason = String.Format(
        //            "Error on starting task {2}: {0}\n{1}",
        //            e.Message, e.StackTrace, task.TaskId
        //        );

        //        Log.Error(failReason);
        //        minEst = null;
        //    }

        //    return minEst;
        //}

        #endregion
    }
}
