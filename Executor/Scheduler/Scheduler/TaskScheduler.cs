using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using TimeMeter;
using Scheduler.Heuristics;
using Common;
using System.ServiceModel;
using Common.VManager.DataTypes;
using Scheduler.Estimated;
using System.ComponentModel.Composition.Hosting;

namespace Scheduler
{
    public class TaskScheduler
    {
        public const double E = 0.0000005;

        private string _configPath;
        private string _appsConfigFile;
        private string _defaultHeuristicsName;
        private string _defaultUHeuristicsName;
        private bool _allowSettingHeuristics;

        private const string ConfigPathKey = "ConfigPath";
        private const string AppsConfigFileKey = "AppsConfigFile";
        public const string DefaultHeuristicsKey = "DefaultHeuristics";
        public const string DefaultUrgentHeuristicsKey = "DefaultUrgentHeuristics";
        private const string WebHostedKey = "IsWebHosted";
        private const string AllowHSettingsKey = "AllowSettingHeuristics";

        private static readonly IDictionary<string, Type> HeuristicsTypes = new Dictionary<string, Type>();
        private static readonly IDictionary<string, Type> UHeuristicsTypes = new Dictionary<string, Type>();
        
        private ISchedulerHeuristics defaultHeuristics = new MinMinHeuristics();

        private IDictionary<KeyValuePair<string, ulong>, EstimationResult> _estimations = new Dictionary<KeyValuePair<string, ulong>, EstimationResult>();

        private object StateLock = new object();

        //private VManagerClient vManager = new VManagerClient();

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public string GetDefaultUHName()
        {
            string res;
            if (_allowSettingHeuristics)
            {
                lock (StateLock)
                {
                    res = State.DefaultUHName;
                }
            }
            else res = _defaultUHeuristicsName;
            return res;
        }

        public string[] GetUHNames()
        {
            return UHeuristicsTypes.Keys.ToArray();
        }

        public void SetDefaultUHName(string newName)
        {   
            /*Configuration cfg;
            if (IsWebHosted())
            {
                logger.Debug("Is web hosted");
                cfg = System.Web.Configuration.WebConfigurationManager.OpenWebConfiguration("~");
            }
            else
            {
                logger.Debug("Is locally hosted");
                cfg = ConfigurationManager.OpenExeConfiguration(System.Reflection.Assembly.GetCallingAssembly().Location);
            }
            AppSettingsSection objAppsettings = (AppSettingsSection)cfg.GetSection("appSettings");
            if (objAppsettings != null)
            {
                if (objAppsettings.Settings.AllKeys.Any(k => k == _defaultUHeuristicsName))
                {
                    objAppsettings.Settings[_defaultUHeuristicsName].Value = newName;
                }
                else
                {
                    var e = new KeyValueConfigurationElement(_defaultUHeuristicsName, newName);
                    objAppsettings.Settings.Add(e);
                }
                cfg.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }*/
            lock (StateLock)
            {
                State.DefaultUHName = newName;
            }
        }

        private bool IsWebHosted()
        {
            try
            {
                return Boolean.Parse(ConfigurationManager.AppSettings.Get(WebHostedKey));
            }
            catch
            {
                return false;
            }
        }

        static TaskScheduler()
        {
            var container = new CompositionContainer(new DirectoryCatalog(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)));
            foreach (var v in container.GetExportedValues<ITaskBasedHeuristics>())
            {
                var attrs = v.GetType().GetCustomAttributes(typeof(HeuristicsNameAttribute), false);
                Debug.Assert(attrs.Length > 0);
                var attribute = attrs[0];
                var heuristicsName = ((HeuristicsNameAttribute)attribute).Name;
                HeuristicsTypes[heuristicsName] = v.GetType();
            }

            foreach (var v in container.GetExportedValues<IWFBasedUrgentHeuristics>())
            {
                var attrs = v.GetType().GetCustomAttributes(typeof(HeuristicsNameAttribute), false);
                Debug.Assert(attrs.Length > 0);
                var attribute = attrs[0];
                var heuristicsName = ((HeuristicsNameAttribute)attribute).Name;
                UHeuristicsTypes[heuristicsName] = v.GetType();
            }
        }

        public static ISchedulerHeuristics CreateHeuristics(string name)
        {
            try
            {
                return (ISchedulerHeuristics)HeuristicsTypes[name].GetConstructor(Type.EmptyTypes).Invoke(null);
            }
            catch
            {
                throw new ArgumentException("Heuristics not found: " + name);
            }
        }

        public static IWFBasedUrgentHeuristics CreateUrgentHeuristics(string name)
        {
            try
            {
                return (IWFBasedUrgentHeuristics)UHeuristicsTypes[name].GetConstructor(Type.EmptyTypes).Invoke(null);
            }
            catch
            {
                throw new ArgumentException("Heuristics not found: " + name);
            }
        }

        public static ITaskBasedHeuristics CreateTaskBasedHeuristics(string name)
        {
            try
            {
                return (ITaskBasedHeuristics)HeuristicsTypes[name].GetConstructor(Type.EmptyTypes).Invoke(null);
            }
            catch
            {
                throw new ArgumentException("Heuristics not found: " + name);
            }
        }

        public TaskScheduler()
        {
            System.Configuration.ConfigurationManager.RefreshSection("appSettings");
            var _settingsReader = new AppSettingsReader();
            try
            {
                _configPath = (string)_settingsReader.GetValue(ConfigPathKey, typeof(string));
                _appsConfigFile = (string)_settingsReader.GetValue(AppsConfigFileKey, typeof(string));
                _defaultHeuristicsName = (string)_settingsReader.GetValue(DefaultHeuristicsKey, typeof(string));
                _defaultUHeuristicsName = (string)_settingsReader.GetValue(DefaultUrgentHeuristicsKey, typeof(string));
                _allowSettingHeuristics = Boolean.Parse((string)_settingsReader.GetValue(AllowHSettingsKey, typeof(string)));
            }
            catch (Exception e)
            {
                throw new ConfigurationErrorsException("Unable to read Scheduler service configuration params", e);
            }
            var filePath = _configPath + System.IO.Path.PathSeparator + _appsConfigFile;
            /*try
            {
                TaskTimeMeter.LoadParameters(filePath);
                //TaskTimeMeter.UpdateDescriptions();
                //TaskTimeMeter.StoreParameters(filePath);
                //TaskTimeMeter.LoadParameters(filePath);
            }
            catch (Exception e)
            {
                throw new IOException("TimeMeter service configuration file " + filePath + " not found", e);
            }*/            
        }

        public EstimationResult Estimate(Task task, string resourceName, LaunchDestination destination, bool optimize)
        {
            var key = new KeyValuePair<string, ulong>(resourceName, task.Id);
            if (_estimations.ContainsKey(key))
            {
                return _estimations[key];
            }
            else
            {
                var result = TaskTimeMeter.MeasureAppTime(task.ApplicationName, task.Parameters, resourceName, destination, optimize);
                _estimations[key] = result;
                return result;
            }
        }

        public IEnumerable<string> GetAppNames()
        {
            return TaskTimeMeter.GetAppNames();
        }

        public IEnumerable<string> GetClusterNames()
        {
            return TaskTimeMeter.GetClusterNames();
        }
        
        #region Datatypes

        [DataContract]
        public abstract class BaseTask
        {
            [DataMember(Order=0)] public ulong Id;
            [DataMember] public string ApplicationName;
            [DataMember] public Dictionary<string, string> Parameters;
            [DataMember] public string WFid { get; set; } 

            protected BaseTask()
            {
            }

            protected BaseTask(BaseTask other)
            {
                Id = other.Id;
                ApplicationName = other.ApplicationName;
                Parameters = other.Parameters;
            }
        }

        [DataContract]
        public class Task : BaseTask
        {
            
            [DataMember] public LaunchDestination[] SuitableDestinations;
            

            public Task() : base()
            {
            }
            
            public Task(Task other) : base(other)
            {
                
                SuitableDestinations = (LaunchDestination[]) other.SuitableDestinations.Clone();
            }
        }

        [DataContract]
        public class ActiveTask : Task, IComparable, IComparable<ActiveTask>
        {
            public ActiveTask()
                : base()
            {
            }

            public ActiveTask(ActiveTask other)
                : base(other)
            {
                IsUrgent = other.IsUrgent;
                State = other.State;
                Estimation = other.Estimation;
                Destination = other.Destination;
                EstimatedLaunchTime = other.EstimatedLaunchTime;
            }

            public ActiveTask(Task other)
                : base(other)
            {
            }

            [DataMember]
            public LaunchDestination Destination;

            [DataMember]
            public double EstimatedLaunchTime;
            
            [DataMember]
            public EstimationResult Estimation;

            [DataMember]
            public TaskState State;

            [DataMember]
            public bool IsUrgent;

            public int CompareTo(object obj)
            {
                if (!(obj is ActiveTask))
                {
                    return 1;
                }
                else
                {
                    
                    return (int) Math.Round(Estimation.Time - ((ActiveTask)obj).Estimation.Time);
                }
            }

            public int CompareTo(ActiveTask other)
            {
                return (int)Math.Round(Estimation.Time - other.Estimation.Time);
            }

            public override string ToString()
            {
                return ToString(false);
            }

            public string ToString(bool verbose)
            {
                var res = String.Format(verbose ? "{0} (Id #{1}) => {2}" : "{0}:{1}:{2}", ApplicationName, Id, Destination.ResourceName);
                if (verbose)
                {
                    res += String.Format(" Estimated execution time: {0}", Math.Round(Estimation.Time, 3));
                }
                return res;
            }
        }

        private class TaskComparer : IComparer<ActiveTask>
        {
            private ILookup<string, NodeAvailabilityTime> states;

            public TaskComparer(ILookup<string, NodeAvailabilityTime> states)
            {
                this.states = states;
            }

            public int Compare(ActiveTask x, ActiveTask y)
            {
                return (int)Math.Round(x.Estimation.Time + states[x.Destination.ResourceName].Where(n => x.Destination.NodeNames.Contains(n.NodeName)).Max(n => n.GetAvailabilityTime()) - y.Estimation.Time - states[y.Destination.ResourceName].Where(n => y.Destination.NodeNames.Contains(n.NodeName)).Max(n => n.GetAvailabilityTime()));
                /*if (diff < 0)
                {
                    return -1;
                }
                else if (diff > 0)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }*/
            }
        }

        [DataContract]
        public enum TaskState
        {
            [EnumMember]
            LAUNCHED,
            [EnumMember]
            SCHEDULED,
            [EnumMember]
            ABORTED
        }

        [DataContract]
        public class Workflow
        {
            [DataMember]
            public string Name;

            [DataMember]
            public bool Optimize
            {
                get;
                set;
            }
            
            public bool IsSequence
            {
                get;
                protected set;
            }

            public bool IsUrgent
            {
                get;
                protected set;
            }

            [DataMember]
            public Resource[] Resources;            

            [DataMember] public List<Task> Tasks = new List<Task>();
            [DataMember] public List<ActiveTask> ActiveTasks = new List<ActiveTask>();
            
            public string[] ResourcesNames = new string[0];

            public LaunchDestination[] Destinations = new LaunchDestination[0];
            
            public Workflow()
            {
                IsSequence = false;
                IsUrgent = false;
            }

            public Workflow(Workflow other)
                : this()
            {   
                if (other == null)
                {
                    return;
                }
                Resources = (Resource[]) other.Resources.Clone();
                ResourcesNames = (string[])other.ResourcesNames.Clone();
                Destinations = (LaunchDestination[])other.Destinations.Clone();
                Tasks.AddRange(other.Tasks);
                ActiveTasks.AddRange(other.ActiveTasks);
                Name = other.Name;
                Optimize = other.Optimize;
            }

            public override string ToString()
            {
                var nl = System.Environment.NewLine;
                return "Workflow" + nl +
                    String.Format("Name: {0} Tasks count: {1} Params optimized: {2}", Name, Tasks.Count, Optimize) + nl
                    ;
            }
        }

        [DataContract]
        public class UrgentWorkflow : Workflow
        {
            public UrgentWorkflow()
                : this(null)
            {                
            }

            public UrgentWorkflow(Workflow other)
                : base(other)
            {
                IsUrgent = true;
                IsSequence = true;
            }

            public UrgentWorkflow(UrgentWorkflow other)
                : this((Workflow) other)
            {
                if (other == null)
                {
                    return;
                }
                MinExecutionTime = other.MinExecutionTime;
                MaxExecutionTime = other.MaxExecutionTime;
            }

            [DataMember]
            public double MinExecutionTime;

            [DataMember]
            public double MaxExecutionTime;

            public override string ToString()
            {
                var nl = System.Environment.NewLine;
                return "Urgent Workflow" + nl +
                    String.Format("Name: {0} Tasks count: {5} Params optimized: {3}{4}Minimum execution time: {1} Maximum execution time: {2}", Name, MinExecutionTime, MaxExecutionTime, Optimize, nl, Tasks.Count) + nl
                    ;
            }
        }

        [DataContract]
        public class OldNodeAvailabilityTime
        {
            public OldNodeAvailabilityTime(string resourceName, string nodeName)
            {
                ResourceName = resourceName;
                NodeName = nodeName;
            }

            public OldNodeAvailabilityTime(Node node)
                : this(node.ResourceName, node.DNSName)
            {                
            }

            [DataMember]
            public string ResourceName;

            [DataMember]
            public string NodeName;

            [DataMember]
            public double AvailabilityTime;
        }

        [DataContract]
        public class LaunchPlan
        {
            [DataMember]
            public List<ActiveTask> Plan = new List<ActiveTask>();

            [DataMember]
            public double EstimatedExecutionTime = 0;

            public override string ToString()
            {
                return ToString(false);
            }

            public string GetHeader()
            {
                return String.Format("Estimated execution time: {0}", Math.Round(EstimatedExecutionTime, 3));
            }

            public string ToString(bool verbose)
            {
                var nl = System.Environment.NewLine;
                var result = GetHeader() + nl;
                var i = 0;
                foreach (var task in Plan)
                {
                    result += task.ToString(verbose);
                    if (!verbose)
                    {
                        if (i < Plan.Count - 1)
                        {
                            result += '|';
                        }
                        /*if ((i + 1) % 2 == 0)
                        {
                            result += nl;
                        }
                        else
                        {
                            result += '\t';
                        }*/
                    }
                    else
                    {
                        if (i < Plan.Count - 1)
                        {
                            result += nl;
                        }
                    }
                    i++;
                }
                if (verbose && Plan.Any(t => t.Estimation.Parameters.Count > 0))
                {
                    if (Plan.Count <= 1)
                    {
                        result += nl;
                    }
                    result += "Optimized parameters:" + nl;
                    var oParams = Plan.SelectMany(t => t.Estimation.Parameters);
                    var j = 0;
                    double pres;
                    foreach (var param in oParams)
                    {
                        result += String.Format("Name: {0}\tPrevious value: {1}\tNew value: {2}",
                            param.Name,
                            (Double.TryParse(param.InitialValue, out pres) ? Math.Round(pres, 3).ToString() : param.InitialValue),
                            (Double.TryParse(param.NewValue, out pres) ? Math.Round(pres, 3).ToString() : param.NewValue));
                        if (j++ < oParams.Count())
                        {
                            result += nl;
                        }
                    }
                }
                return result;
            }
        }


        public interface IResourceState
        {
            double GetAvailabilityTime(bool ignoreNonUrgent);

            string ResourceName
            {
                get;
            }

            string NodeName
            {
                get;
            }
        }
        
        public class NodeAvailabilityTime : IResourceState, ICloneable
        {
            protected NodeAvailabilityTime()
            {
            }

            public NodeAvailabilityTime(string resourceName, string nodeName)
                : this()
            {
                this.resourceName = resourceName;
                this.nodeName = nodeName;
            }

            public NodeAvailabilityTime(Node node)
                : this()
            {
                this.Node = node;
            }

            private List<ActiveEstimatedTask> DestinedTasks = new List<ActiveEstimatedTask>();

            public void AssignTask(ActiveEstimatedTask task)
            {
                if (task.State == TaskState.LAUNCHED && DestinedTasks.Any())
                {
                    var currentTask = LaunchedTask;
                    if (currentTask != null)
                    {
                        DestinedTasks.Remove(currentTask);
                    }
                }
                DestinedTasks.Add(task);
            }

            public ActiveEstimatedTask LaunchedTask
            {
                get
                {
                    return DestinedTasks.SingleOrDefault(t => t.State == TaskState.LAUNCHED);
                }
            }

            public Node Node
            {
                get;
                protected set;
            }

            protected string resourceName;

            protected string nodeName;

            [DataMember]
            public string ResourceName
            {
                get
                {
                    if (Node == null)
                    {
                        return resourceName;
                    }
                    else
                    {
                        return Node.ResourceName;
                    }
                }

                private set
                {
                }
            }

            [DataMember]
            public string NodeName
            {
                get
                {
                    if (Node == null)
                    {
                        return nodeName;
                    }
                    else
                    {
                        return Node.DNSName;
                    }
                }

                private set
                {
                }
            }

            public object Clone()
            {
                var result = new NodeAvailabilityTime();
                result.DestinedTasks.AddRange(result.DestinedTasks);
                result.Node = Node;
                return result;
            }

            public double GetAvailabilityTime(bool ignoreNonUrgent)
            {
                var tasks = !ignoreNonUrgent ? DestinedTasks : DestinedTasks.Where(t => t.IsUrgent);
                return tasks.Sum(t => t.Estimation.Result.Time);
            }

            public double GetAvailabilityTime()
            {
                return GetAvailabilityTime(false);
            }
        }

        #endregion

        #region Processing

        public Estimated.LaunchPlan RescheduleEstimated(EstimatedWorkflow workflow)
        {
            Estimated.LaunchPlan mostTrueResult;
            if (!workflow.IsUrgent)
            {
                var h = CreateTaskBasedHeuristics(GetDefaultHName());
                workflow.UpdateDependencies();
                var wrappers = GenerateWrappers(workflow);
                var estimations = MakeOverallEstimations(workflow, wrappers);
                var result = new Estimated.LaunchPlan();
                var roots = workflow.Tasks.Where(t => !t.IsScheduled && t.IsReady);

                {
                    var busyWrappers = wrappers.Where(w => w.GetAvailabilityTime() > E).ToArray();
                    var freeWrappers = wrappers.Except(busyWrappers).ToArray();
                    for (var i = 0; i < estimations.Count; i++)
                    {
                        if (estimations[i].Count() > 1)
                        {
                            /*var estimationsOnFreeNodes = estimations[i].Where(e => e.Estimation.Destination.NodeNames.All(n => freeWrappers.Any(fw => fw.ResourceName == e.Estimation.Destination.ResourceName && fw.NodeName == n))).OrderBy(t => t.Estimation.NodesTimings.Max(n => n.GetAvailabilityTime()) + t.Estimation.Result.Time);
                            var otherEstimations = estimations[i].Except(estimationsOnFreeNodes).OrderBy(t => t.Estimation.NodesTimings.Max(n => n.GetAvailabilityTime()) + t.Estimation.Result.Time);
                            estimations[i] = new List<ActiveEstimatedTask>();
                            ((List<ActiveEstimatedTask>)estimations[i]).AddRange(estimationsOnFreeNodes);
                            ((List<ActiveEstimatedTask>)estimations[i]).AddRange(otherEstimations);*/
                            estimations[i] = estimations[i].OrderBy(t => t.Estimation.NodesTimings.Max(n => n.GetAvailabilityTime()) + t.Estimation.Result.Time).ToList();
                        }
                    }
                }

                while (roots.Count() > 0)
                {
                    var rootIds = roots.Select(r => r.Id);
                    var effectiveEstimations = estimations.Where(e => rootIds.Contains(e.First().Id)).ToList();
                    var prevInstance = h.ChooseTask(workflow, effectiveEstimations);
                    //var nodeWrappers = wrappers.Where(w => w.ResourceName == prevInstance.Estimation.Destination.ResourceName && prevInstance.Estimation.Destination.NodeNames.Contains(w.NodeName));
                    var task = workflow.Tasks.Single(t => t.Id == prevInstance.Id);
                    var depsWaitingTime = task.RequiresDependencies.Count == 0 ? 0 : task.RequiresDependencies.Max(d => d.ScheduledInstance.Estimation.LaunchTime + d.ScheduledInstance.Estimation.Result.Time);
                    prevInstance.Estimation.LaunchTime = Math.Max(depsWaitingTime, prevInstance.Estimation.NodesTimings.Max(n => n.GetAvailabilityTime()));
                    prevInstance.State = prevInstance.Estimation.LaunchTime > E || prevInstance.Estimation.NodesTimings.Any(w => w.GetAvailabilityTime() > E) ? TaskState.SCHEDULED : TaskState.LAUNCHED;
                    var clonedInstance = new ActiveEstimatedTask(prevInstance);

                    foreach (var node in prevInstance.Estimation.NodesTimings)
                    {
                        node.AssignTask(clonedInstance);
                    }
                    var seq = estimations.Single(e => e.First().Id == prevInstance.Id);
                    estimations.Remove(seq);                    
                    result.Plan.Add(clonedInstance);
                    task.ScheduledInstance = clonedInstance;
                    roots = workflow.Tasks.Where(t => !t.IsScheduled && t.IsReady);
                    var busyWrappers = wrappers.Where(w => w.GetAvailabilityTime() > E);
                    var freeWrappers = wrappers.Except(busyWrappers);
                    for (var i = 0; i < estimations.Count; i++)
                    {
                        if (estimations[i].Count() > 1)
                        {
                            /*var estimationsOnFreeNodes = estimations[i].Where(e => e.Estimation.Destination.NodeNames.All(n => freeWrappers.Any(fw => fw.ResourceName == e.Estimation.Destination.ResourceName && fw.NodeName == n))).OrderBy(t => t.Estimation.NodesTimings.Max(n => n.GetAvailabilityTime()) + t.Estimation.Result.Time);
                            var otherEstimations = estimations[i].Except(estimationsOnFreeNodes).OrderBy(t => t.Estimation.NodesTimings.Max(n => n.GetAvailabilityTime()) + t.Estimation.Result.Time);
                            estimations[i] = new List<ActiveEstimatedTask>();
                            ((List<ActiveEstimatedTask>)estimations[i]).AddRange(estimationsOnFreeNodes);
                            ((List<ActiveEstimatedTask>)estimations[i]).AddRange(otherEstimations);*/
                            estimations[i] = estimations[i].OrderBy(t => t.Estimation.NodesTimings.Max(n => n.GetAvailabilityTime()) + t.Estimation.Result.Time).ToList();
                        }
                    }
                }
                result.NodesTimings = wrappers.ToList();
                result.EstimatedExecutionTime = result.Plan.Any() ? result.Plan.Max(t => t.Estimation.LaunchTime + t.Estimation.Result.Time) : 0;
                Debug.Assert(workflow.Tasks.All(t => t.IsScheduled));
                mostTrueResult = result;
            }
            else
            {
                var uwf = (EstimatedUrgentWorkflow) workflow;
                var h = CreateUrgentHeuristics(GetDefaultUHName());
                mostTrueResult = h.MakePlan(uwf);
            }
            var hasPlanDups = mostTrueResult.Plan.Any(t => t.State == TaskState.LAUNCHED && mostTrueResult.Plan.Any(t2 => t2.Id != t.Id && t2.State == TaskState.LAUNCHED && t.Estimation.Destination.IsLike(t2.Estimation.Destination)));
            var hasWfDups = mostTrueResult.Plan.Any(t => t.State == TaskState.LAUNCHED && workflow.ActiveTasks.Any(t2 => t.Id != t2.Id && t2.State == TaskState.LAUNCHED && t.Estimation.Destination.IsLike(t2.Estimation.Destination)));
            return (Scheduler.Estimated.LaunchPlan) mostTrueResult;
        }

        public static List<IEnumerable<ActiveEstimatedTask>> MakeOverallEstimations(EstimatedWorkflow workflow, NodeAvailabilityTime[] wrappers)
        {
            var estimatedTasks = new List<IEnumerable<ActiveEstimatedTask>>(workflow.Tasks.Count);
            for (var i = 0; i < workflow.Tasks.Count; i++)
            {
                var task = workflow.Tasks[i];
                estimatedTasks.Add(new List<ActiveEstimatedTask>());
                for (var j = 0; j < task.Estimations.Length; j++)
                {                    
                    var estimation = task.Estimations[j];
                    var nodesNumber = Int32.Parse(estimation.Result.Parameters.Single(p => p.Name == NodesCountExtractor.NODES).NewValue);
                    var coresNumber = Int32.Parse(estimation.Result.Parameters.Single(p => p.Name == ProcessorCountPerNode.P).NewValue);
                    var nodeWrappers = wrappers.Where(w => w.ResourceName == estimation.Resource.Name && w.Node.CoresTotal >= coresNumber && estimation.Resource.Nodes.Any(n => n.DNSName == w.NodeName));
                    var optionsNumber = (int)Math.Pow(nodeWrappers.Count(), nodesNumber);
                    var dests = new LaunchDestination[optionsNumber];
                    var indices = Enumerable.Repeat(0, nodesNumber).ToArray();
                    var baseValues = Enumerable.Repeat(nodeWrappers.Count(), nodesNumber).ToArray();
                    var usedNodes = new NodeAvailabilityTime[nodesNumber];
                    var resName = estimation.Resource.Name;
                    for (var k = 0; k < optionsNumber; k++)
                    {
                        for (var l = 0; l < usedNodes.Length; l++)
                        {
                            usedNodes[l] = nodeWrappers.ElementAt(indices[l]);
                        }
                        var dest = new LaunchDestination()
                        {
                            ResourceName = resName,
                            NodeNames = usedNodes.Select(un => un.NodeName).ToArray()
                        };
                        dests[k] = dest;
                        AdvanceIndices(indices, baseValues);
                    }
                    dests = dests.Where(d => d.NodeNames.Distinct().Count() == nodesNumber).ToArray();
                    foreach (var dest in dests)
                    {
                        var taskOnResource = new ActiveEstimatedTask(task);
                        taskOnResource.Estimation.Result = estimation.Result;
                        taskOnResource.Estimation.Destination = dest;
                        taskOnResource.Estimation.NodesTimings = nodeWrappers.Where(w => w.ResourceName == dest.ResourceName && dest.NodeNames.Contains(w.NodeName)).ToArray();                        
                        ((List<ActiveEstimatedTask>)estimatedTasks[i]).Add(taskOnResource);
                    }
                }
            }
            return estimatedTasks;
        }

        /*private void LaunchVMs(Workflow workflow, LaunchPlan plan)
        {
            var startedMachines = new List<string>();
            bool virtualsUsed = false;

            foreach (var t in plan.Plan)
            {
                if (virtualsUsed && workflow.IsSequence)
                {
                    t.State = TaskState.SCHEDULED;
                    t.EstimatedLaunchTime = 0;
                    continue;
                }

                var plannedNodes = workflow.Resources.Single(r => r.Name == t.Destination.ResourceName).Nodes.Where(n => t.Destination.NodeNames.Any(s => s == n.DNSName));
                foreach (var node in plannedNodes)
                {
                    var isVirtual = Boolean.Parse(ClusterParameterReader.GetValue(IsNodeVirtual.IS_VIRTUAL, node));
                    if (isVirtual)
                    {                        
                        var parameters = Newtonsoft.Json.JsonConvert.DeserializeObject<HostConnectionParams>(ClusterParameterReader.GetValue(VirtualConnectionParams.VCP, node));
                        try
                        {
                            var host = vManager.AddHost(parameters);
                            if (vManager.ConnectToHost(host.Name))
                            {
                                var state = vManager.GetMachineState(host.Name, node.DNSName);
                                if (state.State != MachineState.RUNNING || !vManager.IsGuestOSRunning(host.Name, node.DNSName))
                                {
                                    virtualsUsed = true;
                                    if (!startedMachines.Any(m => m == node.DNSName))
                                    {
                                        switch (state.State)
                                        {
                                            case MachineState.STOPPED:
                                                vManager.StartMachine(host.Name, node.DNSName, new Dictionary<string, string>());
                                                break;
                                            case MachineState.SUSPENDED:
                                                vManager.WakeMachine(host.Name, node.DNSName);
                                                break;
                                        }
                                        startedMachines.Add(node.DNSName);
                                    }
                                    t.State = TaskState.SCHEDULED;
                                    t.EstimatedLaunchTime = 0;
                                }
                                else
                                {
                                    startedMachines.Add(node.DNSName);
                                }
                            }                            
                        }
                        catch (FaultException<ErrorMessage> e)
                        {

                        }
                    }
                }
            }
        }*/

        public static NodeAvailabilityTime[] GenerateWrappers(EstimatedWorkflow workflow)
        {
            var wrs = workflow.Tasks.SelectMany(t => t.Estimations.SelectMany(e => e.Resource.Nodes.Select(n => new NodeAvailabilityTime(n)))).Distinct(new RWComparer()).ToList();
            foreach (var wrapper in wrs)
            {
                var d = new LaunchDestination() { ResourceName = wrapper.ResourceName, NodeNames = new[] { wrapper.NodeName } };
                foreach (var t in workflow.ActiveTasks.Where(t => t.Estimation.Destination.IsLike(d)))
                {
                    wrapper.AssignTask(t);
                }
            }
            return wrs.ToArray();
        }

        private class RWComparer : IEqualityComparer<NodeAvailabilityTime>
        {
            public bool Equals(NodeAvailabilityTime x, NodeAvailabilityTime y)
            {
                if (ReferenceEquals(x, null) != ReferenceEquals(y, null))
                {
                    return false;
                }
                if (ReferenceEquals(x, null))
                {
                    return true;
                }
                return x.ResourceName.Equals(y.ResourceName) && x.NodeName.Equals(y.NodeName);
            }

            public int GetHashCode(NodeAvailabilityTime obj)
            {
                return obj.ResourceName.GetHashCode() * obj.NodeName.GetHashCode();
            }
        }

        private static void AdvanceIndices(int[] indices, int[] baseValues)
        {            
            if (indices.Length > 0)
            {
                indices[0]++;
            }
            var i = 0;
            while (i < indices.Length && indices[i] >= baseValues[i])
            {
                indices[i] = 0;
                if (++i < indices.Length)
                {
                    indices[i]++;
                }
            }
            /*for (var i = 0; i < indexes.Length; i++)
            {
                if (indexes[i] >= baseValue)
                {
                    indexes[i] = 0;
                    if (i < indexes.Length - 1)
                    {
                        indexes[i + 1]++;
                    }
                }
                else
                {
                    break;
                }
            }*/
        }

        #endregion


        public string GetDefaultHName()
        {
            string res;
            if (_allowSettingHeuristics)
            {
                lock (StateLock)
                {
                    res = State.DefaultHName;
                }
            }
            else res = _defaultHeuristicsName;
            return res;
        }

        public IEnumerable<string> GetHNames()
        {
            return HeuristicsTypes.Keys.AsEnumerable();
        }

        public void SetDefaultHName(string newName)
        {
            lock (StateLock)
            {
                State.DefaultHName = newName;
            }
        }
    }
}
