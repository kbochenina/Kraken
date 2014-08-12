using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;
using System.Xml.Serialization;
using Easis.PackageBase.Definition;
using ServiceProxies.SchedulerService;
using Config = System.Configuration.ConfigurationManager;
using Resource = ServiceProxies.ResourceBaseService.Resource;

namespace MITP
{
    [DataContract]
    public enum ScheduledAction
    {
        [EnumMember]
        None,
        [EnumMember]
        Abort,
        [EnumMember]
        Run,
        [EnumMember]
        Fail,
    }

    [DataContract]
    public class TaskSchedule   // todo : TaskSchedule & FullTaskSchedule
    {
        private const string SCHEDULE_ALL_TASKS_PARAM_NAME = "Scheduler.SendAllTasks";

        public ulong TaskId { get; private set; }

        [DataMember]
        public ScheduledAction Action { get; private set; }

        [DataMember]
        public IEnumerable<NodeConfig> Nodes { get; private set; }

        [DataMember]
        public string ResourceName
        {
            get
            {
                if (Nodes == null || !Nodes.Any())
                    return null;

                return Nodes.First().ResourceName;
            }

            private set { } // for WCF
        }

        [DataMember]
        public Estimation Estimation { get; private set; }

        public string FailReason { get; private set; }

        //public DateTime? EstimatedStartTime { get; private set; }
        //public DateTime? EstimatedFinishTime { get; private set; }

        public IDictionary<string, string> ModifiedParams { get; private set; }

        private TaskSchedule()
        {
            Nodes = new NodeConfig[0];
            FailReason = null;
            ModifiedParams = new Dictionary<string, string>();
        }

        public TaskSchedule(TaskSchedule other)
            : this()
        {
            if (other != null)
            {
                TaskId = other.TaskId;
                Action = other.Action;

                var newNodes = new List<NodeConfig>();
                foreach (var node in other.Nodes)
                {
                    var newNode = node;
                    newNodes.Add(newNode);
                }
                Nodes = newNodes;

                FailReason = other.FailReason;

                if (other.Estimation != null)
                    Estimation = new Estimation(other.Estimation);
                //EstimatedStartTime  = other.EstimatedStartTime;
                //EstimatedFinishTime = other.EstimatedFinishTime;

                if (other.ModifiedParams != null)
                    ModifiedParams = new Dictionary<string, string>(other.ModifiedParams);
            }
        }

        public static IEnumerable<TaskSchedule> Build(
            IEnumerable<Task> tasks, IEnumerable<Resource> resources, IEnumerable<TaskDependency> dependencies,
            out Dictionary<ulong, Dictionary<NodeConfig, Estimation>> estimations)
        {
            estimations = new Dictionary<ulong, Dictionary<NodeConfig, Estimation>>();

            if (tasks == null || !tasks.Any())
            {
                Log.Warn("No tasks to schedule");
                return Enumerable.Empty<TaskSchedule>();
            }

            if (resources == null || !resources.Any())
            {
                Log.Warn("No resources to allocate");
                return Enumerable.Empty<TaskSchedule>();
            }

            var schedule = new List<TaskSchedule>();

            Log.Info(String.Format("Checking permissions for tasks [{0}]", String.Join(", ", tasks.Select(t => t.TaskId.ToString()))));
            var failReasonForTask = new Dictionary<ulong, string>();
            var permissionsForTask = GetPermissionsForTasks(tasks, resources, out failReasonForTask);

            foreach (var task in tasks)
            {
                // todo : move to SimpleAllocator
                if (/*task.State == TaskState.ReadyToExecute &&*/ !permissionsForTask[task.TaskId].Any())
                {
                    if (!failReasonForTask.ContainsKey(task.TaskId))
                    {
                        Log.Warn("Unknown permission error");
                        failReasonForTask[task.TaskId] = "Can't run task bacause of unknown permission error";
                    }

                    schedule.Add(new TaskSchedule()
                    {
                        TaskId = task.TaskId,
                        Action = ScheduledAction.Fail,
                        FailReason = failReasonForTask[task.TaskId],
                        Nodes = new NodeConfig[0],
                    });
                }
            }

            Log.Debug(
                "Permissions for tasks are: " +
                String.Join("; ", tasks.Select(t =>
                    String.Format("{0} -> {1}", t.TaskId, String.Join(", ", permissionsForTask[t.TaskId]))
                ))
            );

            // Get Estimations
            var estimationsForTask = new Dictionary<ulong, Dictionary<NodeConfig, Estimation>>();
            foreach (var task in tasks)
            {
                task.Time.AddToOverheads(TaskTimeOverheads.Estimation, () =>
                {
                    var permittedNodes = resources.SelectMany(r => r.Nodes).Where(n =>
                            permissionsForTask[task.TaskId].Contains(n.ResourceName + "." + n.NodeName)
                        );

                    // todo : use CoresAvailable if can run now, and CoresTotal if in the future
                    estimationsForTask[task.TaskId] = PackageBaseProxy.GetEstimationsByModel(task.PackageEngineState, resources, permittedNodes);

                    foreach (var node in permittedNodes)
                    {
                        if (!estimationsForTask[task.TaskId].Keys.Any(config => config.NodeName == node.NodeName))
                        {
                            // estimate from history

                            var config = new NodeConfig
                            {
                                NodeName = node.NodeName,
                                ResourceName = node.ResourceName,
                                Cores = 1,
                            };

                            // todo: [!] estimate from history
                            estimationsForTask[task.TaskId][config] = new Estimation(
                                null,
                                null //new HistoryEstimation(1337 + node.NodeName.Length)
                            );

                            estimationsForTask[task.TaskId][config].CalcDuration = TimeSpan.FromSeconds(1.337 + node.NodeName.Length);
                        }
                    }
                });
            }

            try
            {
                estimations = estimationsForTask;

                Log.Debug(
                    "Estimations for tasks are: " +
                    String.Join("; ", tasks.Select(t =>
                        String.Format("{0} -> {1}",
                            t.TaskId,
                            String.Join(", ", estimationsForTask[t.TaskId]
                                .Select(pair => String.Format("{0} on {1}.{2}", pair.Value.CalcDuration, pair.Key.ResourceName, pair.Key.NodeName))
                            )
                        )
                    ))
                );
            }
            catch (Exception estimEx)
            {
                Log.Warn(estimEx.ToString());
            }

            bool scheduledSuccessfully = false;
            try
            {
                Log.Info("Scheduling");
                var schedulingStrated = DateTime.Now;

                var scheduleByScheduler = Reschedule(tasks, resources, dependencies, /*permissionsForTask,*/ estimationsForTask);

                var schedulingFinished = DateTime.Now;
                var schedulingTime = schedulingFinished - schedulingStrated;
                //                var schedulingTimeOnOneTask = TimeSpan.FromMilliseconds(schedulingTime.TotalMilliseconds / scheduleByScheduler.Count());
                Log.Debug(String.Format("Scheduling took {0} seconds", schedulingTime.TotalSeconds));

                foreach (var schedResult in scheduleByScheduler)
                {
                    tasks.Single(t => t.TaskId == schedResult.TaskId)
                        //.Time.AddToOverheads(TaskTimeOverheads.Scheduler, schedulingTimeOnOneTask);
                        .Time.AddToOverheads(TaskTimeOverheads.Scheduler, schedulingTime);
                }

                if (scheduleByScheduler != null && scheduleByScheduler.Any())
                {
                    var nonPermittedSchedule = scheduleByScheduler.FirstOrDefault(s =>
                        s.Action == ScheduledAction.Run &&
                        s.Nodes.Select(n => n.ResourceName + "." + n.NodeName).Except(permissionsForTask[s.TaskId]).Any());

                    if (nonPermittedSchedule != null)
                    {
                        // scheduled to run on non-permitted resource
                        Log.Error(String.Format(
                            "Scheduler scheduled task {0} on non-permitted resource config: '{1}', nodes '{2}'",
                            nonPermittedSchedule.TaskId, nonPermittedSchedule.ResourceName,
                            String.Join("', '", nonPermittedSchedule.Nodes.Select(n => n.NodeName))
                        ));
                        scheduledSuccessfully = false;
                    }
                    else
                    {
                        var busyNodes = resources.SelectMany(r => r.Nodes.Where(n =>
                            (n.CoresAvailable <= 0 || n.SubmissionsAvailable <= 0) && // n.CoresCount > 0 && 
                            !scheduleByScheduler.Any(s => s.Action == ScheduledAction.Abort && s.Nodes.Any(sn => sn.NodeName == n.NodeName && sn.ResourceName == n.ResourceName))
                        )).Select(n => n.ResourceName + "." + n.NodeName);

                        var nodeLimits = resources.SelectMany(r => r.Nodes.Select(n =>
                            new
                            {
                                ResourceName = n.ResourceName,
                                NodeName = n.NodeName,
                                CoresLimit = n.CoresAvailable,
                                TasksLimit = n.TasksSubmissionLimit
                            }
                        )).ToDictionary(lim => lim.ResourceName + "." + lim.NodeName, lim => lim);

                        var nodeAborts = scheduleByScheduler
                            .Where(s => s.Action == ScheduledAction.Abort)
                            .SelectMany(s => s.Nodes)
                            .GroupBy(s => s.ResourceName + "." + s.NodeName)
                            .ToDictionary(group => group.Key, group => new { Tasks = group.Count(), Cores = group.Sum(s => s.Cores) });

                        var overtargetedNodes = scheduleByScheduler
                            .Where(s => s.Action == ScheduledAction.Run)
                            .SelectMany(s => s.Nodes)
                            .GroupBy(n => n.ResourceName + "." + n.NodeName)
                            .Where(
                                group => group.Count() > nodeLimits[group.Key].TasksLimit + (nodeAborts.ContainsKey(group.Key) ? nodeAborts[group.Key].Tasks : 0) ||
                                group.Sum(n => n.Cores) > nodeLimits[group.Key].CoresLimit + (nodeAborts.ContainsKey(group.Key) ? nodeAborts[group.Key].Cores : 0))
                            .Select(group => group.Key);

                        // todo: disallow targeting same node twice in schedule for one task
                        var insaneDecisions = scheduleByScheduler.Join(tasks, s => s.TaskId, t => t.TaskId, (s, t) =>
                            ((s.Action == ScheduledAction.Run && t.State != TaskState.ReadyToExecute) ||
                             (s.Action == ScheduledAction.Run && String.IsNullOrEmpty(s.ResourceName)) ||
                             (s.Action == ScheduledAction.Run && !s.Nodes.Any()) ||
                                //(s.Action == ScheduledAction.Run && s.Nodes.Any(n => n.Cores <= 0)) ||
                             (s.Action == ScheduledAction.Run && busyNodes.Intersect(s.Nodes.Select(conf => conf.ResourceName + "." + conf.NodeName)).Any()) ||
                             (s.Action == ScheduledAction.Run && overtargetedNodes.Intersect(s.Nodes.Select(conf => conf.ResourceName + "." + conf.NodeName)).Any()) ||
                             (s.Action == ScheduledAction.Abort && t.State != TaskState.Started))
                            ? new
                            {
                                action = s.Action,
                                state = t.State,
                                taskId = t.TaskId,
                                resourceName = s.ResourceName,
                                nodeNames = s.Nodes.Select(n => n.NodeName)
                            } : null
                        ).Where(r => r != null);

//                        if (insaneDecisions.Any())
//                        {
//                            if (busyNodes.Any())
//                                Log.Debug("Busy nodes: " + String.Join(", ", busyNodes));
//
//                            if (overtargetedNodes.Any())
//                                Log.Debug("Overtargeted nodes: " + String.Join(", ", overtargetedNodes));
//
//                            Log.Error(
//                                "Scheduler made some insane decisions: " +
//                                String.Join(", ",
//                                    insaneDecisions.Select(d => String.Format(
//                                        "{0} {1} task '{2}' on {3}({4})",
//                                        d.action, d.state, d.taskId,
//                                        d.resourceName, String.Join(", ", d.nodeNames)
//                                    ))
//                                ) +
//                                ". Ignoring them."
//                            );
//
//                            //Log.Warn("NOT ignoring insane schedule for debug purposes"); // todo : [!] remove
//                            scheduleByScheduler = scheduleByScheduler.Where(s => !insaneDecisions.Any(d => d.taskId == s.TaskId)).ToArray();
//                        }

                        if (scheduleByScheduler.Any())
                        {
                            schedule.AddRange(scheduleByScheduler);
                            scheduledSuccessfully = true;
                        }
                        else
                        {
                            scheduledSuccessfully = false;
                        }
                    }
                }
                else if (tasks.All(t => t.State == TaskState.Started))
                {
                    Log.Info("All tasks sento to the scheduler are running. Ignore null or empty schedule.");
                    scheduledSuccessfully = true;
                }
                else
                    Log.Warn("Scheduler returned bad schedule (either null or empty).");
            }
            catch (Exception e)
            {
                Log.Error(String.Format(
                    "Exception in scheduler: {0}\n{1}",
                    e.Message, e.StackTrace
                ));
            }

            //todo: remove it later
//            scheduledSuccessfully = false;

            if (!scheduledSuccessfully)
            {
                try
                {
                    Log.Info("Using simple task allocator");
                    var scheduleByAllocator = AllocateTasks(tasks, resources, /* permissionsForTask, */ estimationsForTask);
                    schedule.AddRange(scheduleByAllocator);
                }
                catch (Exception e)
                {
                    Log.Error(String.Format(
                        "Exception in allocator: {0}\n{1}",
                        e.Message, e.StackTrace
                    ));
                }
            }

            return schedule;
        }

        private static Dictionary<ulong, List<string>> GetPermissionsForTasks(IEnumerable<Task> tasks, IEnumerable<Resource> resources, out Dictionary<ulong, string> failReasonForTask)
        {
            var permissions = new Dictionary<ulong, List<string>>();
            failReasonForTask = new Dictionary<ulong, string>();

            var taskUsers = tasks.Where(t => !t.IsFinished()).Select(t => t.UserId).Distinct();
            var userHasMoney = new Dictionary<string, bool>();
            var allowedResourceNamesForUser = new Dictionary<string, string[]>();
            foreach (string userId in taskUsers)
            {
                userHasMoney[userId] = RightsProxy.HasMoney(userId);
                allowedResourceNamesForUser[userId] = RightsProxy.GetAllowedResourceNames(userId, resources);
            }

            foreach (var task in tasks)
            {
                permissions[task.TaskId] = new List<string>();

                if (task.IsFinished())
                {
                    Log.Info(String.Format(
                        "Permissions check skipped for task '{0}' because it's state = {1}.",
                        task.TaskId, task.State
                    ));
                }
                else
                {
                    bool canRunInTheFuture = true;

                    // money
                    if (canRunInTheFuture && !userHasMoney[task.UserId])
                    {
                        failReasonForTask[task.TaskId] = "Not enough money to launch task " + task.TaskId.ToString();
                        canRunInTheFuture = false;
                    }

                    // rights
                    var allowedResources = Enumerable.Empty<Resource>();
                    if (canRunInTheFuture)
                    {
                        if (!RightsProxy.CanExecutePackage(task.UserId, task.Package))
                        {
                            failReasonForTask[task.TaskId] = "Not enough permissions to run package " + task.Package;
                            canRunInTheFuture = false;
                        }
                        else
                        {
                            allowedResources = resources.Where(r =>
                                allowedResourceNamesForUser[task.UserId].Contains(r.ResourceName));

                            if (!allowedResources.Any())
                            {
                                failReasonForTask[task.TaskId] = "Not enough resource permissions to run task " + task.TaskId.ToString();
                                canRunInTheFuture = false;
                            }
                        }
                    }

                    // package installed
                    if (canRunInTheFuture)
                    {
                        var resourcesWithPack = allowedResources.SelectMany(
                            r => r.Nodes
                                .Where(n => n.HasPackage(task.Package))
                                .Select(n => n.ResourceName + "." + n.NodeName)
                        );

                        if (resourcesWithPack.Any())
                            permissions[task.TaskId].AddRange(resourcesWithPack);
                        else
                        {
                            failReasonForTask[task.TaskId] = String.Format(
                                "Could not run task {0}: package {1} is not installed on resources available to user.",
                                task.TaskId, task.Package
                            );
                            canRunInTheFuture = false;
                        }
                    }

                    // ExecParams
                    if (task.ExecParams.ContainsKey("Resource"))
                    {
                        permissions[task.TaskId] = permissions[task.TaskId]
                            .Where(perm => perm.StartsWith(task.ExecParams["Resource"], StringComparison.InvariantCultureIgnoreCase))
                            .ToList();

                        if (!permissions[task.TaskId].Any())
                        {
                            failReasonForTask[task.TaskId] = "Chosen resource is not permitted for user";
                            canRunInTheFuture = false;
                        }
                    }

                    // todo : can run (in the future) on resources wich are occupied by our tasks

                    if (!canRunInTheFuture)
                    {
                        permissions[task.TaskId].Clear();
                    }
                }
            }

            return permissions;
        }

        private static IEnumerable<TaskSchedule> AllocateTasks(
            IEnumerable<Task> tasks, IEnumerable<Resource> resources,
            //            IDictionary<ulong, IEnumerable<string>> permissionsForTask
            IDictionary<ulong, Dictionary<NodeConfig, Estimation>> estimationsForTask)
        {
            var now = DateTime.Now;

            double maxCoresCount = resources.Max(r => r.Nodes.Max(n => n.CoresCount));
            var availableNodes = resources
                .SelectMany(r => r.Nodes.Select(n => new
                {
                    ResourceName = n.ResourceName,
                    NodeName = n.NodeName,

                    CoresLeft = n.CoresAvailable,
                    TasksLeft = n.SubmissionsAvailable,

                    Packs = n.Packages.Select(p => p.Name.ToLowerInvariant()).ToArray()
                }))
                .Where(n => n.CoresLeft > 0 && n.TasksLeft > 0)
                //.Where(n => n.CoresAvailable > 0)
                //.OrderBy(n => n.Packages.Count + (n.CoresAvailable / maxCoresCount))
                .OrderBy(n => n.Packs.Length + (n.CoresLeft / maxCoresCount))
                .ToList();

            // todo: log availableNodes

            var scheduledTasks = new List<TaskSchedule>();
            foreach (var task in tasks)
            {
                if (task.State == TaskState.ReadyToExecute || task.State == TaskState.Scheduled)
                {
                    var taskSchedule = new TaskSchedule
                    {
                        TaskId = task.TaskId,
                        Action = ScheduledAction.None,
                        Nodes = new NodeConfig[0],

                        Estimation = new Estimation()
                        //EstimatedStartTime = now,
                        //EstimatedFinishTime = now,
                    };

                    var nodeForTask = availableNodes.FirstOrDefault(n =>
                        n.CoresLeft > 0 && n.TasksLeft > 0 &&
                        n.Packs.Contains(task.Package.ToLowerInvariant()) &&
                            //n.HasPackage(task.Package) &&
                        estimationsForTask[task.TaskId].Any(pair =>
                            pair.Key.ResourceName == n.ResourceName && pair.Key.NodeName == n.NodeName &&
                            pair.Key.Cores <= n.CoresLeft)
                        //                        permissionsForTask[task.TaskId].Contains(n.ResourceName + "." + n.NodeName, StringComparer.InvariantCultureIgnoreCase)
                    );

                    if (nodeForTask != null)
                    {
                        taskSchedule.Action = ScheduledAction.Run;
                        var estimation = estimationsForTask[task.TaskId]
                                .First(pair =>
                                    pair.Key.ResourceName == nodeForTask.ResourceName && pair.Key.NodeName == nodeForTask.NodeName &&
                                    pair.Key.Cores <= nodeForTask.CoresLeft)
                                ;//.Value;

                        /*
                        taskSchedule.EstimatedStartTime = now;
                        /*
                        taskSchedule.EstimatedFinishTime = 
                            now +
                            estimationsForTask[task.TaskId]
                                .Single(pair => pair.Key.ResourceName == nodeForTask.ResourceName && pair.Key.NodeName == nodeForTask.NodeName)
                                .Value;
                        */
                        taskSchedule.Estimation = estimation.Value;
                        taskSchedule.Estimation.CalcStart = now;
                        /**/

                        taskSchedule.Nodes = new[] 
                        { 
                            new NodeConfig 
                            {
                                Cores = estimation.Key.Cores, // was "1"
                                NodeName = nodeForTask.NodeName,
                                ResourceName = nodeForTask.ResourceName,
                            }
                        };

                        scheduledTasks.Add(taskSchedule);

                        availableNodes.RemoveAll(n =>
                            n.ResourceName == nodeForTask.ResourceName &&
                            n.NodeName == nodeForTask.NodeName
                        );

                        if (nodeForTask.CoresLeft > taskSchedule.Nodes.Single().Cores &&
                            nodeForTask.TasksLeft > 1)
                        {
                            availableNodes.Add(new
                            {
                                ResourceName = nodeForTask.ResourceName,
                                NodeName = nodeForTask.NodeName,

                                CoresLeft = (int)(nodeForTask.CoresLeft - taskSchedule.Nodes.Single().Cores),
                                TasksLeft = (int)(nodeForTask.TasksLeft - estimation.Key.Cores), // was "- 1"

                                Packs = nodeForTask.Packs
                            });
                        }

                        // todo: log schedule for task
                    }
                }
            }

            return scheduledTasks;


            /*
            var limits = new[]
			{
				new { pack = "",             otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "GAMESS",       otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "ORCA",         otherSoft = "windows", maxCores = 1,  maxNodes = 1},
				new { pack = "ORCA",         otherSoft = "",        maxCores = 8,  maxNodes = 1},
				new { pack = "SEMP",         otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "Plasmon",      otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "QDLaser",      otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "JAggregate",   otherSoft = "",        maxCores = 1,  maxNodes = 1},
                //new { pack = "Plasmon",      otherSoft = "",        maxCores = 1,  maxNodes = 1},
                //new { pack = "QDLaser",      otherSoft = "",        maxCores = 8,  maxNodes = 1},
                //new { pack = "JAggregate",   otherSoft = "",        maxCores = 8,  maxNodes = 1},
				//new { pack = "NanoFlow",     otherSoft = "",        maxCores = 16*3, maxNodes = 3},
				new { pack = "NanoFlow",     otherSoft = "",        maxCores = 8,  maxNodes = 1},
				new { pack = "MD_KMC",       otherSoft = "",        maxCores = 8,  maxNodes = 1},
				new { pack = "MD_KMC",       otherSoft = "windows", maxCores = 0,  maxNodes = 0},
				new { pack = "NTDMFT",       otherSoft = "",        maxCores = 8,  maxNodes = 1},
				new { pack = "Dalton",       otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "NAEN",         otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "Upconversion", otherSoft = "",        maxCores = 8,  maxNodes = 1},
				new { pack = "TestP",        otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "Belman",       otherSoft = "",        maxCores = 8,  maxNodes = 1},
				new { pack = "SWAN",         otherSoft = "",        maxCores = 1,  maxNodes = 1},
			};

            var scheduledTasks = new List<TaskSchedule>();
            foreach (var task in tasks)
            {
                var packLimits = limits.Where(l => String.IsNullOrWhiteSpace(l.pack));
                if (limits.Any(limit => limit.pack.ToLower() == task.Package.ToLower()))
                    packLimits = limits.Where(limit => limit.pack.ToLower() == task.Package.ToLower());

                foreach (var resource in resources)
                {
                    foreach (var node in resource.Nodes.Where(n => n.HasPackage(task.Package)))
                    {
                        var properPackLimit = packLimits.Max(
                            lim => lim.otherSoft
                                .Split(new char[] { ' ', '\t', '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                .Intersect(node.OtherSoftware)
                                .Count()
                        );

                        // Choose number of cores for each node:

                        var coresOnNode = new List<int>();
                        int coresToDo = properPackLimit.maxCores;
                        int nodesToDo = properPackLimit.maxNodes;

                        foreach (var node in resource.Nodes)
                        {
                            if (coresToDo > 0 && nodesToDo > 0)
                            {
                                int coresOnCurrentNode = Math.Min(node.CoresAvailable, coresToDo);

                                coresOnNode.Add(coresOnCurrentNode);
                                coresToDo -= coresOnCurrentNode;
                                nodesToDo -= (coresOnCurrentNode == 0) ? 0 : 1;
                            }
                            else
                                coresOnNode.Add(0);
                        }

                        int coresFound = coresOnNode.Sum();
                        if (coresFound == 0) // haven't found anything
                            return null;

                        // Estimate (clusters with more nodes are preferable, so subtract it's nodes.Count from time estimation):

                        TimeSpan time = new TimeSpan((long) ((Math.Round(18000.0 / coresFound) - resource.Nodes.Count + 60)*SEC_IN_TICKS));
                        Estimation estimation = new Estimation(task.TaskId, resource.ProviderName, resource.ResourceName, coresOnNode.ToArray()) { ExecutionTime = time };
                    }
                }
            }

            return scheduledTasks;
            */
        }

        private static void SetWorkflowDescription(
            ServiceProxies.SchedulerService.EstimatedWorkflow wf,
            IEnumerable<Task> tasks, IEnumerable<Resource> resources,
            IEnumerable<TaskDependency> dependencies,
            IDictionary<ulong, Dictionary<NodeConfig, Estimation>> estimationsForTask,
            string schedulingWfId = null)
        {
            var now = DateTime.Now;

            var permittedResourceNames = new Func<IEnumerable<string>, IEnumerable<string>>((perms) =>
                perms.Select(p => p.Substring(0, p.IndexOf('.'))).Distinct()
            );

            var permittedNodeNames = new Func<string, IEnumerable<string>, IEnumerable<string>>((resName, perms) =>
                perms.Where(p => p.StartsWith(resName)).Select(st => st.Substring(resName.Length + 1))
            );

            var taskStateConvert = new Func<Task, ServiceProxies.SchedulerService.TaskSchedulerTaskState>(task =>
            {
                switch (task.State)
                {
                    case TaskState.Started:
                        return ServiceProxies.SchedulerService.TaskSchedulerTaskState.LAUNCHED;

                    case TaskState.Scheduled:
                        return ServiceProxies.SchedulerService.TaskSchedulerTaskState.SCHEDULED;

                    case TaskState.ReadyToExecute:
                        return ServiceProxies.SchedulerService.TaskSchedulerTaskState.SCHEDULED;

                    default:
                        throw new Exception(String.Format(
                            "Could not map task's {0} state {1} to scheduler's task state",
                            task.TaskId, task.State.ToString()
                        ));
                }
            });

            wf.Dependencies = dependencies.Select(d => new ServiceProxies.SchedulerService.TasksDepenendency()
            {
                ProviderId = d.ParentTaskId,
                ConsumerId = d.ChildTaskId,
            }).ToList();

            //Optimize = true,
            wf.Name = String.Join(", ", tasks.Select(t => t.TaskId.ToString()));

            wf.ActiveTasks = tasks
                .Where(t =>
                    (t.State != TaskState.ReadyToExecute && t.State != TaskState.Scheduled && t.State != TaskState.Defined) ||

                    // or another known urgent in parallel
                    (t.WfId != schedulingWfId && t.Priority == TaskPriority.Urgent && t.State == TaskState.ReadyToExecute &&
                    t.CurrentSchedule != null && !String.IsNullOrEmpty(t.CurrentSchedule.ResourceName)) // todo : -> Task.isScheduled()
                )
                .Select(t => new ServiceProxies.SchedulerService.ActiveEstimatedTask()
                {
                    ApplicationName = t.Package,

                    WFid = t.WfId,

                    Id = t.TaskId,
                    IsUrgent = (t.Priority == TaskPriority.Urgent),
                    Parameters = new Dictionary<string, string>(t.Params),
                    State = taskStateConvert(t),

                    Estimation = new ServiceProxies.SchedulerService.ActiveEstimation()
                    {
                        //Destination = new Common.LaunchDestination()
                        Destination = new ServiceProxies.SchedulerService.LaunchDestination()
                        {
                            ResourceName = t.CurrentSchedule.ResourceName,
                            NodeNames = t.CurrentSchedule.Nodes.Select(n => n.NodeName).ToList()
                        },

                        //Resource = new Common.Resource()
                        Resource = new ServiceProxies.SchedulerService.Resource()
                        {
                            Name = t.CurrentSchedule.ResourceName,

                            Nodes = resources
                                .Single(r => r.ResourceName == t.CurrentSchedule.ResourceName)
                                .Nodes.Where(n => t.CurrentSchedule.Nodes.Any(sn => sn.NodeName == n.NodeName))
                                //.Select(n => new Common.Node()
                                .Select(n => new ServiceProxies.SchedulerService.Node()
                                {
                                    ResourceName = n.ResourceName,
                                    DNSName = n.NodeName,
                                    Parameters = new Dictionary<string, string>(n.StaticHardwareParams),
                                    CoresAvailable = n.CoresAvailable,
                                    CoresTotal = (int)n.CoresCount,
                                }).ToList(),

                            Parameters = new Dictionary<string, string>(
                                resources.Single(r => r.ResourceName == t.CurrentSchedule.ResourceName).HardwareParams
                            ),
                        },

                        LaunchTime = t.Time.Duration[TaskTimeMetric.Calculation].TotalSeconds,
                        //Math.Max(0.0, ((t.CurrentSchedule.EstimatedStartTime ?? now) - now).TotalSeconds),

                        Result = new ServiceProxies.SchedulerService.EstimationResult()
                        //Result = new TimeMeter.EstimationResult(null)
                        {
                            //Time = t.CurrentSchedule.Estimation.TimeLeft(t.Time.WhenStarted[TaskTimeMetric.Calculation]).TotalSeconds, //t.Time.Left.Value.TotalSeconds, 
                            CalculationTime = t.CurrentSchedule.Estimation.TimeLeft(t.Time.WhenStarted[TaskTimeMetric.Calculation]).TotalSeconds, //t.Time.Left.Value.TotalSeconds, 
                            Overheads = 0, // todo : overheads estimation for scheduler

                            //Math.Max(0.0, ((t.CurrentSchedule.EstimatedFinishTime ?? now) - (t.CurrentSchedule.EstimatedStartTime ?? now)).TotalSeconds),
                            //Parameters = new[] { new ServiceProxies.SchedulerService.EstimationResult.ParameterValue()                               
                            Parameters = t.Params.Select(pair => new ServiceProxies.SchedulerService.EstimationResult.ParameterValue()
                            //Parameters = t.Params.Select(pair => new TimeMeter.EstimationResult.ParameterValue()
                            {
                                Name = pair.Key,
                                InitialValue = pair.Value,
                                NewValue = pair.Value
                            }).ToList(),
                            //}).ToList(),
                        },
                    },
                }).ToList();

            bool sendAllTasks = (Config.AppSettings[SCHEDULE_ALL_TASKS_PARAM_NAME] ?? "false").ToLowerInvariant().Trim() == "true";

            wf.Tasks = tasks
                .Where(t =>
                    (schedulingWfId == null && (t.State == TaskState.ReadyToExecute || t.State == TaskState.Scheduled || (t.State == TaskState.Defined && sendAllTasks))) ||

                    // to plan ahead all tasks in urgent WF, including future ones
                    (t.WfId == schedulingWfId && (t.State == TaskState.ReadyToExecute || t.State == TaskState.Scheduled || t.State == TaskState.Defined))
                )
                .Select(t => new ServiceProxies.SchedulerService.EstimatedTask()
                {
                    WFid = t.WfId,

                    Id = t.TaskId,
                    ApplicationName = (t.Package + (String.IsNullOrWhiteSpace(t.Method) ? "" : "_" + t.Method)).ToUpperInvariant(),

                    Parameters = new Dictionary<string, string>(t.Params),

                    Estimations = estimationsForTask[t.TaskId].Select(pair =>
                        new ServiceProxies.SchedulerService.ResourceEstimation()
                        {
                            Resource = new ServiceProxies.SchedulerService.Resource()
                            //Resource = new Common.Resource()
                            {
                                Name = pair.Key.ResourceName,

                                Nodes = resources.Single(r => r.ResourceName == pair.Key.ResourceName)
                                    .Nodes.Where(n => n.NodeName == pair.Key.NodeName) // note: single node now
                                    .Select(n => new ServiceProxies.SchedulerService.Node()
                                    //.Select(n => new Common.Node()
                                    {
                                        ResourceName = n.ResourceName,
                                        DNSName = n.NodeName,
                                        Parameters = new Dictionary<string, string>(n.StaticHardwareParams),
                                        CoresAvailable = n.CoresAvailable,
                                        CoresTotal = (int)n.CoresCount,
                                    }
                                ).ToList(),

                                Parameters = new Dictionary<string, string>(
                                    resources.Single(r => r.ResourceName == pair.Key.ResourceName).HardwareParams
                                ),
                            },

                            Result = new ServiceProxies.SchedulerService.EstimationResult()
                            //Result = new TimeMeter.EstimationResult(null)
                            {
                                //Time = pair.Value.CalcDuration.TotalSeconds,
                                CalculationTime = pair.Value.CalcDuration.TotalSeconds,
                                Overheads = 0, // todo : overheads estimation for scheduler

                                Parameters = new ServiceProxies.SchedulerService.EstimationResult.ParameterValue[]
                                //Parameters = new List<TimeMeter.EstimationResult.ParameterValue>()
                                {
                                    new ServiceProxies.SchedulerService.EstimationResult.ParameterValue()
                                    //new TimeMeter.EstimationResult.ParameterValue()
                                    {
                                        Name = "P", NewValue = pair.Key.Cores.ToString()
                                    },

                                    new ServiceProxies.SchedulerService.EstimationResult.ParameterValue()
                                    //new TimeMeter.EstimationResult.ParameterValue()
                                    {
                                        Name = "NODES", NewValue = "1" // todo: pair.Key.NodeNames.Count
                                    }
                                }.ToList()
                            }
                        }
                    ).ToList(),

                    /*
                    SuitableDestinations = 
                        permittedResourceNames(permissionsForTask[t.TaskId])
                            .Select(resName => new ServiceProxies.SchedulerService.LaunchDestination()
                            {
                                ResourceName = resName,
                                NodeNames    = permittedNodeNames(resName, permissionsForTask[t.TaskId]).ToArray(),                            
                            })
                            .ToArray(),
                    */
                }).ToList();

            /*
            Resources = resources
                .Select(r => new ServiceProxies.SchedulerService.Resource()
                {
                    Name = r.ResourceName,
                    Nodes = r.Nodes.Select(n => new ServiceProxies.SchedulerService.Node()
                    {
                        DNSName = n.NodeName,
                        ResourceName = n.ResourceName,
                        CoresAvailable = n.CoresAvailable,
                        CoresTotal = n.CoresCount,
                        Parameters = new Dictionary<string, string>(n.StaticHardwareParams), //n.DynamicHardwareParams                            
                    }).ToArray(),

                    Parameters = new Dictionary<string, string>(r.HardwareParams),
                })
                .ToArray(),                        
            */

            if (wf.Tasks.Count + wf.ActiveTasks.Count != tasks.Count())
            {
                Log.Warn(String.Format(
                    "Probably error occurred while dividing tasks to active and non-active for Scheduler. " +
                    "{0} tasks ({1}) -> {2} active tasks ({3}) and {4} scheduling tasks ({5})",
                    tasks.Count(), String.Join(", ", tasks.Select(t => t.TaskId.ToString())),
                    wf.ActiveTasks.Count, String.Join(", ", wf.ActiveTasks.Select(t => t.Id.ToString())),
                    wf.Tasks.Count, String.Join(", ", wf.Tasks.Select(t => t.Id.ToString()))
                ));
            }
        }

        private static IEnumerable<TaskSchedule> Reschedule(
            IEnumerable<Task> tasks, IEnumerable<Resource> resources,
            IEnumerable<TaskDependency> dependencies,
            //IDictionary<ulong, IEnumerable<string>> permissionsForTask,
            IDictionary<ulong, Dictionary<NodeConfig, Estimation>> estimationsForTask)
        {
            IEnumerable<TaskSchedule> result = null;
            ServiceProxies.SchedulerService.LaunchPlan schedResult;

            var scheduler = Discovery.GetSchedulerService();
            try
            {
                var urgentTask = tasks.FirstOrDefault(t => t.Priority == TaskPriority.Urgent &&
                    (t.State == TaskState.ReadyToExecute || t.State == TaskState.Scheduled));

                if (urgentTask != null)
                {
                    string urgentWfId = urgentTask.WfId;
                    var tasksForUrgentWf = tasks.Where(t =>
                           t.WfId == urgentWfId // urgent to plan
                        || t.State == TaskState.Started // already running
                        || (t.State == TaskState.ReadyToExecute && t.Priority == TaskPriority.Urgent &&
                            t.CurrentSchedule != null && !String.IsNullOrEmpty(t.CurrentSchedule.ResourceName)) // scheduled urgent
                    );

                    var filteredDependencies = dependencies.Where(d =>
                        tasksForUrgentWf.Any(t => t.TaskId == d.ChildTaskId && t.WfId == urgentWfId) && // todo: [!] ma ny "||", not "&&"?
                        tasksForUrgentWf.Any(t => t.TaskId == d.ParentTaskId && t.WfId == urgentWfId)
                    );

                    Log.Info(String.Format("WF '{0}' is urgent. Now scheduling only it.", urgentWfId));
                    Log.Debug("Tasks in urgent WF + active tasks: " + String.Join(", ", tasksForUrgentWf.Select(t => t.TaskId)));

                    /***** wf time bounds *****/
                    DateTime whenDefined = tasksForUrgentWf.Where(t => t.WfId == urgentWfId).Min(t => t.Time.WhenStarted[TaskTimeMetric.Postponed]);
                    double timeElapsed = (DateTime.Now - whenDefined).TotalSeconds;  // todo : DateTime.Now is different from when estimations was calculated

                    double minTimeInSec = Math.Max(0, double.Parse(urgentTask.ExecParams["MinTime"], System.Globalization.CultureInfo.InvariantCulture));
                    double maxTimeInSec = Math.Max(minTimeInSec, double.Parse(urgentTask.ExecParams["MaxTime"], System.Globalization.CultureInfo.InvariantCulture));

                    double minTimeRemained = Math.Max(0, minTimeInSec - timeElapsed);
                    double maxTimeRemained = Math.Max(0, maxTimeInSec - timeElapsed);

                    Log.Debug(String.Format("Time bounds for urgent WF '{0}' are [{1} - {2}] seconds (WF was defined {3} seconds ago)",
                        urgentWfId, minTimeRemained, maxTimeRemained, timeElapsed
                    ));

                    var wf = new ServiceProxies.SchedulerService.EstimatedUrgentWorkflow()
                    {
                        MinExecutionTime = minTimeRemained,
                        MaxExecutionTime = maxTimeRemained,
                    };


                    SetWorkflowDescription(wf, tasksForUrgentWf, resources, filteredDependencies, estimationsForTask, urgentWfId);

                    // todo : do no write every schedule to file
                    var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(ServiceProxies.SchedulerService.EstimatedUrgentWorkflow));
                    var memStream = new System.IO.MemoryStream();
                    serializer.WriteObject(memStream, wf);
                    string json = Encoding.UTF8.GetString(memStream.ToArray());
                    memStream.Close();
                    Log.Debug("wf to Scheduler: " + json);

                    schedResult = scheduler.RescheduleEstimated((ServiceProxies.SchedulerService.EstimatedUrgentWorkflow)wf);
                }
                else
                {
                    var wf = new ServiceProxies.SchedulerService.EstimatedWorkflow();
                    SetWorkflowDescription(wf, tasks, resources, dependencies, estimationsForTask);

                    Log.Debug("WF is non-urgent. Sending it to scheduler");
                    schedResult = scheduler.RescheduleEstimated(wf);
                }

                scheduler.Close();
            }
            catch (Exception e)
            {
                scheduler.Abort();
                Log.Warn("Exception in scheduler: " + e.ToString());
                throw;
            }

            if (schedResult == null)
            {
                Log.Warn("Scheduler returned null");
            }
            else
            {
                var actionConvert = new Func<ServiceProxies.SchedulerService.TaskSchedulerTaskState, ScheduledAction>(
                    actionByScheduler =>
                    {
                        switch (actionByScheduler)
                        {
                            case ServiceProxies.SchedulerService.TaskSchedulerTaskState.ABORTED:
                                return ScheduledAction.Abort;

                            case ServiceProxies.SchedulerService.TaskSchedulerTaskState.LAUNCHED:
                                return ScheduledAction.Run;

                            case ServiceProxies.SchedulerService.TaskSchedulerTaskState.SCHEDULED:
                                return ScheduledAction.None;

                            default:
                                throw new Exception(String.Format(
                                    "Could not map scheduler's task action ({0}) to executor's task action",
                                    actionByScheduler.ToString()
                                ));
                        }
                    }
                );

                var now = DateTime.Now;

                var paramsByScheduler = new Dictionary<ulong, Dictionary<string, string>>();
                foreach (var activeTask in schedResult.Plan)
                {
                    ulong taskId = activeTask.Id;

                    try
                    {
                        // ModifiedParams = new Dictionary<string, string>(activeTask.Parameters),
                        paramsByScheduler[taskId] = activeTask.Estimation.Result.Parameters.ToDictionary(p => p.Name, p => p.NewValue);
                        if (!paramsByScheduler[taskId].ContainsKey("P"))
                            paramsByScheduler[taskId]["P"] = "0";
                    }
                    catch (Exception e)
                    {
                        paramsByScheduler[taskId] = new Dictionary<string, string>();
                        paramsByScheduler[taskId]["P"] = "0";
                        // todo : nodesCount from scheduler's params

                        Log.Warn(String.Format(
                            "Exception while assigning scheduler's parameters for task {0}: {1}\n{2}",
                            taskId, e.Message, e.StackTrace
                        ));
                    }
                }

                result = schedResult.Plan.Select(activeTask => new TaskSchedule()
                {
                    TaskId = activeTask.Id,
                    Action = actionConvert(activeTask.State),

                    Estimation = new Estimation(
                        estimationsForTask[activeTask.Id].Single(pair =>
                            pair.Key.ResourceName == activeTask.Estimation.Destination.ResourceName &&
                            pair.Key.NodeName == activeTask.Estimation.Destination.NodeNames.Single()
                        ).Value)
                    {
                        CalcStart = now + TimeSpan.FromSeconds(activeTask.Estimation.LaunchTime),
                        //CalcDuration = TimeSpan.FromSeconds(activeTask.Estimation.Result.Time)
                        CalcDuration = TimeSpan.FromSeconds(activeTask.Estimation.Result.CalculationTime)
                    },

                    /*
                    EstimatedStartTime  = now + TimeSpan.FromSeconds(activeTask.Estimation.LaunchTime),
                    EstimatedFinishTime = now + TimeSpan.FromSeconds(activeTask.Estimation.LaunchTime 
                        + activeTask.Estimation.Result.CalculationTime),
                    */

                    ModifiedParams = paramsByScheduler[activeTask.Id],

                    Nodes = activeTask.Estimation.Destination.NodeNames.Select(nodeName => new NodeConfig()
                    {
                        NodeName = nodeName,
                        //Cores = Int32.Parse(activeTask.Estimation.Result.Parameters.First(par => par.Name == "P").NewValue),
                        Cores = UInt32.Parse(paramsByScheduler[activeTask.Id]["P"]),
                        ResourceName = activeTask.Estimation.Destination.ResourceName,
                    }).ToArray()
                });
            }

            return result;
        }
    }
}

