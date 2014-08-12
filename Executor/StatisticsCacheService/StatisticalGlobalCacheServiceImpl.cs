using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using Common;
using CommonDataTypes;
using ServiceProxies.ControllerFarmService;
//using ServiceProxies.StatFarmBuffer;
using StatisticsCacheService.Cache;
using StatisticsCacheService.Cache.FileDumping;
using NodeInfo = CommonDataTypes.RExService.Service.Entity.Info.NodeInfo;

namespace StatisticsCacheService
{
    [ServiceBehavior(Namespace = "http://escience.ifmo.ru/nbutakov/services/", InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class StatisticalGlobalCacheServiceImpl:IStatisticalGlobalCache, IStatisticalService, IArchiveFilesService
    {
        private static ResourceCache _resourceCache = CacheFactory.GetFactory().CreateResourceCache();

        private static TaskCache _taskCache = CacheFactory.GetFactory().CreateTaskCache();
 
        private static StatisticalBufferClient GetStatFarmBufferClient()
        {
            return new StatisticalBufferClient("StatBufferEndpoint");
        }

        public void AddAllTaskInfo(Dictionary<ulong, TaskStatInfo> data)
        {

            if (!CheckDataCorrectness(data))
            {
                Utility.LogError("Data for StatisticalGlobalCacheServiceImpl.AddAllTaskInfo is incorrect");
                return;
            }

            Utility.ExceptionablePlaceWrapper(() =>
            {
                _taskCache.AddAllInfo(data);
            },"", "task info have been added");

        }

        public void AddAllResourcesInfo(Dictionary<string, Dictionary<string, List<NodeInfo>>> data)
        {
            if (!CheckDataCorrectness(data))
            {
                Utility.LogError("Data for StatisticalGlobalCacheServiceImpl.AddAllResourcesInfo is incorrect");
                return;
            }

            Utility.ExceptionablePlaceWrapper(() =>
            {
                _resourceCache.AddAllInfo(data);
            }, "", "resource info have been added");
        }

        public Dictionary<ulong, TaskStatInfo> GetAllTaskInfoStartedWith(DateTime date)
        {
            Dictionary<ulong, TaskStatInfo> result = null;

            Utility.ExceptionablePlaceWrapper(() =>
            {
                result = GetAllInfoStartedWith(() =>
                {
                    return _taskCache.GetAndRemoveAllInfoStartedWith(date);
                },
                x =>
                {
                    return x.GetAllCacheableTaskInfoStartedWith(date);
                });
            },"","Getting all task info have succeded");
          

            return result;
        }

        public TaskStatInfo GetTaskAllInfoStartedWith(ulong taskId, DateTime date)
        {

            Common.Utility.LogDebug(string.Format("Start of method GetTaskAllInfoStartedWith for taskId: {0}, time: {1}", taskId, date));

            TaskStatInfo bufferData = null;

            Utility.ExceptionablePlaceWrapper(() =>
            {
                TaskStatInfo cacheData = _taskCache.GetStartedWith(taskId, date, true);

                if (cacheData != null)
                {
                    bufferData = cacheData;
                    return;
                }

                StatisticalBufferClient farmBuffer = GetStatFarmBufferClient();


                try
                {
                    bufferData = farmBuffer.GetTaskInfoStartedWith(taskId, date);
                }
                finally
                {
                    farmBuffer.Close();
                }
            },"Failed to get info for task with taskId=" + taskId,"Getting info for task with taskId=" + taskId + " have succeded");

            Common.Utility.LogDebug(string.Format("End of method GetTaskAllInfoStartedWith for taskId: {0}, time: {1}", taskId, date));

            return bufferData;
        }

        public Dictionary<string, Dictionary<string, List<NodeInfo>>> GetAllResourcesInfoStartedWith(DateTime date)
        {
            //throw new NotImplementedException();

            Common.Utility.LogDebug(string.Format("Start of method GetAllResourcesInfoStartedWith for time: {0}", date));

            Dictionary<string, Dictionary<string, List<NodeInfo>>> data = null; 
            try
            {
                 data = GetAllInfoStartedWith(() =>
                {
                    return _resourceCache.GetAndRemoveAllInfoStartedWith(date);
                }
               , x =>
               {
                   try
                   {
//                       return new Dictionary<string, Dictionary<string, List<NodeInfo>>>();
                       return x.GetAllCacheableResourcesInfoStartedWith(date);
                   }
                   catch (Exception ex)
                   {
                       Common.Utility.LogError(" cannot to get resources from cache controllers from farm ",ex);
                       return new Dictionary<string, Dictionary<string, List<NodeInfo>>>();
                   }
               });
            }
            catch (Exception ex)
            {
                Common.Utility.LogError(string.Format("Error for method GetAllResourcesInfoStartedWith for time: {0}", ex));   
            }
           

            Common.Utility.LogDebug(string.Format("End of method GetAllResourcesInfoStartedWith for time: {0}", date));

            return data;
        }

        private Dictionary<U, V> GetAllInfoStartedWith<U, V>(
                Func<Dictionary<U, V>> cacheQueryProccessor,
                Func<StatisticalBufferClient,Dictionary< U, V>> serviceCallProccessor)
        {

            Dictionary<U, V> cacheData = cacheQueryProccessor();/*cache.GetAllInfoStartedWith(date);*/
            StatisticalBufferClient farmBuffer = GetStatFarmBufferClient();
            Dictionary<U, V> bufferData = null;

            try
            {
                //todo: remake it later
                //bufferData = new Dictionary<U, V>();
                bufferData = serviceCallProccessor(farmBuffer);
            }
            finally
            {
                if (farmBuffer.State == CommunicationState.Faulted)
                {
                    farmBuffer.Abort();
                }
                else
                {
                    farmBuffer.Close();
                }
            }

            return JoinCacheAndBufferData<U, V>(cacheData, bufferData);
        }
        //this method is acceptable only if every resource has either all cacheable or all non-cacheable nodes
        private Dictionary<U,V> JoinCacheAndBufferData<U,V>(Dictionary<U,V> cacheData, Dictionary<U,V> bufferData) 
        {
            
            return cacheData.Union(bufferData).ToDictionary(x => x.Key, x => x.Value);
        }

        public ArchiveFilesTicket GetAllResourcesInfoBetween(DateTime startDate, DateTime endDate)
        {
            Common.Utility.LogDebug(string.Format("Start of method GetAllResourcesInfoBetween for startDate: {0}, endDate: {1}", startDate, endDate));

            if (!CheckDataCorrectness(startDate, endDate))
            {
                Utility.LogError("Data for StatisticalGlobalCacheServiceImpl.GetAllResourcesInfoBetween is incorrect");
                return null;
            }

            ArchiveFilesTicket result = null;
            try
            {
                result = CacheFactory.GetFactory().GetResourceDumper().GetTicketByDates(startDate, endDate);
            }
            catch (Exception ex)
            {
                Common.Utility.LogError(string.Format("Error of method GetAllResourcesInfoBetween for startDate: {0}, endDate: {1}", startDate, endDate), ex);
                throw ex;
            }

            Common.Utility.LogDebug(string.Format("End of method GetAllResourcesInfoBetween for startDate: {0}, endDate: {1}", startDate, endDate));

            return result;
        }

        public ArchiveFilesTicket GetAllTasksInfoBetween(DateTime startDate, DateTime endDate)
        {
            Common.Utility.LogDebug(string.Format("Start of method GetAllTasksInfoBetween for startDate: {0}, endDate: {1}", startDate, endDate));

            if (!CheckDataCorrectness(startDate, endDate))
            {
                Utility.LogError("Data for StatisticalGlobalCacheServiceImpl.GetAllTasksInfoBetween is incorrect");
                return null;
            }

            ArchiveFilesTicket result = null;
            try
            {
                result = CacheFactory.GetFactory().GetTaskDumper().GetTicketByDates(startDate, endDate);
            }
            catch (Exception ex)
            {
                Common.Utility.LogError(string.Format("Error of method GetAllTasksInfoBetween for startDate: {0}, endDate: {1}", startDate, endDate), ex);
                throw ex;
            }

            Common.Utility.LogDebug(string.Format("End of method GetAllTasksInfoBetween for startDate: {0}, endDate: {1}", startDate, endDate));

            return result;
        }

        public Stream GetArchiveFile(string fileName)
        {
            Common.Utility.LogDebug(string.Format("Start of method GetArchiveFile for fileName: {0}", fileName));

            if (!CheckDataCorrectness(fileName))
            {
                Utility.LogError("Data for StatisticalGlobalCacheServiceImpl.GetArchiveFile is incorrect");
                return null;
            }

            Stream result = null;

            try
            {
                result = DataDumper.GetFileByName(CacheFactory.GetFactory().GetPathToBackUpFolder(), fileName);
            }
            catch (Exception ex)
            {
                Common.Utility.LogError(string.Format("Error of method GetArchiveFile for fileName: {0}", fileName), ex);
                throw ex;
            }
            Common.Utility.LogDebug(string.Format("End of method GetArchiveFile for fileName: {0}", fileName));
            return result;

        }

        private bool CheckDataCorrectness(Dictionary<string, Dictionary<string, List<NodeInfo>>> data)
        {
            return data.All(x =>x.Key!=null && x.Value != null && x.Value.All(y => y.Key != null && y.Value != null && y.Value.All(z => z != null)));
        }

        private bool CheckDataCorrectness(Dictionary<ulong, TaskStatInfo> data)
        {
            return data.All(
                x =>x.Key != null &&
                    x.Value.ResourceName != null &&
                    x.Value.ProcessInfoCollection.All(y => y.Key != null && y.Value != null && y.Value.All(z => z != null)));
        }

        private bool CheckDataCorrectness(string fileName)
        {
            return !(string.IsNullOrEmpty(fileName) || string.IsNullOrWhiteSpace(fileName));
        }

        private bool CheckDataCorrectness(DateTime startDate, DateTime endDate)
        {
            return startDate < endDate;
        }
    }
}
