using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading;
//using System.Reflection;
using CommonDataTypes;
using CommonDataTypes.RExService.Service.Entity.Info;
using ControllerFarmService.Controllers;
using ControllerFarmService.Installation.Interfaces;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;
using ControllerFarmService.Statistic;
using ControllerFarmService.Installation;
using ServiceProxies.ExtStatService;
using ServiceProxies.StatGlobalCache;
using Config = System.Configuration.ConfigurationManager;
using PFX = System.Threading.Tasks;
using ControllerFarmService.Installation;
//using NLog;


namespace MITP
{
    [ServiceBehavior(Namespace = "http://escience.ifmo.ru/nano/services/", InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    //[ServiceBehavior(Namespace = "http://escience.ifmo.ru/nano/services/", InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Single)]
    public class ControllerFarmService : IControllerFarmService, IStatisticalBuffer, IInstallationService 
    {
        public const string FARMID_PARAM_NAME = "FarmId";

        private static ReaderWriterLockSlim _resourcesLock = new ReaderWriterLockSlim();

        //private static Logger Log = LogManager.GetCurrentClassLogger();

        private void CheckNodeConfigConsistency(ulong taskId, IEnumerable<NodeRunConfig> config, Resource resource)
        {
            if (config.Any(node => node.ResourceName != resource.ResourceName))
            {
                Log.Error("Node configs have different resources: " + String.Join(", ", config.Select(c => c.ResourceName)));
                throw new ArgumentException("All node configs should have the same resource name");
            }

            var unknownNodes = config.Select(n => n.NodeName).Except(resource.Nodes.Select(n => n.NodeName));
            if (unknownNodes.Any())
            {
                Log.Error(String.Format(
                    "Task {0} has unknown nodes for resource {1}: {2}",
                    taskId, resource.ResourceName, String.Join(", ", unknownNodes)
                ));
                throw new Exception("Wrong node config for task " + taskId.ToString() + ": " + String.Join(", ", unknownNodes));
            }
        }

        public void Run(TaskRunContext task)
        {
            _resourcesLock.EnterReadLock();

            try
            {
                Log.Info("Running task " + task.ToString());

                string resourceName = task.NodesConfig.First().ResourceName;
                var resourceCache = 
                    ResourceCache.GetByName(resourceName);

                lock (resourceCache.StateLock)
                {
                    CheckNodeConfigConsistency(task.TaskId, task.NodesConfig, resourceCache.Resource);

                    task.Resource = resourceCache.Resource;
                    task.Controller = resourceCache.Controller;
                }

                try
                {
                    resourceCache.Acquire(task.NodesConfig);  // todo : m.b. move under resourceCache.StateLock?

                    Log.Info(String.Format("Trying to run task {0} on resource {1}", task.TaskId, task.Resource.ResourceName));

                    task.LocalId = task.Controller.Run(task);

                    Log.Info(String.Format("Task {0} ({1}) started on resource {2} with localId = {3}",
                        task.TaskId, task.PackageName, task.Resource.ResourceName, task.LocalId
                    ));

                    var state = new TaskStateInfo(TaskState.Started, task.LocalId.ToString());
                    TaskCache.AddTask(task, state);
                }
                catch (Exception e)
                {
                    resourceCache.Release(task.NodesConfig);

                    Log.Error(String.Format("Unable to run task {0}: {1}", task.TaskId, e));
                    throw;
                }
            }
            catch (Exception e)
            {
                Log.Error(String.Format("Exception on Farm.Run(task {0}): {1}", task.TaskId, e));
                throw;
            }
            finally
            {
                _resourcesLock.ExitReadLock();
            }

            
            //todo for mock
            if (CacheCollectorFactory.CheckMockMode())
            {
                CacheCollectorFactory.GetInstance().SendTask(task);
            }
        }

        public void Abort(ulong taskId)
        {
            _resourcesLock.EnterReadLock();

            try
            {
                Log.Info("Aborting task " + taskId.ToString());

                var task = TaskCache.GetById(taskId);
                task.UpdateStateAsync();

                lock (task.StateLock)
                {
                    if (task.StateInfo.State == TaskState.Started)
                    {
                        task.Context.Controller.Abort(task.Context);
                        Log.Info("Task aborted: " + taskId.ToString());

                        task.SetState(TaskState.Aborted, "Aborted by request"); // autorelease resources
                    }
                    else
                    {
                        Log.Warn("Task was not started: " + taskId.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(String.Format("Error on aborting task {0}: {1}}", taskId, e));
                throw;
            }
            finally
            {
                _resourcesLock.ExitReadLock();
            }
        }

        public TaskStateInfo GetTaskStateInfo(ulong taskId)
        {
            _resourcesLock.EnterReadLock();

            try
            {
                var task = TaskCache.GetById(taskId);
                task.UpdateStateAsync();

                lock (task.StateInfo)
                {
                    var taskState = new TaskStateInfo(task.StateInfo);
                    return taskState;
                }
            }
            catch (Exception e)
            {
                Log.Error(String.Format("Error on getting task {0} state info: {1}", taskId, e));
                throw;
            }
            finally
            {
                _resourcesLock.ExitReadLock();
            }
        }

        public NodeStateInfo[] GetNodesState(string resourceName)
        {
            _resourcesLock.EnterReadLock();

            try
            {
                ResourceCache.UpdateNodesState(resourceName); // todo : unify. TaskCache.UpdateState(id) OR task.UpdateState()
                var resourceCache = ResourceCache.GetByName(resourceName);

                lock (resourceCache.StateLock)
                {
                    var nodesState = resourceCache.NodeStateInfo.Select(state => new NodeStateInfo(state)).ToArray();
                    return nodesState;
                }
            }
            catch (Exception e)
            {
                Log.Error(String.Format("Error on getting resource '{0}' state info: {1}", resourceName, e));
                throw;
            }
            finally
            {
                _resourcesLock.ExitReadLock();
            }
        }

        public ulong[] GetActiveTaskIds()
        {
            _resourcesLock.EnterReadLock();

            try
            {
                ulong[] ids = TaskCache.GetActiveTaskIds();
                return ids;
            }
            catch (Exception e)
            {
                Log.Error("Error on getting active tasks ids: " + e.ToString());
                throw;
            }
            finally
            {
                _resourcesLock.ExitReadLock();
            }
        }

        public string[] GetActiveResourceNames()
        {
            _resourcesLock.EnterReadLock();

            try
            {
                string[] names = ResourceCache.GetActiveResourceNames();
                return names;
            }
            catch (Exception e)
            {
                Log.Error("Error on getting active resource names: " + e.ToString());
                throw;
            }
            finally
            {
                _resourcesLock.ExitReadLock();
            }
        }

        public void ReloadAllResources(string dumpingKey = null)
        {
            PFX.Task.Factory.StartNew(() =>
            {
                _resourcesLock.EnterWriteLock(); // the only update (i.e. write) to resources

                try
                {
                    Log.Info("Reloading resources for controller");
                    Console.WriteLine("Reloading resources for controller");

                    TaskCache.DumpAllTasks();

                    var resourceBase = new ResourceBaseServiceClient();

                    try
                    {
                        string farmId = Config.AppSettings[FARMID_PARAM_NAME];

                        var resources = resourceBase.GetResourcesForFarm(farmId, dumpingKey); // waits all other dumps
                        resourceBase.Close();

                        string[] resourceNames = resources.Select(r => r.ResourceName).ToArray();
                        Log.Info("Resources to reload for farm " + farmId + ": " + String.Join(", ", resourceNames));

                        ResourceCache.ReloadResources(resources);
                        TaskCache.RestoreTasks(resourceNames);
                        //TaskCache.ReloadTasks(resourceNames);

                        PFX.Parallel.ForEach(resourceNames, (name) =>
                        {
                            ResourceCache.UpdateNodesState(name);
                        });

                        Log.Info("Resource reloading done for farm " + farmId);
                    }
                    catch (Exception e)
                    {
                        resourceBase.Abort();
                        Log.Error("Exception on reloading resources: " + e.ToString());
                        throw;
                    }
                }
                finally
                {
                    _resourcesLock.ExitWriteLock();
                }
            });
        }

        ControllerFarmService()
        {
            bool loaded = false;
            //for (int retries = 0; !loaded && retries < 3; retries++)
            {
                try
                {
                    ReloadAllResources();
                    loaded = true;
                }
                catch (Exception e)
                {
                    Log.Error("Could not load resources on server start. Retying. " + e.ToString());
                    System.Threading.Thread.Sleep(200);
                }
            }

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Log.Error("Exception in some TPL's task: " + e.ToString());
                e.SetObserved();
                //throw e.Exception;
            };

            //todo it was made for a mock creating task #1267
            if (CacheCollectorFactory.CheckMockMode())
            {
                CacheCollectorFactory.GetInstance().RunWithFarm(this);
            }
        }

        private ResourceCache[] GetActiveResources()
        {
            _resourcesLock.EnterReadLock();

            try
            {
                ResourceCache[] resources = ResourceCache.GetActiveResources();
                return resources;
            }
            catch (Exception e)
            {
                Log.Error("Error on getting active resources: " + e.ToString());
                throw;
            }
            finally
            {
                _resourcesLock.ExitReadLock();
            }
        }

        //for batch-supported resources (for the current moment only for windows resources)
        //List<NodeInfo> is a list of measurement started with startTime
        //List<List<NodeInfo>> is a list of measurements for all nodes
        public Dictionary<string, Dictionary<string, List<NodeInfo>>> GetAllResourcesInfoStartWith(DateTime startTime)
        {

            ResourceCache[] resources = GetActiveResources();

            Dictionary<string, Dictionary<string, List<NodeInfo>>> data = new Dictionary<string, Dictionary<string, List<NodeInfo>>>();

            foreach (var resource in resources)
            {
                IStatisticalCacheableController controller = resource.Controller as IStatisticalCacheableController;
                if (controller != null)
                {
                    data[resource.Resource.ResourceName] = GetResourceInfoStartWith(resource.Resource, controller, startTime);
                }
            }

            return data; 
        }



        private Dictionary<string, List<NodeInfo>> GetResourceInfoStartWith(Resource resource, IStatisticalCacheableController controller, DateTime startTime)
        {
            return controller.GetResourceInfoStartWith(resource, startTime);
        }

        public Dictionary<string, Dictionary<string, List<NodeInfo>>> GetAllCacheableResourcesInfoStartedWith(DateTime date)
        {
            if (CacheCollectorFactory.CheckMockMode())
            {
                Common.Utility.LogInfo("Mock Mode enabled. nothing to return.");
                return new Dictionary<string, Dictionary<string, List<NodeInfo>>>();
            }
            
            return GetAllResourcesInfoStartWith(date);
        }

        public TaskStatInfo GetTaskInfoStartedWith(ulong taskId, DateTime date)
        {
            //todo uncomment it later when rex will have been repaired
            return new TaskStatInfo(new Dictionary<string, List<ProcessStatInfo>>(), "empty");
            return GetCacheableInfoForTask(taskId, date);
        }

        public Dictionary<ulong, TaskStatInfo> GetAllCacheableTaskInfoStartedWith(DateTime date)
        {
            if (CacheCollectorFactory.CheckMockMode())
            {
                Common.Utility.LogInfo("Mock Mode enabled. nothing to return.");
                return new Dictionary<ulong, TaskStatInfo>();
            }

            ulong[] ids = GetActiveTaskIds();
            Dictionary<ulong, TaskStatInfo> data = new Dictionary<ulong, TaskStatInfo>();

            //todo uncomment it later when rex will have been repaired
            return data;
             foreach (var id in ids)
             {
                 var statInfo = GetCacheableInfoForTask(id, date);
                 if (statInfo != null)
                 {
                     data.Add(id, statInfo);    
                 }
             }
            return data; 
        }

        private TaskStatInfo GetCacheableInfoForTask(ulong id, DateTime date)
        {
            var task = TaskCache.GetById(id);

            //todo ask Sergey or Denis about states
            IStatisticalCacheableController controller = task.Context.Controller as IStatisticalCacheableController;
            if (controller != null)
            {
                var infos = controller.GetTaskInfoStartWith(id, date, task.Context);
                var statInfo = new TaskStatInfo(infos, task.Context.Resource.ResourceName);

                return statInfo;
            }

            return null;
        }

        public void InstallPackage(InstallationTicket ticket)
        {
            Common.Utility.ExceptionablePlaceWrapper(() =>
            {
                var resource = ResourceCache.GetByName(ticket.ResourceName);

                InstallationUtility.RouteToController(resource, ticket);

            }, "Error at installation start. TicketId: " + ticket.Id,"Installation start was successful. TicketId: " + ticket.Id);
        }
    }
}


