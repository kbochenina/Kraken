using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace MITP
{
    [DataContract]
    public enum TaskTimeMetric
    {
        [EnumMember] Postponed, // Defined, but not ready to execute
        [EnumMember] Queued,             
        [EnumMember] Brokering,
        [EnumMember] Calculation,
    }

    [DataContract]
    public enum TaskTimeOverheads
    {
        [EnumMember] WaitInQueue,

        [EnumMember] PackageBase,
        [EnumMember] Estimation,
        [EnumMember] Scheduler,        
        [EnumMember] Provider,         
        [EnumMember] InputFilesCopy,
        [EnumMember] OutputFilesCopy,

        [EnumMember] Other,
        [EnumMember] All,
    }

    [DataContract]
    public class TaskTimeMeasurement
    {
        private readonly TimeSpan DURATION_UPDATE_INTERVAL = TimeSpan.FromSeconds(0.2);
        private DateTime? _lastDurationUpdateTime = null;

        [DataMember] public IDictionary<TaskTimeMetric, DateTime> WhenStarted  { get; private set; }
        [DataMember] public IDictionary<TaskTimeMetric, DateTime> WhenFinished { get; private set; }

        private Dictionary<TaskTimeOverheads, TimeSpan> _overheads { get; set; }
        //private Dictionary<TaskTimeOverheads, TimeSpan> _overheadsMax { get; set; }
        private Dictionary<TaskTimeOverheads, int> _overheadsActionCount { get; set; }

        [DataMember]
        public Dictionary<string, TimeSpan> OverheadsSpecial { get; private set; }

        [DataMember]
        public IDictionary<TaskTimeOverheads, TimeSpan> OverheadAverages
        {
            get
            {
                var overs = OverheadTotals;
                var res = new Dictionary<TaskTimeOverheads, TimeSpan>();

                foreach (var overType in overs.Keys)
                {
                    if (_overheadsActionCount[overType] == 0)
                    {
                        res[overType] = overs[overType]; // m.b. do not set at all?
                    }
                    else
                    {
                        res[overType] = TimeSpan.FromTicks(
                            overs[overType].Ticks / _overheadsActionCount[overType]
                            //(long) (overs[overType].Ticks / (1.0 * _overheadsActionCount[overType]))
                        );
                    }
                }

                return res;
            }

            private set { }  // Property is evaluated, not set. Setter needed for deserialization.
        }

        [DataMember]
        public IDictionary<TaskTimeOverheads, TimeSpan> OverheadTotals 
        {
            get
            {
                var duration = Duration;
                var allOverheadsSum = 
                      duration[TaskTimeMetric.Queued] 
                    + duration[TaskTimeMetric.Brokering] 
                    - duration[TaskTimeMetric.Calculation];

                var namedOverheads = _overheads
                    .Where(pair => pair.Key != TaskTimeOverheads.All && pair.Key != TaskTimeOverheads.Other)
                    .Select(pair => pair.Value);
                var namedOverheadsSum = TimeSpan.FromSeconds(0);
                foreach (var over in namedOverheads)
                    namedOverheadsSum += over;

                var otherOverheadsSum = allOverheadsSum - namedOverheadsSum;

                var res = new Dictionary<TaskTimeOverheads, TimeSpan>(_overheads);
                res[TaskTimeOverheads.All] = allOverheadsSum;
                res[TaskTimeOverheads.Other] = otherOverheadsSum;

                return res;
            }

            private set { }  // Property is evaluated, not set. Setter needed for deserialization.
        } 

        public void AddToOverheads(TaskTimeOverheads overheadsName, TimeSpan overheadsValue)
        {
            if (overheadsName == TaskTimeOverheads.All || overheadsName == TaskTimeOverheads.Other)
                throw new ArgumentOutOfRangeException("overheadsName");

            _overheads[overheadsName] += (overheadsValue.Ticks >= 0)? overheadsValue: TimeSpan.Zero;
            _overheadsActionCount[overheadsName] += 1;
        }

        public void AddToOverheads(TaskTimeOverheads overheadsName, Action action)
        {
            var actionStarted = DateTime.Now;
            action();
            var actionFinished = DateTime.Now;

            AddToOverheads(overheadsName, actionFinished - actionStarted);
        }

        [DataMember] 
        public IDictionary<TaskTimeMetric, TimeSpan> Duration 
        {
            get
            {
                // To sync time values in duration and overheads during serialization:
                if (_lastDurationUpdateTime == null || DateTime.Now > _lastDurationUpdateTime + DURATION_UPDATE_INTERVAL)
                    _lastDurationUpdateTime = DateTime.Now;

                var now = _lastDurationUpdateTime.Value;
                var duration = new Dictionary<TaskTimeMetric, TimeSpan>();

                foreach (var metric in Enum.GetValues(typeof(TaskTimeMetric)))
                {
                    duration[(TaskTimeMetric) metric] = TimeSpan.Zero;
                }

                foreach (var timePoint in WhenStarted.Keys)
                {
                    var started  = WhenStarted[timePoint];
                    var finished = WhenFinished.ContainsKey(timePoint)? WhenFinished[timePoint]: now;

                    duration[timePoint] = finished - started;
                }

                return duration;
            }

            private set { }  // Property is evaluated, not set. Setter needed for deserialization.
        }

        /*
        [DataMember]
        public DateTime? EstimatedStart { get; set; }     

        [DataMember]
        public DateTime? EstimatedFinish { get; set; }

        [DataMember]
        public TimeSpan? Left
        {
            get
            {
                if (EstimatedFinish == null || EstimatedStart == null)
                    return null;

                if (!WhenStarted.ContainsKey(TaskTimeMetric.Calculation))
                    return EstimatedFinish - DateTime.Now;

                if (WhenFinished.ContainsKey(TaskTimeMetric.Calculation))
                    return TimeSpan.FromSeconds(0);
                
                TimeSpan duration = EstimatedFinish.Value - EstimatedStart.Value;
                TimeSpan passed = DateTime.Now - WhenStarted[TaskTimeMetric.Calculation];
                TimeSpan left = duration - passed;
                return (left.TotalMilliseconds < 0) ? TimeSpan.FromSeconds(0) : left;
            }

            private set { }  // Property is evaluated, not set. Setter needed for deserialization.
        }
        */

        private void RecalcWaitInQueueOverhead()
        {    
            if (!WhenStarted.ContainsKey(TaskTimeMetric.Queued) || WhenStarted[TaskTimeMetric.Queued] == null)
            {
                _overheads[TaskTimeOverheads.WaitInQueue] = TimeSpan.Zero;
            }
            else
            {
                //TimeSpan queuedDuration = DateTime.Now - WhenStarted[TaskTimeMetric.Queued];
                //if (WhenFinished.ContainsKey(TaskTimeMetric.Queued) && WhenFinished[TaskTimeMetric.Queued] != null)
                //    queuedDuration = WhenFinished[TaskTimeMetric.Queued] - WhenStarted[TaskTimeMetric.Queued];

                TimeSpan oversWhileInQueue = _overheads
                    .Where(o => !(new[] {TaskTimeOverheads.All, TaskTimeOverheads.Other, TaskTimeOverheads.WaitInQueue}).Contains(o.Key) )
                    .Select(pair => pair.Value).Aggregate(TimeSpan.Zero, (acc, cur) => acc + cur);
                _overheads[TaskTimeOverheads.WaitInQueue] = Duration[TaskTimeMetric.Queued] - oversWhileInQueue;
            }
        }

        public void Started(TaskTimeMetric metric)
        {
            WhenStarted[metric] = DateTime.Now;
        }

        public void Finished(TaskTimeMetric metric)
        {
            WhenFinished[metric] = DateTime.Now;

            if (metric == TaskTimeMetric.Queued)
                RecalcWaitInQueueOverhead();
        }

        public void Edge(TaskTimeMetric started, TaskTimeMetric finished)
        {
            var now = DateTime.Now;                                                                
            WhenStarted[started] = now;
            WhenFinished[finished] = now;

            if (finished == TaskTimeMetric.Queued)
                RecalcWaitInQueueOverhead();
        }

        public void EdgeUndo(TaskTimeMetric wronglyStarted, TaskTimeMetric wronglyFinished)
        {
            WhenStarted.Remove(wronglyStarted);
            WhenFinished.Remove(wronglyFinished);

            if (wronglyFinished == TaskTimeMetric.Queued || wronglyStarted == TaskTimeMetric.Queued)
                RecalcWaitInQueueOverhead();
        }

        public TaskTimeMeasurement()
        {
            this._overheadsActionCount = new Dictionary<TaskTimeOverheads, int>();
            this._overheads = new Dictionary<TaskTimeOverheads, TimeSpan>();
            this._overheads.Clear();
            foreach (var expObj in Enum.GetValues(typeof(TaskTimeOverheads)))
            {
                var exp = (TaskTimeOverheads) expObj;

                this._overheadsActionCount[exp] = 0;

                if (exp != TaskTimeOverheads.All && exp != TaskTimeOverheads.Other && exp != TaskTimeOverheads.WaitInQueue)
                    this._overheads[exp] = TimeSpan.Zero;
            }

            this.OverheadsSpecial = new Dictionary<string, TimeSpan>();

            this.WhenStarted  = new Dictionary<TaskTimeMetric, DateTime>();
            this.WhenFinished = new Dictionary<TaskTimeMetric, DateTime>();

            //this.EstimatedStart  = null;
            //this.EstimatedFinish = null;
        }

        public TaskTimeMeasurement(TaskTimeMeasurement other)
            : this()
        {
            if (other._overheads != null)
                this._overheads = new Dictionary<TaskTimeOverheads, TimeSpan>(other._overheads);

            if (other._overheadsActionCount != null)
                this._overheadsActionCount = new Dictionary<TaskTimeOverheads, int>(other._overheadsActionCount);

            if (other.OverheadsSpecial != null)
                this.OverheadsSpecial = new Dictionary<string, TimeSpan>(other.OverheadsSpecial);

            if (other.WhenStarted != null)
                this.WhenStarted = new Dictionary<TaskTimeMetric, DateTime>(other.WhenStarted);

            if (other.WhenFinished != null)
                this.WhenFinished = new Dictionary<TaskTimeMetric, DateTime>(other.WhenFinished);

            /*
            if (other.EstimatedStart != null)
                this.EstimatedStart = other.EstimatedStart;

            if (other.EstimatedFinish != null)
                this.EstimatedFinish = other.EstimatedFinish;
            */
        }
    }
}

