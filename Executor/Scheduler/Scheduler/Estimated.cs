using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Common;
using TimeMeter;
using System.Diagnostics;
using Scheduler;

namespace Scheduler.Estimated
{
    [DataContract]
    public class ResourceEstimation
    {
        [DataMember]
        public Resource Resource;

        [DataMember]
        public EstimationResult Result;
    }

    [DataContract]
    public class ActiveEstimation : ResourceEstimation, ICloneable
    {
        public TaskScheduler.NodeAvailabilityTime[] NodesTimings;

        [DataMember]
        public LaunchDestination Destination;

        [DataMember]
        public double LaunchTime;

        

        public object Clone()
        {
            var result = new ActiveEstimation();
            result.Resource = Resource;
            result.Result = Result;
            result.LaunchTime = LaunchTime;
            result.Destination = Destination;
            result.NodesTimings = NodesTimings == null ? null : NodesTimings.Select(nt => nt.Clone() as TaskScheduler.NodeAvailabilityTime).ToArray();
            return result;
        }
    }

    [DataContract]
    public class EstimatedTask : TaskScheduler.BaseTask
    {
        [DataMember]
        public ResourceEstimation[] Estimations = Enumerable.Empty<ResourceEstimation>().ToArray();

        public EstimatedTask()
        {
        }

        public EstimatedTask(EstimatedTask other)
            : base(other)
        {
            Estimations = (ResourceEstimation[])other.Estimations.Clone();
            RequiresDependencies = new List<EstimatedTask>(other.RequiresDependencies);
            ProvidesDependencies = new List<EstimatedTask>(other.ProvidesDependencies);
        }

        public List<EstimatedTask> RequiresDependencies = new List<EstimatedTask>();

        public List<EstimatedTask> ProvidesDependencies = new List<EstimatedTask>();

        public int DepthLevel = 0;

        public ActiveEstimatedTask ScheduledInstance;

        public bool IsScheduled
        {
            get
            {
                return ScheduledInstance != null;
            }
        }

        public bool IsReady
        {
            get
            {
                return RequiresDependencies.Count == 0 || RequiresDependencies.All(t => t.IsScheduled);
            }
        }
    }



    [DataContract]
    public class ActiveEstimatedTask : TaskScheduler.BaseTask, ICloneable
    {
        public ActiveEstimatedTask()
        {
        }

        internal bool IgnoreNonUrgent = true;

        [DataMember]
        public TaskScheduler.TaskState State;

        [DataMember]
        public ActiveEstimation Estimation = new ActiveEstimation();

        [DataMember]
        public bool IsUrgent;

        [DataMember]
        public bool MonopolizeResource;

        public ActiveEstimatedTask(TaskScheduler.BaseTask task)
            : base(task)
        {
        }

        public bool IsOlderThan(ActiveEstimatedTask other)
        {
            return other == null || Id < other.Id;
        }

        public ActiveEstimatedTask(ActiveEstimatedTask task)
            : base(task)
        {
            var t = task.Clone() as ActiveEstimatedTask;
            State = t.State;
            IsUrgent = t.IsUrgent;
            MonopolizeResource = t.MonopolizeResource;
            Estimation = t.Estimation;
        }

        public object Clone()
        {
            var result = new ActiveEstimatedTask(this as TaskScheduler.BaseTask);
            result.State = State;
            result.IsUrgent = IsUrgent;
            result.MonopolizeResource = MonopolizeResource;
            result.Estimation = Estimation == null ? null : Estimation.Clone() as ActiveEstimation;            
            return result;
        }
    }

    [DataContract]
    [KnownType(typeof(EstimatedUrgentWorkflow))]
    public class EstimatedWorkflow
    {
        public EstimatedWorkflow()
        {
        }

        public EstimatedWorkflow(EstimatedWorkflow other)
            : this()
        {
            if (other == null)
            {
                return;
            }

            this.Name = other.Name;
            this.Optimize = Optimize;
        }

        [DataMember]
        public string Name;

        [DataMember]
        public bool Optimize
        {
            get;
            set;
        }

        public virtual bool IsUrgent
        {
            get
            {
                return false;
            }
        }

        [DataMember]
        public List<EstimatedTask> Tasks = new List<EstimatedTask>();

        [DataMember]
        public List<ActiveEstimatedTask> ActiveTasks = new List<ActiveEstimatedTask>();

        [DataMember]
        public List<TasksDepenendency> Dependencies = new List<TasksDepenendency>();

        public void UpdateDependencies()
        {
            foreach (var task in Tasks)
            {
                if (task.RequiresDependencies == null)
                {
                    task.RequiresDependencies = new List<EstimatedTask>();
                }
                if (task.ProvidesDependencies == null)
                {
                    task.ProvidesDependencies = new List<EstimatedTask>();
                }
                task.RequiresDependencies.Clear();                
            }
            foreach (var task in Tasks)
            {
                var depsRequired = Dependencies != null ? Dependencies.Where(d => d.ConsumerId == task.Id).Select(d => d.ProviderId).ToArray() : Enumerable.Empty<ulong>().ToArray();
                var providers = Tasks.Where(t => depsRequired.Contains(t.Id));
                task.RequiresDependencies.AddRange(providers);                
            }
            foreach (var task in Tasks)
            {
                var depsProvided = Dependencies != null ? Dependencies.Where(d => d.ProviderId == task.Id).Select(d => d.ConsumerId).ToArray() : Enumerable.Empty<ulong>().ToArray();
                var consumers = Tasks.Where(t => depsProvided.Contains(t.Id));
                task.ProvidesDependencies.AddRange(consumers);
            }

            DFS(Tasks.Where(t => t.RequiresDependencies.Count == 0), 1);
        }

        private void DFS(IEnumerable<EstimatedTask> tasks, int level)
        {
            foreach (var t in tasks)
            {
                if (t.DepthLevel == 0)
                {
                    t.DepthLevel = level;
                    DFS(t.ProvidesDependencies, level + 1);
                }
            }
        }
    }

    [DataContract]
    public class TasksDepenendency
    {
        [DataMember]
        public ulong ProviderId;

        [DataMember]
        public ulong ConsumerId;

        public TasksDepenendency()
        {
        }

        public TasksDepenendency(ulong providerId, ulong consumerId)
        {
            ProviderId = providerId;
            ConsumerId = consumerId;
        }
    }

    [DataContract]
    public class EstimatedUrgentWorkflow : EstimatedWorkflow
    {
        public EstimatedUrgentWorkflow()
            : this(null)
        {
        }

        public EstimatedUrgentWorkflow(EstimatedWorkflow other)
            : base(other)
        {
        }

        public override bool IsUrgent
        {
            get
            {
                return true;
            }
        }

        public EstimatedUrgentWorkflow(EstimatedUrgentWorkflow other)
            : this((EstimatedWorkflow)other)
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
            return "Urgent Workflow";
        }
    }

    [DataContract]
    public class LaunchPlan : ICloneable
    {
        [DataMember]
        public List<ActiveEstimatedTask> Plan = new List<ActiveEstimatedTask>();

        [DataMember]
        public List<Scheduler.TaskScheduler.NodeAvailabilityTime> NodesTimings = new List<Scheduler.TaskScheduler.NodeAvailabilityTime>();

        [DataMember]
        public double EstimatedExecutionTime;

        public LaunchPlan()
        {
        }

        public LaunchPlan(LaunchPlan other)
        {
            EstimatedExecutionTime = other.EstimatedExecutionTime;
            NodesTimings = other.NodesTimings == null ? null : other.NodesTimings.Select(nt => nt.Clone() as TaskScheduler.NodeAvailabilityTime).ToList();
            Plan = other.Plan == null ? null : other.Plan.Select(t => t.Clone() as ActiveEstimatedTask).ToList();

        }

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
            return "";
        }

        public virtual object Clone()
        {
            var result = new LaunchPlan();
            result.EstimatedExecutionTime = EstimatedExecutionTime;
            result.NodesTimings = NodesTimings == null ? null : NodesTimings.Select(nt => nt.Clone() as TaskScheduler.NodeAvailabilityTime).ToList();
            result.Plan = Plan == null ? null : Plan.Select(t => t.Clone() as ActiveEstimatedTask).ToList();
            return result;
        }
    }

    public interface ITaskBasedHeuristics
    {
        ActiveEstimatedTask ChooseTask(EstimatedWorkflow workflow, List<IEnumerable<ActiveEstimatedTask>> tasks);
    }

    [Heuristics.HeuristicsName("MinMin")]
    [System.ComponentModel.Composition.Export(typeof(ITaskBasedHeuristics))]
    public class EMinMinHeuristics : ITaskBasedHeuristics
    {

        public ActiveEstimatedTask ChooseTask(EstimatedWorkflow workflow, List<IEnumerable<ActiveEstimatedTask>> tasks)
        {
            return tasks.OrderBy(t =>
            {
                var parent = workflow.Tasks.Single(wt => wt.Id == t.First().Id);
                var depsWaitingTime = parent.RequiresDependencies.Count == 0 ? 0 : parent.RequiresDependencies.Max(d => d.ScheduledInstance.Estimation.LaunchTime + d.ScheduledInstance.Estimation.Result.Time);
                var nodesWaitingTime = t.First().Estimation.NodesTimings.Max(n => n.GetAvailabilityTime());
                var waitingTime = Math.Max(depsWaitingTime, nodesWaitingTime);
                return waitingTime + t.First().Estimation.Result.Time;
            }).First().First();
        }
    }

    [Heuristics.HeuristicsName("MaxMin")]
    [System.ComponentModel.Composition.Export(typeof(ITaskBasedHeuristics))]
    public class EMaxMinHeuristics : ITaskBasedHeuristics
    {

        public ActiveEstimatedTask ChooseTask(EstimatedWorkflow workflow, List<IEnumerable<ActiveEstimatedTask>> tasks)
        {
            return tasks.OrderByDescending(t =>
            {
                var parent = workflow.Tasks.Single(wt => wt.Id == t.First().Id);
                var depsWaitingTime = parent.RequiresDependencies.Count == 0 ? 0 : parent.RequiresDependencies.Max(d => d.ScheduledInstance.Estimation.LaunchTime + d.ScheduledInstance.Estimation.Result.Time);
                var nodesWaitingTime = t.First().Estimation.NodesTimings.Max(n => n.GetAvailabilityTime());
                var waitingTime = Math.Max(depsWaitingTime, nodesWaitingTime);
                return waitingTime + t.First().Estimation.Result.Time;
            }).First().First();
        }
    }

    [Heuristics.HeuristicsName("Sufferage")]
    [System.ComponentModel.Composition.Export(typeof(ITaskBasedHeuristics))]
    public class ESuffHeuristics : ITaskBasedHeuristics
    {

        public ActiveEstimatedTask ChooseTask(EstimatedWorkflow workflow, List<IEnumerable<ActiveEstimatedTask>> tasks)
        {
            return tasks.OrderByDescending(t =>
                GetTime(workflow, t.ElementAt(1)) - GetTime(workflow, t.ElementAt(0))).First().First();
        }

        private static double GetTime(EstimatedWorkflow workflow, ActiveEstimatedTask t)
        {
            var parent = workflow.Tasks.Single(wt => wt.Id == t.Id);
            var depsWaitingTime = parent.RequiresDependencies.Count == 0 ? 0 : parent.RequiresDependencies.Max(d => d.ScheduledInstance.Estimation.LaunchTime + d.ScheduledInstance.Estimation.Result.Time);
            var nodesWaitingTime = t.Estimation.NodesTimings.Max(n => n.GetAvailabilityTime());
            var waitingTime = Math.Max(depsWaitingTime, nodesWaitingTime);
            return waitingTime + t.Estimation.Result.Time;
        }
    }

    [Heuristics.HeuristicsName("Stub")]
    [System.ComponentModel.Composition.Export(typeof(ITaskBasedHeuristics))]
    public class EStubHeuristics : ITaskBasedHeuristics
    {
        private static Random r = new Random(DateTime.Now.Millisecond);

        public ActiveEstimatedTask ChooseTask(EstimatedWorkflow workflow, List<IEnumerable<ActiveEstimatedTask>> tasks)
        {
            /*var seq = tasks[0];
            int idx;
            idx = (int)taskPosition % seq.Count();
            taskPosition++;
            return seq.ElementAt(idx);
            return tasks[0].First();*/
            var a = tasks[r.Next(tasks.Count)].ToArray();
            return a[r.Next(a.Length)];
        }
    }

    public interface IWFBasedUrgentHeuristics
    {
        UrgentPlan MakePlan(EstimatedUrgentWorkflow workflow);
    }

    [DataContract]
    [KnownType(typeof(LaunchPlan))]
    public class UrgentPlan : LaunchPlan, ICloneable
    {
        internal readonly EstimatedUrgentWorkflow wf;

        public bool HasAbortedTasks
        {
            get
            {
                return Plan.Any(t => t.State == TaskScheduler.TaskState.ABORTED);
            }
        }

        internal List<IEnumerable<ActiveEstimatedTask>> Estimations
        {
            get;
            set;
        }

        internal TaskScheduler.NodeAvailabilityTime[] Wrappers
        {
            get;
            set;
        }

        internal virtual bool IgnoreNonUrgentTasks
        {
            get
            {
                return true;
            }
        }

        internal UrgentPlan()
        {
        }

        public UrgentPlan(EstimatedUrgentWorkflow workflow)
        {
            wf = workflow;
            Reset();
        }

        public void Reset()
        {
            foreach (var t in wf.Tasks)
            {
                t.ScheduledInstance = null;
            }
            Plan.Clear();
            Wrappers = TaskScheduler.GenerateWrappers(wf);
            Estimations = TaskScheduler.MakeOverallEstimations(wf, Wrappers);
            if (!IgnoreNonUrgentTasks)
            {
                var additions = new Dictionary<int, List<ActiveEstimatedTask>>();
                for (var i = 0; i < Estimations.Count; i++)
                {
                    var ests = Estimations[i];
                    var busyEsts = ests.Where(e =>
                    {
                        var busyNodes = e.Estimation.NodesTimings.Where(n => n.LaunchedTask != null).ToArray();
                        if (!busyNodes.Any())
                        {
                            return false;
                        }
                        var maxUrgentTime = busyNodes.Max(n => n.LaunchedTask.IsUrgent ? n.LaunchedTask.Estimation.Result.Time : 0);
                        return busyNodes.Any(n => !n.LaunchedTask.IsUrgent && n.LaunchedTask.Estimation.Result.Time > maxUrgentTime);
                    });
                    foreach (var e in busyEsts.ToArray())
                    {
                        additions.GetOrCreateValue(i, () => new List<ActiveEstimatedTask>()).Add(new ActiveEstimatedTask(e) { IgnoreNonUrgent = false });
                    }
                }
                foreach (var p in additions)
                {
                    (Estimations[p.Key] as List<ActiveEstimatedTask>).AddRange(p.Value);
                }
            }
        }

        public override object Clone()
        {
            var result = new UrgentPlan();
            result.EstimatedExecutionTime = EstimatedExecutionTime;
            result.NodesTimings = NodesTimings == null ? null : NodesTimings.Select(nt => nt.Clone() as TaskScheduler.NodeAvailabilityTime).ToList();
            result.Plan = Plan == null ? null : Plan.Select(t => t.Clone() as ActiveEstimatedTask).ToList();
            result.Wrappers = Wrappers == null ? null : Wrappers.Select(w => w.Clone() as TaskScheduler.NodeAvailabilityTime).ToArray();
            result.Estimations = Estimations;
            return result;
        }
    }

    internal class TunablePlan : UrgentPlan
    {
        internal Dictionary<ulong, int> optionsIdxs;
        internal Dictionary<ulong, int> baseValues;

        private int currentIndex = 0, maxIndex = 0;
        private ulong[] leaves, roots;

        public bool TrueForward(bool doRoots)
        {
            var numbers = doRoots ? roots : leaves;
            var i = 0;
            while (i < baseValues.Count && (this[numbers[i]] >= currentIndex || this[numbers[i]] >= baseValues[numbers[i]] - 1))
            {
                i++;
            }
            if (i == baseValues.Count)
            {
                currentIndex++;
                //return currentIndex < maxIndex;
                if (currentIndex < maxIndex)
                {
                    this[numbers[0]] = Math.Min(baseValues[numbers[0]] - 1, currentIndex);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                this[numbers[i]]++;
                return true;
            }
        }

        public bool TrueBackward(bool doRoots)
        {
            var numbers = doRoots ? roots : leaves;
            if (numbers.All(n => this[n] == 0))
            {
                return false;
            }            
            var sNumber = numbers.Where(n => this[n] != 0).OrderBy(n => this[n]).First();
            this[sNumber]--;
            return true;
        }

        public bool AtFirst(bool doRoots)
        {
            var numbers = doRoots ? roots : leaves;
            return numbers.All(n => this[n] == 0);
        }

        public bool AtLast(bool doRoots)
        {
            var numbers = doRoots ? roots : leaves;
            return numbers.All(n => this[n] == baseValues[n] - 1);
        }

        public int this[ulong index]
        {
            get
            {
                return optionsIdxs[index];
            }

            set
            {
                optionsIdxs[index] = value;
            }
        }

        public TunablePlan(EstimatedUrgentWorkflow workflow)
            : base(workflow)
        {
            roots = workflow.Tasks.OrderBy(t => t.DepthLevel).Select(t => t.Id).ToArray();
            leaves = roots.Reverse().ToArray();
            baseValues = Estimations.ToDictionary(e => e.First().Id, e => e.Count());
            optionsIdxs = baseValues.ToDictionary(p => p.Key, p => 0);
            maxIndex = baseValues.Values.Max();
        }

        private List<int[]> EForward(int[] baseValues)
        {
            return EForward(baseValues.Length - 1, baseValues);
        }

        private List<int[]> EForward(int currentIdx, int[] baseValues)
        {
            if (currentIdx == 0)
            {
                return new List<int[]>() { MakeInts(currentIdx) };
            }
            else
            {
                var current = MakeInts(currentIdx);

                return null;
            }
        }

        private void FForward(ref int position, int[] baseValues, int[] result)
        {
            if (position == 0)
            {
                return;
            }
        }

        private static int[] MakeInts(int idx)
        {
            var result = Enumerable.Repeat(0, idx).ToArray();
            for (var i = 1; i < idx; i++)
            {
                result[i] = i;
            }
            return result;
        }

        public bool Forward()
        {
            if (optionsIdxs.Count == 0 || optionsIdxs.All(p => p.Value == baseValues[p.Key] - 1))
            {
                return false;
            }
            optionsIdxs[baseValues.Keys.First()]++;
            var e = baseValues.Keys.GetEnumerator();
            e.MoveNext();
            while (optionsIdxs[e.Current] >= baseValues[e.Current])
            {
                optionsIdxs[e.Current] = 0;
                if (e.MoveNext())
                {
                    optionsIdxs[e.Current]++;
                }
                else
                {
                    break;
                }
            }
            return true;
        }

        public bool Backward()
        {
            if (optionsIdxs.Count == 0 || optionsIdxs.All(p => p.Value == 0))
            {
                return false;
            }
            optionsIdxs[optionsIdxs.Keys.First()]--;
            var e = optionsIdxs.Keys.GetEnumerator();
            while (optionsIdxs[e.Current] < 0)
            {
                optionsIdxs[e.Current] = baseValues[e.Current] - 1;
                if (e.MoveNext())
                {
                    optionsIdxs[e.Current]--;
                }
                else
                {
                    break;
                }
            }
            return true;
        }

        public override string ToString()
        {
            var result = "";
            var i = 0;
            foreach (var val in optionsIdxs.Values)
            {
                if (i++ > 0)
                {
                    result += " ";
                }
                result += val.ToString();
            }
            return result;
        }
    }

    [Heuristics.HeuristicsName("UGreedy")]
    [System.ComponentModel.Composition.Export(typeof(IWFBasedUrgentHeuristics))]
    public class GreedyHeuristics : IWFBasedUrgentHeuristics
    {   
        internal virtual UrgentPlan CreateEmptyPlan(EstimatedUrgentWorkflow workflow)
        {
            return new UrgentPlan(workflow);
        }

        internal virtual void Reorder(UrgentPlan plan)
        {
            var roots = plan.wf.Tasks.Where(t => !t.IsScheduled && t.IsReady).ToArray();
            var busyWrappers = plan.Wrappers.Where(w => w.GetAvailabilityTime(plan.IgnoreNonUrgentTasks) > TaskScheduler.E);
            var freeWrappers = plan.Wrappers.Except(busyWrappers);
            var idxs = new Dictionary<ulong, int>();
            var currentEsts = new List<IEnumerable<ActiveEstimatedTask>>();
            for (var i = 0; i < plan.Estimations.Count; i++)
            {
                if (plan.Estimations[i].Count() > 1)
                {
                    var first = plan.Estimations[i].First();
                    if (roots.Any(r => r.Id == first.Id))
                    {
                        idxs[first.Id] = i;
                        currentEsts.Add(plan.Estimations[i]);
                    }
                }
            }
            foreach (var es in currentEsts)
            {
                var id = es.First().Id;
                plan.Estimations[idxs[id]] = es.OrderBy(e => PrimaryComparisonLambda()(e, plan.wf)).ToList();
            }
            /*for (var i = 0; i < plan.Estimations.Count; i++)
            {
                if (plan.Estimations[i].Count() > 1)
                {                    
                    //plan.Estimations[i] = plan.Estimations[i].OrderBy(e => PrimaryComparisonLambda()(e, plan.wf)).ToList();

                    //var estimationsOnFreeNodes = plan.Estimations[i].Where(e => e.Estimation.Destination.NodeNames.All(n => freeWrappers.Any(fw => fw.ResourceName == e.Estimation.Destination.ResourceName && fw.NodeName == n))).OrderBy(e => PrimaryComparisonLambda()(e, plan.wf));
                    //var otherEstimations = plan.Estimations[i].Except(estimationsOnFreeNodes).OrderBy(e => PrimaryComparisonLambda()(e, plan.wf));
                    //plan.Estimations[i] = new List<ActiveEstimatedTask>();
                    //((List<ActiveEstimatedTask>)plan.Estimations[i]).AddRange(estimationsOnFreeNodes);
                    //((List<ActiveEstimatedTask>)plan.Estimations[i]).AddRange(otherEstimations);
                }
            }*/
        }

        protected double FullTaskTime(ActiveEstimatedTask task, EstimatedUrgentWorkflow workflow)
        {
            var parent = workflow.Tasks.Single(wt => wt.Id == task.Id);
            var depsWaitingTime = parent.RequiresDependencies.Count == 0
                                      ? 0
                                      : parent.RequiresDependencies.Max(
                                          d =>
                                          d.ScheduledInstance.Estimation.LaunchTime +
                                          d.ScheduledInstance.Estimation.Result.Time);
            var nodesWaitingTime = GetNodesAvailabilityTimeForUrgentTask(task, workflow); //t.First().Estimation.NodesTimings.Max(n => n.AvailabilityTime);
            var waitingTime = Math.Max(depsWaitingTime, nodesWaitingTime);
            return waitingTime + task.Estimation.Result.Time;
        }

        protected virtual Func<ActiveEstimatedTask, EstimatedUrgentWorkflow, double> PrimaryComparisonLambda()
        {
            //return t => t.Estimation.NodesTimings.Max(n => n.AvailabilityTime) + t.Estimation.Result.Time;
            //return (t, workflow) => GetNodesAvailabilityTimeForUrgentTask(t, workflow) + t.Estimation.Result.Time;
            return (t, workflow) => FullTaskTime(t, workflow);
        }

        protected virtual Func<EstimatedUrgentWorkflow, IEnumerable<ActiveEstimatedTask>, double> SecondaryComparisonLambda()
        {
            return (workflow, t) =>
            {
                /*var parent = workflow.Tasks.Single(wt => wt.Id == t.First().Id);
                var depsWaitingTime = parent.RequiresDependencies.Count == 0
                                          ? 0
                                          : parent.RequiresDependencies.Max(
                                              d =>
                                              d.ScheduledInstance.Estimation.LaunchTime +
                                              d.ScheduledInstance.Estimation.Result.Time);
                var nodesWaitingTime = GetNodesAvailabilityTimeForUrgentTask(t.First(), workflow); //t.First().Estimation.NodesTimings.Max(n => n.AvailabilityTime);
                var waitingTime = Math.Max(depsWaitingTime, nodesWaitingTime);
                return waitingTime + t.First().Estimation.Result.Time;*/
                return FullTaskTime(t.First(), workflow);
            };
        }

        internal virtual IEnumerable<ActiveEstimatedTask> ChooseSequence(EstimatedUrgentWorkflow workflow, UrgentPlan plan)
        {
            var rootIds = workflow.Tasks.Where(t => !t.IsScheduled && t.IsReady).Select(r => r.Id);
            var effectiveEstimations = plan.Estimations.Where(e => rootIds.Contains(e.First().Id)).ToList();
            var seq = effectiveEstimations.OrderBy(t => SecondaryComparisonLambda()(workflow, t)).First();
            plan.Estimations.Remove(seq);
            return seq;
        }

        internal virtual ActiveEstimatedTask ChooseTask(EstimatedUrgentWorkflow workflow, UrgentPlan plan)
        {
            var seq = ChooseSequence(workflow, plan);
            var prevInstance = ExtractTask(seq, plan);
            var parent = workflow.Tasks.Single(t => t.Id == prevInstance.Id);
            var depsWaitingTime = parent.RequiresDependencies.Count == 0 ? 0 : parent.RequiresDependencies.Max(d => d.ScheduledInstance.Estimation.LaunchTime + d.ScheduledInstance.Estimation.Result.Time);
            prevInstance.Estimation.LaunchTime = Math.Max(depsWaitingTime, GetNodesAvailabilityTimeForUrgentTask(prevInstance, workflow));
            prevInstance.State = prevInstance.Estimation.LaunchTime > TaskScheduler.E || prevInstance.Estimation.NodesTimings.Any(w => w.GetAvailabilityTime(plan.IgnoreNonUrgentTasks) > TaskScheduler.E) ? TaskScheduler.TaskState.SCHEDULED : TaskScheduler.TaskState.LAUNCHED;
            var clonedInstance = new ActiveEstimatedTask(prevInstance) { IsUrgent = true };
            foreach (var node in prevInstance.Estimation.NodesTimings)
            {
                node.AssignTask(clonedInstance);
            }
            
            plan.Plan.Add(clonedInstance);
            parent.ScheduledInstance = clonedInstance;
            return clonedInstance;
        }

        internal virtual ActiveEstimatedTask ExtractTask(IEnumerable<ActiveEstimatedTask> sequence, UrgentPlan plan)
        {
            return sequence.First();//new ActiveEstimatedTask(sequence.First());
        }

        public virtual UrgentPlan MakeInitialPlan(EstimatedUrgentWorkflow workflow)
        {
            workflow.UpdateDependencies();
            return GeneratePlan(workflow, null);
        }

        internal virtual UrgentPlan GeneratePlan(EstimatedUrgentWorkflow workflow, UrgentPlan plan)
        {
            var result = plan ?? CreateEmptyPlan(workflow);
            while (workflow.Tasks.Any(t => !t.IsScheduled && t.IsReady))
            {
                Reorder(result);
                ChooseTask(workflow, result);
            }
            result.NodesTimings = result.Wrappers.ToList();
            result.EstimatedExecutionTime = result.Plan.Any() ? result.Plan.Max(t => t.Estimation.LaunchTime + t.Estimation.Result.Time) : 0;
            Debug.Assert(workflow.Tasks.All(t => t.IsScheduled));
            foreach (var t in workflow.ActiveTasks.Where(t => !t.IsUrgent && t.State == TaskScheduler.TaskState.LAUNCHED))
            {
                if (result.Plan.Any(p => p.IsUrgent && p.State == TaskScheduler.TaskState.LAUNCHED && p.Estimation.Destination.IsLike(t.Estimation.Destination)))
                {
                    result.Plan.Add(new ActiveEstimatedTask(t)
                    {
                        State = TaskScheduler.TaskState.ABORTED
                    });
                }
            }
            return result;
        }

        protected virtual double GetNodesAvailabilityTimeForUrgentTask(ActiveEstimatedTask task, EstimatedUrgentWorkflow workflow)
        {
            return task.Estimation.NodesTimings.Max(n =>
                {
                    var lTask = n.LaunchedTask;
                    var result = lTask != null && (lTask.IsUrgent || !task.IgnoreNonUrgent) ? lTask.Estimation.Result.Time : 0;
                    var td = new LaunchDestination()
                    {
                        ResourceName = n.ResourceName,
                        NodeNames = new[] { n.NodeName }
                    };
                    result += workflow.ActiveTasks.Where(t => t.IsUrgent && t.State == TaskScheduler.TaskState.SCHEDULED && t.IsOlderThan(task) && t.Estimation.Destination.IsLike(td)).Sum(t => t.Estimation.Result.Time);
                    return result;
                }
            );
        }

        public virtual UrgentPlan MakePlan(EstimatedUrgentWorkflow workflow)
        {
            return MakeInitialPlan(workflow);
        }
    }

    [Heuristics.HeuristicsName("UBestFirst")]
    [System.ComponentModel.Composition.Export(typeof(IWFBasedUrgentHeuristics))]
    public class BestFirstHeuristics : GreedyHeuristics
    {
        public override UrgentPlan MakePlan(EstimatedUrgentWorkflow workflow)
        {
            var plan = MakeInitialPlan(workflow) as TunablePlan;
            UrgentPlan subopt = null;
            do
            {
                if (!plan.AtFirst(DoRoots))
                {
                    plan.Reset();
                    GeneratePlan(workflow, plan);
                }

                var shouldUpdate = false;
                var shouldMove = false;
                var trigger = false;

                if (subopt == null)
                {
                    trigger = true;
                    shouldUpdate = true;
                    shouldMove = true;
                }

                if (!trigger && workflow.MinExecutionTime < TaskScheduler.E) //ASAP
                {
                    trigger = true;
                }

                if (!trigger && workflow.MinExecutionTime >= TaskScheduler.E) //should we count MinExTime at all?
                {
                    if (workflow.MinExecutionTime - plan.EstimatedExecutionTime >= TaskScheduler.E) //the time grows, and as long as current time is beyond the lower bound,
                                                                                                    //we choose to increase it
                    {
                        trigger = true;
                        shouldUpdate = true;
                        shouldMove = true;
                    }
                }

                if (!trigger && workflow.MaxExecutionTime >= TaskScheduler.E) //the time grows, and if the MaxExTime == 0 then we can safely stop without
                                                                                 //further increasing it
                {
                    if (workflow.MaxExecutionTime - plan.EstimatedExecutionTime >= TaskScheduler.E) //is it within bounds?
                    {
                        trigger = true;
                        if (!plan.HasAbortedTasks || subopt.HasAbortedTasks) //if current plan is the same or better in terms of terminating tasks
                        {
                            shouldUpdate = true;                            
                        }
                        shouldMove = true;
                    }
                }

                if (shouldUpdate)
                {
                    subopt = plan.Clone() as UrgentPlan;
                }
                if (!shouldMove)
                {
                    break;
                }
            }
            while (plan.TrueForward(DoRoots));

            return subopt;
        }

        internal override UrgentPlan CreateEmptyPlan(EstimatedUrgentWorkflow workflow)
        {
            return new TunablePlan(workflow);
        }

        internal override ActiveEstimatedTask ExtractTask(IEnumerable<ActiveEstimatedTask> sequence, UrgentPlan plan)
        {
            return sequence.Skip((plan as TunablePlan)[sequence.First().Id]).First();//new ActiveEstimatedTask(sequence.Skip((plan as TunablePlan)[sequence.First().Id]).First());
        }

        internal virtual bool DoRoots
        {
            get
            {
                return false;
            }
        }
    }

    internal class FreeFirstPlan : TunablePlan
    {
        public FreeFirstPlan(EstimatedUrgentWorkflow workflow)
            : base(workflow)
        {
        }

        internal override bool IgnoreNonUrgentTasks
        {
	        get 
	        {
                return false;
	        }
        }
    }

    [Scheduler.Heuristics.HeuristicsName("UBestFreeFirst")]
    [System.ComponentModel.Composition.Export(typeof(IWFBasedUrgentHeuristics))]
    public class BestFreeFirstHeuristics : BestFirstHeuristics
    {
        internal override UrgentPlan CreateEmptyPlan(EstimatedUrgentWorkflow workflow)
        {
            return new FreeFirstPlan(workflow);
        }
    }

    [Scheduler.Heuristics.HeuristicsName("UBusiestFirst")]
    [System.ComponentModel.Composition.Export(typeof(IWFBasedUrgentHeuristics))]
    public class BusiestFirstHeuristics : BestFirstHeuristics
    {
        public override UrgentPlan MakePlan(EstimatedUrgentWorkflow workflow)
        {
            var plan = MakeInitialPlan(workflow) as TunablePlan;
            UrgentPlan subopt = null;
            do
            {
                //the following is just to avoid single meaningless execution of that block
                if (!plan.AtFirst(DoRoots))
                {
                    plan.Reset();
                    GeneratePlan(workflow, plan);
                }

                var shouldUpdate = false;
                var shouldMove = false;
                var trigger = false;

                if (subopt == null)
                {
                    shouldUpdate = true;
                    shouldMove = true;
                    trigger = true;
                }

                if (!trigger && workflow.MinExecutionTime < TaskScheduler.E) //ASAP
                {
                    return new GreedyHeuristics().MakePlan(workflow);
                }

                if (!trigger && workflow.MaxExecutionTime >= TaskScheduler.E && workflow.MaxExecutionTime - plan.EstimatedExecutionTime < TaskScheduler.E) //current time is above the higher bound
                {
                    trigger = true;
                    shouldUpdate = true;
                    shouldMove = true;
                }

                if (!trigger && workflow.MinExecutionTime >= TaskScheduler.E) //should we count MinExTime at all?
                {
                    if (plan.EstimatedExecutionTime - workflow.MinExecutionTime >= TaskScheduler.E) //is current plan above the lower bound?
                    {
                        if (!plan.HasAbortedTasks || subopt.HasAbortedTasks) //if current plan is the same or better in terms of terminating tasks
                        {
                            shouldUpdate = true;
                        }
                        shouldMove = true;
                    }
                }
                
                if (shouldUpdate)
                {
                    subopt = plan.Clone() as UrgentPlan;
                }
                if (!shouldMove)
                {
                    break;
                }
            }
            while (plan.TrueForward(DoRoots));

            return subopt;
        }

        internal override void Reorder(UrgentPlan plan)
        {
            /*//for (var i = 0; i < plan.Estimations.Count; i++)
            //{
            //    plan.Estimations[i] = plan.Estimations[i].OrderByDescending(t => PrimaryComparisonLambda()(t));
            //}
            var busyWrappers = plan.Wrappers.Where(w => w.GetAvailabilityTime(plan.IgnoreNonUrgentTasks) > TaskScheduler.E);
            var freeWrappers = plan.Wrappers.Except(busyWrappers);
            for (var i = 0; i < plan.Estimations.Count; i++)
            {
                if (plan.Estimations[i].Count() > 1)
                {
                    plan.Estimations[i] = plan.Estimations[i].OrderBy(e => PrimaryComparisonLambda()(e, plan.wf)).ToList();
                    //var estimationsOnFreeNodes = plan.Estimations[i].Where(e => e.Estimation.Destination.NodeNames.All(n => freeWrappers.Any(fw => fw.ResourceName == e.Estimation.Destination.ResourceName && fw.NodeName == n))).OrderByDescending(e => PrimaryComparisonLambda()(e, plan.wf));
                    //var otherEstimations = plan.Estimations[i].Except(estimationsOnFreeNodes).OrderByDescending(e => PrimaryComparisonLambda()(e, plan.wf));
                    //plan.Estimations[i] = new List<ActiveEstimatedTask>();
                    //((List<ActiveEstimatedTask>)plan.Estimations[i]).AddRange(estimationsOnFreeNodes);
                    //((List<ActiveEstimatedTask>)plan.Estimations[i]).AddRange(otherEstimations);
                }
            }*/

            var roots = plan.wf.Tasks.Where(t => !t.IsScheduled && t.IsReady).ToArray();
            var busyWrappers = plan.Wrappers.Where(w => w.GetAvailabilityTime(plan.IgnoreNonUrgentTasks) > TaskScheduler.E);
            var freeWrappers = plan.Wrappers.Except(busyWrappers);
            var idxs = new Dictionary<ulong, int>();
            var currentEsts = new List<IEnumerable<ActiveEstimatedTask>>();
            for (var i = 0; i < plan.Estimations.Count; i++)
            {
                if (plan.Estimations[i].Count() > 1)
                {
                    var first = plan.Estimations[i].First();
                    if (roots.Any(r => r.Id == first.Id))
                    {
                        idxs[first.Id] = i;
                        currentEsts.Add(plan.Estimations[i]);
                    }
                }
            }
            foreach (var es in currentEsts)
            {
                var id = es.First().Id;
                plan.Estimations[idxs[id]] = es.OrderByDescending(e => PrimaryComparisonLambda()(e, plan.wf)).ToList();
            }
        }

        internal override IEnumerable<ActiveEstimatedTask> ChooseSequence(EstimatedUrgentWorkflow workflow, UrgentPlan plan)
        {
            var rootIds = workflow.Tasks.Where(t => !t.IsScheduled && t.IsReady).Select(r => r.Id);
            var effectiveEstimations = plan.Estimations.Where(e => rootIds.Contains(e.First().Id)).ToList();
            var seq = effectiveEstimations.OrderByDescending(t => SecondaryComparisonLambda()(workflow, t)).First();
            plan.Estimations.Remove(seq);
            return seq;
        }

        internal override bool DoRoots
        {
            get
            {
                return true;
            }
        }
    }

    [Scheduler.Heuristics.HeuristicsName("UBusiestFreeFirst")]
    [System.ComponentModel.Composition.Export(typeof(IWFBasedUrgentHeuristics))]
    public class BusiestFreeFirstHeuristics : BusiestFirstHeuristics
    {
        internal override UrgentPlan CreateEmptyPlan(EstimatedUrgentWorkflow workflow)
        {
            return new FreeFirstPlan(workflow);
        }
    }
}