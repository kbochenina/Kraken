using System;
using System.Collections.Generic;
using System.Linq;
using CommonDataTypes;
using CommonDataTypes.RExService.Service.Entity.Info;

namespace StatisticsCacheService.Cache
{
    public class TaskCache : CommonCache<Dictionary<ulong, TaskStatInfo>>
    {
        private Dictionary<ulong, Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>>> _data = new Dictionary<ulong, Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>>>();

        private readonly object _readWriteLock = new object();

        private Dictionary<ulong, Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>>> _survivedData;

        public TaskCache() {}

        public TaskCache(TimeSpan lifeTime) : base(lifeTime) { }

        public override void AddAllInfo(Dictionary<ulong, TaskStatInfo> data)
        {
            lock (_readWriteLock)
            {
                foreach (var info in data)
                {
                    if (!_data.ContainsKey(info.Key))
                    {
                        _data.Add(info.Key, ConvertToNativeStruct(info.Value));
                    }
                    else
                    {
                        _data[info.Key] = MergeWithNativeStruct(_data[info.Key], info.Value, info.Key);
                    }
                }
            }
        }

        protected override Dictionary<ulong, TaskStatInfo> GetStartedWith(DateTime date, bool withRemoving)
        {
            lock (_readWriteLock)
            {
                Dictionary<ulong, TaskStatInfo> result = new Dictionary<ulong, TaskStatInfo>();

                Dictionary<ulong, Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>>> survivedData = new Dictionary<ulong, Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>>>();

                foreach (var task in _data)
                {
                    Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>> survivedTuple;
                    var extracted = GetProperValues(task.Value, date, withRemoving, out survivedTuple);
                   
                    if (extracted.ProcessInfoCollection.Count > 0)
                    {
                        result.Add(task.Key, extracted);
                    }
                    
                    
                    if (survivedTuple.Item2.Count > 0)
                    {
                        survivedData.Add(task.Key, survivedTuple);
                    }
                }

                if (withRemoving)
                {
                    _data = survivedData;
                }

                return result;
            }
        }

        public TaskStatInfo GetStartedWith(ulong taskId, DateTime date, bool withRemoving)
        {
            lock (_readWriteLock)
            {
                if (!_data.ContainsKey(taskId))
                {
                    return null;
                }

                Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>> survivedTuple;
                var result = GetProperValues(_data[taskId], date, withRemoving, out survivedTuple);

                if (withRemoving)
                {
                    if (survivedTuple.Item2.Count > 0)
                    {
                        _data[taskId] = survivedTuple;
                    }
                    else
                    {
                        _data.Remove(taskId);
                    }
                }

                return result;
            }
        }
        
        public override Tuple<object,DateTime,DateTime> CollectGarbage()
        {
            lock (_readWriteLock)
            {
                var survivedData = new Dictionary<ulong, Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>>>();

                var deadData = new Dictionary<ulong, TaskStatInfo>();
                foreach (var tuple in _data)
                {
                    foreach (var infosSetPair in tuple.Value.Item2)
                    {
                        var survived = createLeaf(infosSetPair.Value.Where(x => !IsExpired(x)));

                        var dead = createLeaf(infosSetPair.Value.Except(survived)); 

                        //todo optimize it later.
                        if (survived.Count > 0)
                        {
                            if (!survivedData.ContainsKey(tuple.Key))
                            {
                                survivedData.Add(tuple.Key, new Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>>(tuple.Value.Item1, new Dictionary<string, SortedSet<ProcessStatInfo>>()));
                            }
                            survivedData[tuple.Key].Item2.Add(infosSetPair.Key, survived);
                        }

                        if (dead.Count > 0)
                        {
                            if (!deadData.ContainsKey(tuple.Key))
                            {
                                deadData.Add(tuple.Key, new TaskStatInfo(new Dictionary<string, List<ProcessStatInfo>>(), tuple.Value.Item1));
                            }
                            deadData[tuple.Key].ProcessInfoCollection.Add(infosSetPair.Key, dead.ToList());
                        }

                        /*OnElementsExpired(new TaskCacheEventArgs
                        {
                           
                            Id = tuple.Key,
                            ResourceName = tuple.Value.Item1,
                            NodeName = infosSetPair.Key,
                            RemovedElements = dead.Select(x => (object)x).ToList(),
                        }
                       );*/
                    }
                }
                _survivedData = survivedData;
                return FormDataForUtilize(deadData);
            }
        }

        public override void ApplySurvived()
        {
            lock (_readWriteLock)
            {
                _data = _survivedData;
            }
        }

        protected bool IsExpired(ProcessStatInfo x)
        {
            return x.TimeSnapshot.Ticks <= DateTime.Now.Subtract(TimeOfLife).Ticks;
        }

        private Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>> ConvertToNativeStruct(TaskStatInfo data)
        {
            Dictionary<string, SortedSet<ProcessStatInfo>> dat = new Dictionary<string, SortedSet<ProcessStatInfo>>();

            foreach (var info in data.ProcessInfoCollection)
            {
                dat.Add(info.Key, new SortedSet<ProcessStatInfo>(info.Value));
            }

            return new Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>>(data.ResourceName, dat);
        }

        private TaskStatInfo GetProperValues(Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>> data, DateTime date, bool withRemoving,out Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>> survivedTuple)
        {
                Dictionary<string, List<ProcessStatInfo>> result = new Dictionary<string, List<ProcessStatInfo>>();

                Dictionary<string, SortedSet<ProcessStatInfo>> survived = new Dictionary<string, SortedSet<ProcessStatInfo>>();

            foreach (var info in data.Item2)
            {
                var selected = info.Value.TakeWhile(x => x.TimeSnapshot.Ticks >= date.Ticks);

                if (selected.Count() > 0)
                {
                    result.Add(info.Key, new List<ProcessStatInfo>(selected));
                }

                if (withRemoving)
                {
                    var list = createLeaf(info.Value.Except(selected));
                    if (list.Count > 0)
                    {
                        survived.Add(info.Key, list);
                    }
                }

                //Common.Utility.LogDebug(string.Format("TaskCache: '{0}' - Selected count: {1}", info.Key, selected.Count()));
            }

            survivedTuple = new Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>>(data.Item1, survived);

            return new TaskStatInfo(result, data.Item1);
        }

        private Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>> MergeWithNativeStruct(
            Tuple<string, Dictionary<string, SortedSet<ProcessStatInfo>>> oldData, TaskStatInfo data, ulong taskId)
        {
            if (oldData.Item1 != data.ResourceName)
            {
                throw new ArgumentException("resource name for taskId=" + taskId + " isn't equal");
            }

            var storage = oldData.Item2;

            foreach (var info in data.ProcessInfoCollection)
            {
                if (!storage.ContainsKey(info.Key))
                {
                    storage.Add(info.Key, createLeaf(info.Value));
                }
                else
                {
                    storage[info.Key].UnionWith(info.Value);
                }
            }

            return oldData;
        }

        private SortedSet<ProcessStatInfo> createLeaf(IEnumerable<ProcessStatInfo> data = null)
        {
            return data == null ? new SortedSet<ProcessStatInfo>(new ProcessStatInfoComparer()) : new SortedSet<ProcessStatInfo>(data, new ProcessStatInfoComparer());
        }

        protected Tuple<object, DateTime, DateTime> FormDataForUtilize(Dictionary<ulong, TaskStatInfo> data)
        {
            var startEnd = new DateTime[] { DateTime.MaxValue, DateTime.MinValue };

            var result = data.Aggregate(startEnd,
                            (seed, x) =>
                            {
                                return x.Value.ProcessInfoCollection.Aggregate(seed, (seedy, y) =>
                                {
                                    return y.Value.Aggregate(seedy, (seedz, z) =>
                                    {
                                        seedz[0] = seedz[0] > z.TimeSnapshot ? z.TimeSnapshot : seedz[0];
                                        seedz[1] = seedz[1] < z.TimeSnapshot ? z.TimeSnapshot : seedz[1];
                                        return seedz;
                                    });
                                });
                            }
                            );

            return new Tuple<object, DateTime, DateTime>(data, result[0], result[1]);
        }

        private class ProcessStatInfoComparer : IComparer<ProcessStatInfo>
        {
            public int Compare(ProcessStatInfo x, ProcessStatInfo y)
            {
                return -1 * x.TimeSnapshot.CompareTo(y.TimeSnapshot);
            }
        }
    
    }

}
