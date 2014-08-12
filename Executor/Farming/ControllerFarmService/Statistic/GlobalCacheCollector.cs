using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.ServiceModel;
using System.Text;
using CommonDataTypes.RExService.Service.Entity.Info;
using ControllerFarmService.Controllers;
using MITP;
using MongoDB.Driver.Builders;
using ServiceProxies.SchedulerService;
using ServiceProxies.StatGlobalCache;
//using Resource = ControllerFarmService.ResourceBaseService.Resource;
using Resource = ServiceProxies.ResourceBaseService.Resource;
using Task = System.Threading.Tasks.Task;

namespace ControllerFarmService.Statistic
{
    class GlobalCacheCollector:INodeGlobalCacheCollector
    {
        private static GlobalCacheCollector _instance;
        public static GlobalCacheCollector GetInstance(IEnumerable<Resource> resources)
        {
            return _instance ?? (_instance = new GlobalCacheCollector(resources));
        }

        private IEnumerable<Resource> _resources;

        private static StatisticalGlobalCacheClient GetStatisticalGlobalCacheClient()
        {
            return new StatisticalGlobalCacheClient("globalCacheClient");
        }

        private readonly object _lock = new object();

        private Dictionary<string/*resName*/,Dictionary<string/*nodeName*/,List<NodeInfo>>> bufferResourceConsumption = new Dictionary<string, Dictionary<string, List<NodeInfo>>>();

        public GlobalCacheCollector(IEnumerable<Resource> resources)
        {
            _resources = resources;
                
            CleanBuffer();
           
        }

        public void push(string resName, IEnumerable<NodeStateResponse> infos)
        {
            lock (_lock)
            {
                //uncheckable resource
                if (!bufferResourceConsumption.ContainsKey(resName))
                {
                    return;
                }

                var resource = bufferResourceConsumption[resName];

                foreach (var info in /*setTimeStamp(*/infos/*)*/)
                {
                    resource[info.NodeName].AddRange(info.Consumption);   
                }

                if (CheckForUpload())
                    fireUpload();
                    
            }
        }

        private bool CheckForUpload()
        {
            Func<KeyValuePair<string, Dictionary<string /*nodeName*/, List<NodeInfo>>>, bool> innerFilter = x =>
            {
                var count = x.Value.Where(y => y.Value.Count != 0).Count();
                return count == x.Value.Count;
            };

           return bufferResourceConsumption.Where(innerFilter).Count() == bufferResourceConsumption.Count;
        }

        private void fireUpload()
        {
            Task.Factory.StartNew(() =>
                {
                    var globalCache = GetStatisticalGlobalCacheClient();
                    globalCache.Open();

                    lock (_lock)
                    {

                        try
                        {

                            globalCache.AddAllResourcesInfo(bufferResourceConsumption);
                            Common.Utility.LogInfo("Resources statistic data have been uploaded to a global cache ");
                           
                        }
                        catch(Exception ex)
                        {
                            Common.Utility.LogError("statistics upload failed.");
                            //Common.Utility.LogError("statistics upload failed. Reason: " + ex.ToString());
                            //globalCache.Abort();
                        }
                        finally
                        {
                            try
                            {
                                globalCache.Close();
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Yes It is");
                            }
                            CleanBuffer();
                        }
                    }

               
            });
        }

        protected void CleanBuffer()
        {
            var reses = _resources.Where(x => isServicedController(x));
            
            bufferResourceConsumption = new Dictionary<string, Dictionary<string, List<NodeInfo>>>();

            foreach (var resource in reses)
            {
                var nodes = new Dictionary<string, List<NodeInfo>>();

                foreach (var node in resource.Nodes)
                {
                    nodes.Add(node.NodeName, new List<NodeInfo>());
                }

                bufferResourceConsumption.Add(resource.ResourceName, nodes);
            }
        }

        protected bool isServicedController(Resource res)
        {
            if (CacheCollectorFactory.CheckMockMode())
            {
                return true;
            }
            return res.Controller.Type == "UnixBaseController";
        }
    }

    
}
