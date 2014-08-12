using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CommonDataTypes.RExService.Service.Entity.Info;

namespace StatisticsCacheService.Cache
{
    public class ResourceCache : CommonCache<Dictionary<string, Dictionary<string, List<NodeInfo>>>>
    {
        private Dictionary<string, Dictionary<string, SortedSet<NodeInfo>>> _data =
            new Dictionary<string, Dictionary<string, SortedSet<NodeInfo>>>();

        private readonly object _readWriteLock = new object();

        private bool needToRemove = true;

        private Dictionary<string, Dictionary<string, SortedSet<NodeInfo>>> _survivedData;

        public ResourceCache() { }

        public ResourceCache(TimeSpan lifeTime) : base(lifeTime) { }

        protected bool CompareByDate(NodeInfo value, DateTime date)
        {
            return value.TimeSnapshot.CompareTo(date) > 0;
        }

        protected bool IsExpired(NodeInfo leaf)
        {
            return leaf.TimeSnapshot <= DateTime.Now.Subtract(TimeOfLife);
        }

        public override void AddAllInfo(Dictionary<string, Dictionary<string, List<NodeInfo>>> data)
        {
            lock (_readWriteLock)
            {
                foreach (var layer1 in data)
                {
                    if (!_data.ContainsKey(layer1.Key))
                    {
                        _data.Add(layer1.Key, new Dictionary<string, SortedSet<NodeInfo>>());
                    }

                    var nodes = _data[layer1.Key];

                    foreach (var layer2 in layer1.Value)
                    {
                        if (!nodes.ContainsKey(layer2.Key))
                        {
                            nodes.Add(layer2.Key, createLeaf());
                        }

                        var list = nodes[layer2.Key];
                        foreach (var leaf in layer2.Value)
                        {
                            list.Add(leaf);
                        }
                    }
                }
            }
        }

        protected override Dictionary<string, Dictionary<string, List<NodeInfo>>> GetStartedWith(DateTime date, bool withRemoving )
        {
            var result = new Dictionary<string, Dictionary<string, List<NodeInfo>>>();

            var survivedData = new Dictionary<string, Dictionary<string, SortedSet<NodeInfo>>>();
            lock (_readWriteLock)
            {

                foreach (var resource in _data)
                {
                    
                    var nodes = new Dictionary<string, List<NodeInfo>>();

                    //todo optimize it later.
                    Dictionary<string, SortedSet<NodeInfo>> survived = null;
                    if (withRemoving)
                    {
                        survivedData.Add(resource.Key, new Dictionary<string, SortedSet<NodeInfo>>());
                        survived = survivedData[resource.Key];
                    }

                    foreach (var node in resource.Value)
                    {
                        var infos = node.Value.TakeWhile(x => CompareByDate(x, date)/*x.TimeSnapshot.CompareTo(date) > 0*/).ToList();

                        if (withRemoving)
                        {
                            survived.Add(node.Key, createLeaf(node.Value.Except(infos)));
                        }

                        //Common.Utility.LogDebug(string.Format("ResourceCache:  '{0}':'{1}' - Selected count: {2}",resource.Key, node.Key, infos.Count));

                        if (infos.Count > 0)
                        {
                            nodes.Add(node.Key, infos);
                        }
                    }

                    if (nodes.Count > 0)
                    {
                        result.Add(resource.Key, nodes);
                    }
                }

                if (withRemoving)
                {
                    _data = survivedData;    
                }
            }
            return result;
        }

        private SortedSet<NodeInfo> createLeaf(IEnumerable<NodeInfo> data = null)
        {
            return data == null ? new SortedSet<NodeInfo>(new NodeInfoEqualityComparer()) : new SortedSet<NodeInfo>(data, new NodeInfoEqualityComparer());
        }

        public override Tuple<object,DateTime,DateTime> CollectGarbage()
        {
            lock (_readWriteLock)
            {
                var survivedData = new Dictionary<string, Dictionary<string, SortedSet<NodeInfo>>>();
                var deadData = new Dictionary<string, Dictionary<string, SortedSet<NodeInfo>>>();

                foreach (var layer1 in _data)
                {
                    foreach (var layer2 in layer1.Value)
                    {
                        var survived = createLeaf(layer2.Value.Where(x => !IsExpired(x)));

                        var dead = createLeaf(layer2.Value.Except(survived)); 

                        //todo optimize it later.
                        if (survived.Count > 0)
                        {
                            if (!survivedData.ContainsKey(layer1.Key))
                            {
                                survivedData.Add(layer1.Key,new Dictionary<string, SortedSet<NodeInfo>>());
                            }
                            survivedData[layer1.Key].Add(layer2.Key, survived);
                        }

                        if (dead.Count > 0)
                        {
                            if (!deadData.ContainsKey(layer1.Key))
                            {
                                deadData.Add(layer1.Key, new Dictionary<string, SortedSet<NodeInfo>>());
                            }
                            deadData[layer1.Key].Add(layer2.Key,dead);
                        }
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

        protected Tuple<object, DateTime, DateTime> FormDataForUtilize(Dictionary<string, Dictionary<string, SortedSet<NodeInfo>>> data)
        {
            var startEnd = new DateTime[] { DateTime.MaxValue, DateTime.MinValue };

            var result = data.Aggregate(startEnd,
                            (seed, x) =>
                            {
                                return x.Value.Aggregate(seed, (seedy, y) =>
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

            var dataToSave = data.Select(x =>
                {
                    var dict = x.Value.Select(y =>new KeyValuePair<string, List<NodeInfo>>(y.Key, y.Value.ToList()))
                                      .ToDictionary(y => y.Key, y => y.Value);
                    return new KeyValuePair<string, Dictionary<string, List<NodeInfo>>>(x.Key, dict);
                }).ToDictionary(x=>x.Key, x => x.Value);

            return new Tuple<object, DateTime, DateTime>(dataToSave, result[0], result[1]);
        }

        private class NodeInfoEqualityComparer : IComparer<NodeInfo>
        {
            public int Compare(NodeInfo x, NodeInfo y)
            {
                return -1 * x.TimeSnapshot.CompareTo(y.TimeSnapshot);
            }
        }
    }
}
