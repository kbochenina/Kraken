using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using CommonDataTypes.RExService.Service.Entity.Info;
using CommonDataTypes.Utility;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;
using MITP;
using NLog.Config;
using ServiceProxies.SchedulerService;
//using Resource = ControllerFarmService.ResourceBaseService.Resource;
using Resource = ServiceProxies.ResourceBaseService.Resource;

namespace ControllerFarmService.Statistic
{
    class CacheCollectorFactory
    {
        public const string CACHE_COLLECTOR_MOCK_MODE = "CacheCollectorMockMode"; 

        public const string PUSH_RESOURCE_STATISTIC_ON = "PushResourceStatisticOn";
 
        public const string PUSH_TASK_STATISTIC_ON = "PushTaskStatisticOn"; 

        private static CacheCollectorFactory _instance;

        private readonly bool pushResStat;
        private readonly bool pushTaskStat;

        public static bool CheckMockMode()
        {
           return !string.IsNullOrEmpty(ConfigurationManager.AppSettings[CACHE_COLLECTOR_MOCK_MODE]);
        }

        public CacheCollectorFactory()
        {
            // check if resource statistics is pushed to a gathering service
            var resStat = ConfigurationManager.AppSettings[PUSH_RESOURCE_STATISTIC_ON];
            if (resStat == null || !Boolean.TryParse(resStat, out pushResStat))
            {
                pushResStat = false;
            }
            // check if task statistics is pushed to a gathering service
            var taskStat = ConfigurationManager.AppSettings[PUSH_TASK_STATISTIC_ON];
            if (taskStat == null || !Boolean.TryParse(taskStat, out pushTaskStat))
            {
                pushTaskStat = false;
            }
        }


        public static CacheCollectorFactory GetInstance()
        {
            var mode = ConfigurationManager.AppSettings[CACHE_COLLECTOR_MOCK_MODE];

            if (_instance != null)
            {
                return _instance;
            }

            var result = _instance = string.IsNullOrEmpty(mode) ? new CacheCollectorFactory():new MockCacheCollectorFactory();
            
            Common.Utility.LogInfo(" Global Cache Collector Factory of type " + result.GetType() + " is created");

            return result;
        }

        public virtual GlobalCacheCollector GetResourceCacheCollector(IEnumerable<Resource> resources)
        {

            return pushResStat ? GlobalCacheCollector.GetInstance(resources) : new EmptyNodeCacheCollector(resources);
        }

        public virtual TaskCacheCollector GetTaskCacheCollector()
        {
            return pushTaskStat ? TaskCacheCollector.GetInstance() : new EmptyTaskCacheCollector();
        }

        //for mock
        public virtual void RunWithFarm(MITP.ControllerFarmService farm){}
        //for mock
        public virtual void SendTask(TaskRunContext task){}


        private class MockCacheCollectorFactory : CacheCollectorFactory
        {
            public override GlobalCacheCollector GetResourceCacheCollector(IEnumerable<Resource> resources)
            {
                return null;
            }

            public override TaskCacheCollector GetTaskCacheCollector()
            {
                return null;
            }

            public override void RunWithFarm(MITP.ControllerFarmService farm)
            {
                try
                {
                    string farmId = ConfigurationManager.AppSettings[MITP.ControllerFarmService.FARMID_PARAM_NAME];

                    Resource[] data = null;
                    ResourceBaseServiceClient client = null;
                    try
                    {
                        client = new ResourceBaseServiceClient();
                        data = client.GetResourcesForFarm(farmId, null);
                    }
                    catch (Exception ex)
                    {
                        Common.Utility.LogError(" Failed to get resources from ResourceBaseService. Exception: " + ex);
                        throw ex;
                    }
                    finally
                    {
                        client.Close();
                    }


                    var resourceCollector = base.GetResourceCacheCollector(data);
                    var taskCollector = base.GetTaskCacheCollector();

                    Utility.CreateAndRunRepeatedProcess(5000, false, () =>
                        {
                            Common.Utility.ExceptionablePlaceWrapper(() =>
                                {
                                    foreach (var resource in data)
                                    {
                                        resourceCollector.push(resource.ResourceName, GenerateResourecMockData(resource));
                                    }
                                },
                                "Exception while creating and pushing resources mock data in MockCacheCollectorFactory",
                                " Mock statistic data for resources have been generated and pushed",false);

                        });
                }
                catch (Exception ex)
                {
                    Common.Utility.LogError("failed to reinitialize collecting data", ex);
                }
               
            }

            private TaskCacheCollector taskCacheCollector;

            public override void SendTask(TaskRunContext task)
            {
                //todo rewrite all this sht later 
                if (taskCacheCollector == null)
                {
                    taskCacheCollector = base.GetTaskCacheCollector();
                }

                var data = ExtractInfoCountPerNode(task);
                var iter = data.GetEnumerator();

                int current = 0;

                var t = 0;
                var coeff = Math.Sin(t);
                var angleRandom = new Random();
              
                 Utility.CreateAndRunRepeatedProcess(1000, false, () =>
                 {
                     Common.Utility.ExceptionablePlaceWrapper(() =>
                     {
                         var info = GenerateTaskMockData(iter.Current.Item1.NodeName, t);
                         taskCacheCollector.push(task, task.TaskId, info);
                     }, " Exception while creating and pushing task mock data for taskId=" + task.TaskId + " in MockCacheCollectorFactory", 
                        " Mock statistic data for task with taskId=" + task.TaskId + " have been generated and pushed", false);
                } , () =>
                {
                    if (current == 0)
                    {
                        if (!iter.MoveNext())
                        {
                            t = 0;
                            return true;
                        }
                        current = iter.Current.Item2;
                    }

                    t += angleRandom.Next(5,20);
                    --current;
                    return false;
                   
                });
               
            }

            private double t = 0;

            private IEnumerable<NodeStateResponse> GenerateResourecMockData(Resource resource)
            {
                List<NodeStateResponse> list = new List<NodeStateResponse>();
                foreach (var node in resource.Nodes)
                {
                    var response = new NodeStateResponse(node.NodeName);

                    var coeff = Math.Sin(t);
                    t += 4;

                    response.Consumption = new List<NodeInfo>();
                    response.Consumption.Add(new NodeInfo()
                    {
                        DiskAvailableFreeSpace = (long)(100000 * (coeff + 1)/2),
                        DiskUsage = (100 * (coeff + 1) / 2),
                        MemAvailable = (100 * (coeff + 1) / 2),
                        MemUsage = (100 * (coeff + 1) / 2),
                        Net = (100 * (coeff + 1) / 2),
                        Offline = false,
                        ProcUsage = (100 * (coeff + 1) / 2),
                        SandBoxTotalSize = 1000000,
                        TimeSnapshot = DateTime.Now,
                    });

                    list.Add(response);
                }

                return list;
            }

            private List<Tuple<ResourceNode, int>> ExtractInfoCountPerNode(TaskRunContext task)
            {
                var nodesForTask = task.Resource.Nodes.Where(x => task.NodesConfig.Any(y => y.NodeName == x.NodeName));

                var random = new Random();

                List<Tuple<ResourceNode, int>> list = nodesForTask.Select(x => new Tuple<ResourceNode, int>(x, random.Next(2, 5))).ToList();

                return list;
            }

            private TaskStateInfo GenerateTaskMockData(string nodeName, int koeff)
            {
                //todo recode it in a proper way later.
                var info = new ProcessStatInfo();

                var k1 = koeff*koeff + koeff + 1;
                var k2 = koeff*koeff;
                var k3 = Math.Sqrt(koeff) + koeff*koeff;

                info.FileCount = "5";
                info.Net = 1000 * k1;
                info.PhysicalMem =5000 * k2 ;
                info.ProcUsage = (long)(3000 * k3);
                info.TotalProcTime = (long)(4000 * k3);
                info.WorkDirSize = 11000 * k1;
                info.TimeSnapshot = DateTime.Now;

                var taskStateInfo = new TaskStateInfo();
                taskStateInfo.NodeName = nodeName;
                taskStateInfo.ProcessInfo = info;

                return taskStateInfo;
            }

        }
    }
}
