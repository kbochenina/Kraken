using System;
using System.Collections.Generic;
using System.Linq;
using CommonDataTypes;
using CommonDataTypes.RExService.Service.Entity.Info;
using CommonDataTypes.Utility;
using ControllerFarmService.Controllers;
using MITP;
using ServiceProxies.StatGlobalCache;

namespace ControllerFarmService.Statistic
{
    class TaskCacheCollector : ITaskGlobalCacheCollector
    {
        private static TaskCacheCollector _instance;
        private static System.Timers.Timer timer;

        public static TaskCacheCollector GetInstance(/*IEnumerable<TaskRunContext> tasks*/)
        {
            return _instance ?? (_instance = new TaskCacheCollector(/*tasks*/));
        }

        private static StatisticalGlobalCacheClient GetStatisticalGlobalCacheClient()
        {
            return new StatisticalGlobalCacheClient("globalCacheClient");
        }

        private readonly object _lock = new object();

        private Dictionary<ulong/*taskId*/, TaskStatInfo> bufferTaskInfo = new Dictionary<ulong, TaskStatInfo>(); 

        public TaskCacheCollector()
        {
            StartObservation();
        }

        public void push(TaskRunContext context, ulong taskId, TaskStateInfo info)
        {
            Common.Utility.LogInfo("TaskCacheCollector.push taskId=" + taskId + " info=" + info.ProcessInfo.TimeSnapshot);

            if (!IsServicedController(context.Controller)){ return;}

            var resName = context.Resource.ResourceName;

            lock (_lock)
            {
                if (!bufferTaskInfo.ContainsKey(taskId))
                {
                    bufferTaskInfo.Add(taskId, new TaskStatInfo(new Dictionary<string, List<ProcessStatInfo>>(),resName));
                }

                if (!bufferTaskInfo[taskId].ProcessInfoCollection.ContainsKey(info.NodeName))
                {
                    bufferTaskInfo[taskId].ProcessInfoCollection.Add(info.NodeName,new List<ProcessStatInfo>());
                }

                bufferTaskInfo[taskId].ProcessInfoCollection[info.NodeName].Add(info.ProcessInfo);
            }
        }

        protected bool IsServicedController(IStatelessResourceController ctr)
        {
            if (CacheCollectorFactory.CheckMockMode())
            {
                return true;
            }

            return !(ctr is IStatisticalCacheableController);
        }

        private void StartObservation()
        {
            Action infoRequest = () =>
            {
                //Log.Info("infoRequest Start");
                lock (_lock)
                {
                    var client = GetStatisticalGlobalCacheClient();
                    try
                    {
                        if (bufferTaskInfo.Count > 0 &&
                            bufferTaskInfo.Any(x => x.Value.ProcessInfoCollection.Any(y => y.Value.Count > 0)))
                        {
                            client.AddAllTaskInfo(bufferTaskInfo);
                        }

                        
                    }
                    catch (Exception ex)
                    {
                        Common.Utility.LogError("Exception occured while uploading data to a global cache:" + ex);
                    }
                    finally
                    {
                        client.Close();
                        bufferTaskInfo = new Dictionary<ulong, TaskStatInfo>();
                    }
                }
            };

            Utility.CreateAndRunRepeatedProcess(5000, false, infoRequest);
        }
    }
}
