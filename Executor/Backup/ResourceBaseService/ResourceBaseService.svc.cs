using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Threading;
using PFX = System.Threading.Tasks;

using ResourceBaseService.ControllerFarmService;

namespace MITP
{
    //[ServiceBehavior(Namespace = "http://escience.ifmo.ru/nano/services/", InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]    
    [ServiceBehavior(Namespace = "http://escience.ifmo.ru/nano/services/", InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]    
    // PerCall to call construstor before every method
    public class ResourceBaseService : IResourceBaseService
    {
        private static readonly TimeSpan UPDATE_INTERVAL = TimeSpan.FromSeconds(1);
        private static DateTime _lastUpdateTime = DateTime.Now - UPDATE_INTERVAL - UPDATE_INTERVAL;

        private static Resource[] _cachedResources = Resource.Load().ToArray();
        private static Resource[] _futureResources = null;

        //private static object _resourcesLock = new object();
        //private static object _dumpingLock   = new object();
        //private static bool _updateFinished = true;

        private static object _cacheLock  = new object();

        private static object _updateLock = new object();
        private static bool   _updating   = false;

        private static HashSet<string> _updateKeys = new HashSet<string>();
        
        private ResourceBaseService()
        {
            PFX.Task.Factory.StartNew(() =>
            {
                CheckForResourceDescriptionsUpdate();                
            });
        }

        private static bool ItsTimeToUpdate()
        {
            return (!_updating && (DateTime.Now > _lastUpdateTime + UPDATE_INTERVAL || _lastUpdateTime > DateTime.Now));
        }

        private static void CheckForResourceDescriptionsUpdate()
        {
            if (ItsTimeToUpdate())
            {
                lock (_updateLock)
                {
                    if (ItsTimeToUpdate())
                    {
                        _updating = true;

                        var futureResources = Resource.Load().ToArray();

                        Resource[] currentResources = null;                        
                        lock (_cacheLock)
                        {
                            currentResources = _cachedResources;
                            _futureResources = futureResources;
                        }

                        //var unchangedResourceNames = futureResources.Intersect(currentResources).Select(r => r.ResourceName);
                        //var affectedControllerUrls = futureResources.Where(r => uncha

                        var affectedControllerUrls = 
                            futureResources
                                .Select(r => r.Controller.Url).Distinct()
                            .Union(currentResources
                                .Select(r => r.Controller.Url).Distinct());

                        if (String.Join("\r\n",  futureResources.OrderBy(r => r.ResourceName).Select(r => r.Json)) ==
                            String.Join("\r\n", currentResources.OrderBy(r => r.ResourceName).Select(r => r.Json)))
                            affectedControllerUrls = new string[0];

                        if (_updateKeys.Any())
                        {
                            // todo : Log.Warn
                            _updateKeys = new HashSet<string>();
                        }

                        foreach (string url in affectedControllerUrls) // _updateKeys not threadsafe => not in parallel 
                        {
                            string updateKey = url; // todo : generate dumping key

                            _updateKeys.Add(updateKey);

                            var farm = new ControllerFarmServiceClient();
                            farm.Endpoint.Address = new EndpointAddress(url);
                            farm.InnerChannel.OperationTimeout = TimeSpan.FromSeconds(5);

                            try
                            {
                                farm.ReloadAllResources(updateKey);
                                farm.Close();
                            }
                            catch (Exception e)
                            {
                                farm.Abort();                                
                                // todo: log exception

                                _updateKeys.Remove(updateKey);
                            }
                        }

                        if (!_updateKeys.Any())
                        {
                            _updating = false;
                            _lastUpdateTime = DateTime.Now;
                        }
                    }
                }
            }
        }

        public Resource[] GetAllResources()
        {
            /*
                            Resource[] toReload = null;                        
                            lock (_resourcesLock)
                            {
                                toReload = futureResources.Union(_activeResources).ToArray();
                            }

                            foreach (var res in futureResources)//toReload)
                            {
                                string dumpingKey = res.Controller.FarmId; // todo : generate dumping key

                                lock (_dumpingLock)
                                {
                                    _updateFinished = false;
                                    _dumpingKeys.Add(dumpingKey);
                                }

                                var farm = new ControllerFarmServiceClient();
                                try
                                {
                                    farm.ReloadAllResources(dumpingKey);
                                    farm.Close();
                                }
                                catch (Exception e)
                                {
                                    farm.Abort();                                
                                    // todo: log exception

                                    lock (_dumpingLock)
                                    {
                                        _dumpingKeys.Remove(dumpingKey);
                                    }
                                }
                            }

                            while (!_updateFinished)
                            {
                                lock (_dumpingLock)
                                {
                                    if (!_dumpingKeys.Any())
                                    {
                                        lock (_resourcesLock)
                                        {
                                            _activeResources = futureResources;
                                            _lastUpdateTime = DateTime.Now;
                                            _updateFinished = true;
                                        }
                                    }
                                }

                                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                            }
                        }
                    }
                });

                lock (_resourcesLock)
                {
                    var resources = _activeResources.ToArray();
                */
            lock (_cacheLock)
            {
                var resources = _cachedResources.ToArray();
                return resources;
            }
        }

        public string[] GetResourceNames()
        {
            var resources = GetAllResources();
            string[] names = resources.Select(r => r.ResourceName).ToArray();
            return names;
        }

        public Resource GetResourceByName(string resourceName)
        {
            var resources = GetAllResources();
            var res = resources.Single(r => r.ResourceName == resourceName);
            return res;
        }

        public string[] GetNodeNames(string resourceName)
        {
            var resources = GetAllResources();
            var res = resources.Single(r => r.ResourceName == resourceName);
            var nodeNames = res.Nodes.Select(n => n.NodeName).ToArray();
            return nodeNames;
        }

        public ResourceNode GetResourceNodeByName(string resourceName, string nodeName)
        {
            var resource = GetResourceByName(resourceName);
            var node = resource.Nodes.Single(n => n.NodeName == nodeName);
            return node;
        }

        public Resource[] GetResourcesForFarm(string farmId, string updateKey = null)
        {
            if (updateKey != null)      // todo : reset timeout
            {
                lock (_updateLock)
                {
                    _updateKeys.Remove(updateKey);

                    if (!_updateKeys.Any())
                    {
                        lock (_cacheLock)
                        {
                            _cachedResources = _futureResources;
                            _futureResources = null;
                        }

                        _updating = false;
                        _lastUpdateTime = DateTime.Now;
                    }
                }

                while (_updating)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(120)); // todo : random sleep
                }
            }

            var allResources = GetAllResources();
            var resourcesForFarm = allResources.Where(r => r.Controller.FarmId.ToLowerInvariant() == farmId.ToLowerInvariant()).ToArray();

            return resourcesForFarm;
        }
    }
}
