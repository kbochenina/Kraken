using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ControllerFarmService.Statistic;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Attributes;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;
using Config = System.Configuration.ConfigurationManager;
using PFX = System.Threading.Tasks;

namespace MITP
{
    internal class ResourceCache
    {
        private static readonly TimeSpan UPDATE_DURATION_TO_WARN = TimeSpan.FromSeconds(0.3);
        private static readonly TimeSpan UPDATE_INTERVAL = TimeSpan.FromMilliseconds(300);

        private static readonly Dictionary<string, ResourceCache> _cache = new Dictionary<string, ResourceCache>();
        private static readonly object _globalLock = new object();

        public readonly object StateLock = new object();

        [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
        public ObjectId _id { get; set; }

        public Resource Resource { get; private set; }
        public IStatelessResourceController Controller { get; private set; }

        public NodeStateInfo[] NodeStateInfo { get; private set; }     // mutable

        private bool _isUpdating { get; set; }                         // mutable
        private DateTime _lastUpdateTime { get; set; }                 // mutable

        //todo nbutakov change
        private INodeGlobalCacheCollector gcCollector;

        //private void ApplyNodesState

        private ResourceCache(Resource resource, IStatelessResourceController controller, INodeGlobalCacheCollector collector = null )
        {
            Resource = resource;
            Controller = controller;
            gcCollector = collector;
            NodeStateInfo = resource.Nodes.Select(node => 
                new NodeStateInfo(node.ResourceName, node.NodeName, node.CoresCount, node.TasksSubmissionLimit)
            ).ToArray();

            _isUpdating = false;
            _lastUpdateTime = DateTime.Now - UPDATE_INTERVAL - TimeSpan.FromMilliseconds(50);
        }

        public static ResourceCache GetByName(string resourceName)
        {
            ResourceCache resourceCache;
            lock (_globalLock)
            {
                if (!_cache.ContainsKey(resourceName))
                {
                    Log.Error("No cache for resource " + resourceName);
                    throw new ArgumentException("Unknown resource for this farm");
                }

                resourceCache = _cache[resourceName];
            }
            return resourceCache;
        }

        private static NodeStateInfo[] LoadStates(IEnumerable<string> resourceNames)
        {
            return new NodeStateInfo[0]; // todo : node save
            var collection = Mongo.GetCollection<NodeStateInfo[]>();
            var nodeStates = collection.AsQueryable<NodeStateInfo>().Where(n => n.ResourceName.In(resourceNames)).ToArray();
            return nodeStates;
        }

        public void Save()
        {
            return; // todo: node save
            var collection = Mongo.GetCollection<NodeStateInfo[]>();
            lock (StateLock)
            {
                collection.Save(NodeStateInfo);
            }
        }

        public static void ReloadResources(IEnumerable<Resource> resources)
        {
            lock (_globalLock)
            {
                while (_cache.Any())
                {
                    var elem = _cache.First();
                    lock (elem.Value.StateLock)
                    {
                        elem.Value.Save();
                        _cache.Remove(elem.Key);
                    }
                }

                var resourceNames = resources.Select(r => r.ResourceName);
                var nodeStates = LoadStates(resourceNames);
                var gcCollector = CacheCollectorFactory.GetInstance().GetResourceCacheCollector(resources);
                foreach (var resource in resources)
                {
                    var controller = ControllerBuilder.Build(resource);
                    var resourceCache = new ResourceCache(resource, controller, gcCollector);

                    foreach (var node in resourceCache.NodeStateInfo)
                    {
                        var savedNodeState = nodeStates.FirstOrDefault(s => s.ResourceName == node.ResourceName && s.NodeName == node.NodeName);
                        if (savedNodeState != null)
                            node.ApplyState(savedNodeState);
                        else
                            node.State = NodeState.Busy; // to prevent node usage before actual state update, which is async
                    }

                    _cache[resource.ResourceName] = resourceCache;
                }

                /*
                //foreach (string name in resourceNames)
                PFX.Parallel.ForEach(resourceNames, (name) =>
                {
                    UpdateNodesState(name);
                });
                */
            }
        }

        public void Acquire(IEnumerable<NodeRunConfig> nodesConfig)
        {
            lock (this.StateLock)
            {
                bool nodesOverloaded = false;

                var nodeStates = this.NodeStateInfo;
                foreach (var nodeConfig in nodesConfig)
                {
                    var nodeState = nodeStates.Single(n => n.NodeName == nodeConfig.NodeName);

                    if (nodeState.CoresAvailable < nodeConfig.Cores && nodeState.State != NodeState.Busy) // todo: think. This fixes "Busy" bug on resources update
                        nodesOverloaded = true;

                    nodeState.TasksSubmitted++;
                    nodeState.CoresReserved += nodeConfig.Cores;
                }

                this.Save();

                if (nodesOverloaded)
                {
                    Log.Error("Nodes overload for resource " + this.Resource.ResourceName);
                    throw new Exception("Nodes are overloaded for resource " + this.Resource.ResourceName);
                }
            }
        }

        public void Release(IEnumerable<NodeRunConfig> nodesConfig)
        {
            lock (this.StateLock)
            {
                foreach (var nodeConfig in nodesConfig)
                {
                    var nodeState = this.NodeStateInfo.SingleOrDefault(n => n.NodeName == nodeConfig.NodeName);

                    if (nodeState == null)
                    {
                        Log.Warn(String.Format(
                            "Found unknown node '{0}.{1}' in config for some task while releasing resource",
                            nodeConfig.ResourceName, nodeConfig.NodeName
                        ));
                    }
                    else
                    {
                        nodeState.TasksSubmitted--;
                        nodeState.CoresReserved -= nodeConfig.Cores;

                        if (nodeState.TasksSubmitted < 0)
                        {
                            Log.Error("TasksSubmitted < 0 on node " + nodeState.NodeName + ". Setting it to 0");
                            nodeState.TasksSubmitted = 0;
                        }

                        if (nodeState.CoresReserved < 0)
                        {
                            Log.Error("CoresReserved < 0 on node " + nodeState.NodeName + ". Setting it to 0");
                            nodeState.CoresReserved = 0;
                        }
                    }
                }

                this.Save();
            }
        }
        
        public static void UpdateNodesState(string resourceName)
        {
            // todo : PFX.Task.Run in .Net 4.5
            PFX.Task.Factory.StartNew(() =>
            {
                var cache = GetByName(resourceName);

                if (!cache._isUpdating && (cache._lastUpdateTime + UPDATE_INTERVAL < DateTime.Now || DateTime.Now < cache._lastUpdateTime))
                {
                    bool isThisThreadUpdating = false;
                    lock (cache.StateLock)
                    {
                        if (!cache._isUpdating && (cache._lastUpdateTime + UPDATE_INTERVAL < DateTime.Now || DateTime.Now < cache._lastUpdateTime))
                        {
                            cache._isUpdating = true;
                            isThisThreadUpdating = true;
                        }
                    }

                    if (isThisThreadUpdating)
                    {
                        IEnumerable<NodeStateResponse> stateResponse;
                        try
                        {
                            var timer = System.Diagnostics.Stopwatch.StartNew();
                            //Log.Info("Updating nodes state for resource " + resourceName);
                            stateResponse = cache.Controller.GetNodesState(cache.Resource);
                            //Log.Info("Got response on update for resource " + resourceName);

                            timer.Stop();
                            if (timer.Elapsed > UPDATE_DURATION_TO_WARN)
                                Log.Warn(String.Format("Resource '{0}' update took {1} seconds", resourceName, timer.Elapsed.TotalSeconds));

                            //todo nbutakov change
                            if (cache.gcCollector != null)
                            {
                                cache.gcCollector.push(cache.Resource.ResourceName, stateResponse);
                            }

                        }
                        catch (Exception e)
                        {
                            Log.Error(String.Format("Error on updating resource '{0}' state: {1}",
                                resourceName, e
                            ));
                            stateResponse = cache.Resource.Nodes.Select(node => new NodeStateResponse(node.NodeName) { State = NodeState.Down });
                        }

                        lock (cache.StateLock)
                        {
                            try
                            {
                                // todo : WARN + 'Down' state for each node that is not in resource response

                                foreach (var nodeResponse in stateResponse)
                                {
                                    var nodeCache = cache.NodeStateInfo.FirstOrDefault(n => n.NodeName == nodeResponse.NodeName);
                                    if (nodeCache != null)
                                        nodeCache.ApplyState(nodeResponse);
                                    else
                                        Log.Warn("Resource returned info on unknown node " + nodeResponse.NodeName);

                                    cache.Save();
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error(String.Format("Exception on applying resource '{0}' nodes state: {1}", resourceName, e));
                                throw;
                            }
                            finally
                            {
                                cache._lastUpdateTime = DateTime.Now;
                                cache._isUpdating = false;
                            }
                        }
                    }
                }
            });
        }

        public static string[] GetActiveResourceNames()
        {
            lock (_globalLock)
            {
                var names = _cache.Keys.Select(st => st).ToArray();
                return names;
            }
        }

        //todo nbutakov change
        public static ResourceCache[] GetActiveResources()
        {
            lock (_globalLock)
            {
                var resources = _cache.Values.Select(st => st).ToArray();
                return resources;
            }
        }


        /*
        public static void AddOrUpdateResource(Resource resource, IStatelessResourceController controller)
        {
            lock (_globalLock)
            {


                if (_cache.ContainsKey(resource.ResourceName))
                {
                    var resourceCache = _cache[resource.ResourceName];

                    lock (resourceCache.StateLock)
                    {
                        resourceCache.Resource = resource;
                        resourceCache.Controller = controller;

                        controller.GetNodesState
                    }
                }

                var resourceCache = new ResourceCache(resource, controller);

                _cache[resource.ResourceName] = resourceCache;
            }
        }
        */
    }
}


