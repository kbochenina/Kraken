using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ControllerFarmService.Statistic;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Attributes;
using PFX = System.Threading.Tasks;

namespace MITP
{
    //[BsonIgnoreExtraElements]
    internal class TaskCache
    {
        [BsonIgnore] private static readonly TimeSpan UPDATE_DURATION_TO_WARN = TimeSpan.FromSeconds(0.3);
        [BsonIgnore] private static readonly TimeSpan UPDATE_INTERVAL = TimeSpan.FromMilliseconds(200);

        [BsonIgnore] private static Dictionary<ulong, TaskCache> _dumpedTasks = new Dictionary<ulong,TaskCache>();

        [BsonIgnore] private static readonly Dictionary<ulong, TaskCache> _cache = new Dictionary<ulong, TaskCache>();
        [BsonIgnore] private static readonly object _globalLock = new object();
        [BsonIgnore] private static readonly object _dumpedLock = new object();

        [BsonIgnore] public /*volatile*/ readonly object StateLock; // inits in constructor

        [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
        public ObjectId _id { get; set; }

        public TaskRunContext Context { get; private set; }

        [BsonElement] private TaskStateInfo _stateInfo; //todo : BsonElement("StateInfo")
        [BsonIgnore]  public  TaskStateInfo StateInfo { get { return _stateInfo; } }  // mutable by setter func

        [BsonIgnore] private bool _isUpdating { get; set; }                          // mutable
        [BsonIgnore] private DateTime _lastUpdateTime { get; set; }                  // mutable

        //todo nbutakov change
        [BsonIgnore] private ITaskGlobalCacheCollector gcCollector;

        private TaskCache(TaskRunContext context, TaskStateInfo state, ITaskGlobalCacheCollector collector = null)
        {
            lock (_globalLock)
            {
                StateLock = new object(); // needs to be explicitly before SetState, which triggers Save (i.e. makes object publicly available in memory)

                _isUpdating = false;
                _lastUpdateTime = DateTime.Now - UPDATE_INTERVAL - TimeSpan.FromMilliseconds(50);

                Context = context;
            }

            SetState(state);
            gcCollector = collector;
        }

        public void SetState(TaskStateInfo newStateInfo)
        {
            lock (this.StateLock)
            {
                if (_stateInfo != null && _stateInfo.IsFinished())
                {
                    Log.Warn(String.Format("Tried to change state of finished task {0} to {1} ('{2}')",
                        this.Context.TaskId, newStateInfo.State, newStateInfo.StateComment
                    ));

                    // ignoring changes for finished tasks
                }
                else
                {
                    this._stateInfo = new TaskStateInfo(newStateInfo);
                    this.Save();

                    if (newStateInfo.IsFinished()) // completed on this iteration
                    {
                        var resource = ResourceCache.GetByName(this.Context.Resource.ResourceName);
                        resource.Release(this.Context.NodesConfig);

                        // todo : import to Storage
                    }
                }
            }
        }
        
        public void SetState(TaskState newState, string stateComment = "")
        {
            lock (this.StateLock)
            {
                SetState(new TaskStateInfo(newState, stateComment));
            }
        }

        private void Save()
        {
            lock (_dumpedLock)
            {
                _dumpedTasks[this.Context.TaskId] = this;
            }

            var collection = Mongo.GetCollection<TaskCache>();
            lock (this.StateLock)
            {
                collection.Save(this);
            }
        }

        private static TaskCache LoadTask(ulong taskId)
        {
            var collection = Mongo.GetCollection<TaskCache>();
            var task = collection.AsQueryable<TaskCache>().Where(t => t.Context.TaskId == taskId).SingleOrDefault();

            if (task != null)
                SetControllerForLoadedTask(task);

            return task;
        }

        private static TaskCache[] LoadTasks(IEnumerable<string> resourceNames)
        {
            lock (_dumpedLock)
            {
                var loaded = _dumpedTasks.Values.ToArray();
                //_dumpedTasks.Clear();
                return loaded;
            }

            return new TaskCache[0];


            var collection = Mongo.GetCollection<TaskCache>();
            var tasks = collection.AsQueryable<TaskCache>()
                .Where(t => 
                    t.Context.Resource.ResourceName.In(resourceNames) && 
                    t.StateInfo.State == TaskState.Started
                ).ToArray();

            foreach (var task in tasks)
                SetControllerForLoadedTask(task);

            return tasks;
        }

        public static void DumpAllTasks()
        {
            lock (_globalLock)
            {
                Log.Info("Dumping tasks states");

                lock (_dumpedLock)
                {
                    _dumpedTasks.Clear();
                }

                while (_cache.Any())
                {
                    var elem = _cache.First();
                    lock (elem.Value.StateLock)
                    {
                        elem.Value.Save();
                        _cache.Remove(elem.Key);
                    }
                }
            }
        }

        public static void RestoreTasks(IEnumerable<string> resourceNames)
        {
            //todo create instance of GCCollector
            lock (_globalLock)
            {
                Log.Info("Restoring tasks for resources: " + String.Join(", ", resourceNames));
                var tasks = LoadTasks(resourceNames);

                foreach (var task in tasks)
                {
                    _cache[task.Context.TaskId] = task;

                    if (!task.StateInfo.IsFinished())
                    {
                        var res = ResourceCache.GetByName(task.Context.Resource.ResourceName);
                        res.Acquire(task.Context.NodesConfig);
                    }
                }
            }
        }

        private static void SetControllerForLoadedTask(TaskCache task)
        {
            try
            {
                // DO NOT USE CURRENT CACHED CONTROLLER: could have changed parts from the task's one. Also, controllers should be stateless
                task.Context.Controller = ControllerBuilder.Build(task.Context.Resource);
            }
            catch (Exception buildEx)
            {
                Log.Warn(String.Format("Could not build controller for loaded task {0}: {1}",
                    task.Context.TaskId, buildEx
                ));
            }

            task.Context.Controller = null;
        }

        public static void AddTask(TaskRunContext context, TaskStateInfo state)//, TaskState state = TaskState.Started, string stateComment = "")
        {
            var taskCache = new TaskCache(context, state, CacheCollectorFactory.GetInstance().GetTaskCacheCollector()); // autosaves

            lock (_globalLock)
            {
                _cache[context.TaskId] = taskCache;
            }
        }

        public static TaskCache GetById(ulong taskId)
        {
            TaskCache taskCache;
            lock (_globalLock)
            {
                if (!_cache.ContainsKey(taskId))
                {
                    var task = LoadTask(taskId);

                    if (task != null)
                    {
                        _cache[taskId] = task;
                    }
                    else
                    {
                        Log.Error("No saved state for task " + taskId.ToString());
                        throw new ArgumentException("Unknown task for this farm");
                    }
                }

                taskCache = _cache[taskId];
            }
            return taskCache;
        }

        //public static void UpdateTaskState(TaskCache cache)
        public void UpdateStateAsync()
        {
            //var cache = GetById(taskId);

            // todo : PFX.Task.Run in .Net 4.5
            PFX.Task.Factory.StartNew(() =>
            {
                if (!this._isUpdating && (this._lastUpdateTime + UPDATE_INTERVAL < DateTime.Now || DateTime.Now < this._lastUpdateTime))
                {
                    bool wasFinished = false;
                    bool isThisThreadUpdating = false;
                    lock (this.StateLock)
                    {
                        if (!this._isUpdating && (this._lastUpdateTime + UPDATE_INTERVAL < DateTime.Now || DateTime.Now < this._lastUpdateTime))
                        {
                            this._isUpdating = true;
                            isThisThreadUpdating = true;

                            wasFinished = this.StateInfo.IsFinished();
                        }
                    }

                    if (isThisThreadUpdating)
                    {
                        try
                        {
                            if (!wasFinished)
                            {
                                var timer = System.Diagnostics.Stopwatch.StartNew();

                                var newState = this.Context.Controller.GetTaskStateInfo(this.Context);

                                timer.Stop();
                                if (timer.Elapsed > UPDATE_DURATION_TO_WARN)
                                    Log.Warn(String.Format("Task {0} update took {1} seconds", this.Context.TaskId, timer.Elapsed.TotalSeconds)); // Context.TaskId is immutable

                                SetState(newState);
                                if (gcCollector != null)
                                {
                                    gcCollector.push(this.Context, this.Context.TaskId, newState);
                                }
                               
                            }
                        }
                        catch (Exception e)
                        {
                            SetState(TaskState.Failed, e.Message); // todo : retries

                            Log.Error(String.Format("Exception on update task {0}: {1}", this.Context.TaskId, e)); // Context.TaskId is immutable
                            throw;
                        }
                        finally
                        {
                            lock (this.StateLock)
                            {
                                this._lastUpdateTime = DateTime.Now;
                                this._isUpdating = false;
                            }
                        }
                    }
                }
            });
        }

        public static ulong[] GetActiveTaskIds()
        {
            lock (_globalLock)
            {
                var ids = new List<ulong>();
                foreach (var task in _cache.Values)
                {
                    lock (task.StateLock)
                    {
                        if (task.StateInfo.State == TaskState.Started)
                            ids.Add(task.Context.TaskId);
                    }
                }

                //ulong[] ids = _cache.Where(pair => pair.Value.StateInfo.State == TaskState.Started).Select(pair => pair.Key).ToArray(); // todo : WARNING! UNSAFE!
                ulong[] idsArray = ids.ToArray();
                return idsArray;
            }
        }
    }
}
