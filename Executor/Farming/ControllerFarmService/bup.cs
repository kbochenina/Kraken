using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Reflection;

namespace MITP
{
    //[ServiceBehavior(Namespace = "http://escience.ifmo.ru/nano/services/", InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class ControllerFarmService : IControllerFarmService
    {
        private class TaskCache
        {
            public const TimeSpan UPDATE_INTERVAL = TimeSpan.FromMilliseconds(300);

            private static readonly Dictionary<ulong, TaskCache> _cache = new Dictionary<ulong, TaskCache>();
            private static readonly object _globalLock = new object();

            private readonly object _lock = new object();

            public TaskRunContext Context { get; private set; }
            public TaskStateInfo StateInfo { get; private set; }       // mutable

            private bool _isUpdating { get; private set; }             // mutable
            private DateTime _lastUpdateTime { get; private set; }     // mutable

            private TaskCache(TaskRunContext context, TaskState state = TaskState.Started, string stateComment = "")
            {
                Context = context;
                StateInfo = new TaskStateInfo(state, stateComment);

                _isUpdating = false;
                _lastUpdateTime = DateTime.Now - UPDATE_INTERVAL - TimeSpan.FromMilliseconds(50);
            }

            private TaskCache(TaskRunContext context, TaskStateInfo state)
            {
                Context = context;
                StateInfo = state;

                _isUpdating = false;
                _lastUpdateTime = DateTime.Now;
            }

            public static void AddTask(TaskRunContext context, TaskState state = TaskState.Started, string stateComment = "")
            {
                var taskCache = new TaskCache(context, state, stateComment);

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
                    taskCache = _cache[taskId];
                }
                return taskCache;
            }

            public static void UpdateStateInfo(ulong taskId, Func<TaskRunContext, TaskStateInfo> updateFunc)
            {
                var cache = GetById(taskId);

                if (!cache._isUpdating && (cache._lastUpdateTime + UPDATE_INTERVAL < DateTime.Now || DateTime.Now < cache._lastUpdateTime))
                {
                    lock (cache._lock)
                    {
                        if (!cache._isUpdating && (cache._lastUpdateTime + UPDATE_INTERVAL < DateTime.Now || DateTime.Now < cache._lastUpdateTime))
                        {
                            cache._isUpdating = true;

                            var newState = updateFunc(cache.Context);
                            var newCache = new TaskCache(cache.Context, newState);

                            lock (_globalLock) // unnecessary?
                            {
                                _cache[taskId] = newCache;
                            }
                        }
                    }
                }
            }
        }




        private static readonly object _tasksCacheLock = new object();
        private static readonly Dictionary<ulong, TaskRunContext> _tasksCache = new Dictionary<ulong, TaskRunContext>();

        private static readonly object _resourcesCacheLock = new object();
        private static Dictionary<string, Resource> _resourcesCache = new Dictionary<string, Resource>();
        private static Dictionary<string, IStatelessResourceController> _controllers = new Dictionary<string, IStatelessResourceController>();

        private static Dictionary<string, NodeStateInfo[]> _nodeStateCache = new Dictionary<string, NodeStateInfo[]>();



        public void Run(TaskRunContext task)
        {
            lock (task.Lock)
            {
                Log.Info("Running task " + task.ToString());

                string resourceName = task.NodesConfig.First().ResourceName;
                if (task.NodesConfig.Any(node => node.ResourceName != resourceName))
                {
                    Log.Error("Node configs have different resources: " + String.Join(", ", task.NodesConfig.Select(c => c.ResourceName)));
                    throw new ArgumentException("All node configs should have the same resource name");
                }

                lock (_resourcesCacheLock)
                {
                    if (!_resourcesCache.ContainsKey(resourceName))
                    {
                        Log.Error("No controller for resource " + resourceName);
                        throw new ArgumentException("No such resource controller");
                    }

                    var unknownNodes = task.NodesConfig.Select(n => n.NodeName).Except(_nodeStateCache[resourceName].Select(n => n.NodeName));
                    if (unknownNodes.Any())
                    {
                        Log.Error(String.Format(
                            "Task {0} has unknown nodes for resource {1}: {2}",
                            task.TaskId, resourceName, String.Join(", ", unknownNodes)
                        ));
                        throw new Exception("Wrong node config for task " + task.TaskId.ToString() + ": " + String.Join(", ", unknownNodes));
                    }

                    task.Resource = _resourcesCache[resourceName];
                    task.Controller = _controllers[resourceName];
                }

                // POSSIBLE DATA RACE?! if state is inside controller
                SubmitTask(task);

                lock (_tasksCacheLock)
                {
                    _tasksCache.Add(task.TaskId, task);
                }
            }
        }

        private void SubmitTask(TaskRunContext task)
        {
            lock (task.Lock)
            {
                try
                {
                    lock (_nodeStateCacheLock)
                    {
                        bool nodesOverloaded = false;

                        var nodeStates = _nodeStateCache[task.Resource.ResourceName];
                        foreach (var nodeConfig in task.NodesConfig)
                        {
                            var nodeState = nodeStates.Single(n => n.NodeName == nodeConfig.NodeName);

                            if (nodeState.CoresAvailable <= nodeConfig.Cores)
                                nodesOverloaded = true;

                            nodeState.TasksSubmitted++;
                            nodeState.CoresReserved += nodeConfig.Cores;
                        }

                        if (nodesOverloaded)
                        {
                            Log.Error("Nodes overload for resource " + task.Resource.ResourceName);
                            throw new Exception("Wrong config for task " + task.TaskId.ToString() + ". Selected nodes are overloaded");
                        }
                    }

                    task.LocalId = task.Controller.Run(task);
                    task.CachedRunInfo = new TaskRunInfo(TaskState.Started);
                }
                catch (Exception e)
                {
                    RevokeTask(task);

                    Log.Error(String.Format("Unable to run task {1}: {2}{0}{3}", Environment.NewLine,
                        task.TaskId, e.Message, e.StackTrace
                    ));

                    throw;
                }
            }
        }

        private void RevokeTask(TaskRunContext task)
        {
            lock (task.Lock)
            {
                lock (_nodeStateCacheLock)
                {
                    var nodeStates = _nodeStateCache[task.Resource.ResourceName];
                    foreach (var nodeConfig in task.NodesConfig)
                    {
                        var nodeState = nodeStates.Single(n => n.NodeName == nodeConfig.NodeName);

                        nodeState.TasksSubmitted--;
                        nodeState.CoresReserved -= nodeConfig.Cores;

                        if (nodeState.TasksSubmitted < 0)
                        {
                            Log.Warn();
                            nodeState.TasksSubmitted = 0;
                        }

                        if (nodeState.CoresReserved < 0)
                        {
                            Log.Warn();
                            nodeState.CoresReserved = 0;
                        }
                    }
                }
            }
        }

        public void Abort(ulong taskId)
        {
            var task = GetTaskRunContext(taskId);

            // POSSIBLE DATA RACE?!
            lock (task.Lock)
            {
                task.Controller.Abort(task);

                task.RunInfo.State = TaskState.Aborted;
                task.RunInfo.StateComment = "Aborted by request";

                RevokeTask(task);
            }
        }

        public TaskRunInfo GetTaskRunInfo(ulong taskId)
        {
            var task = GetTaskRunContext(taskId);

            // POSSIBLE DATA RACE?!
            if (!task.CachedRunInfo.IsUpdating && task.CachedRunInfo.LastUpdateTime + TaskRunInfo.UPDATE_INTERVAL < DateTime.Now)
            {
                lock (task.Lock)
                {
                    if (!task.CachedRunInfo.IsUpdating)
                    {
                        task.CachedRunInfo = task.Controller.GetTaskRunInfo(task);
                    }
                }
            }

            return task.CachedRunInfo;
        }

        private void UpdateNodesState(string resourceName)
        {
            IStatelessResourceController controller;
            Resource resource;
            lock (_resourcesCacheLock)
            {
                controller = _controllers[resourceName];
                resource = _resourcesCache[resourceName];

                controller.GetNodesState(resource);
            }
        }

        public NodeState[] GetNodesState(string resourceName)
        {
            NodeState[] nodeStates;

            lock (_resourcesCacheLock)
            {
                if (!_resourcesCache.ContainsKey(resourceName))
                    throw new Exception("No such resource on this farm");

                UpdateNodesState(resourceName);

                nodeStates = _nodeStateCache[resourceName].Select(ns => new NodeState(ns)).ToArray();
            }

            return nodeStates;
        }

        public void ReloadAllResources()
        {
            //using (var resourceBase = 
            //var resourceNames = 

            lock (_resourcesCacheLock)
            {
                _resourcesCache = Resource.Load().ToDictionary(res => res.ResourceName);
                UpdateControllers(_resourcesCache.Values);

                foreach (string resourceName in _resourcesCache.Keys)
                    UpdateNodesState(resourceName);
            }
        }

        private void UpdateControllers(IEnumerable<Resource> resources)
        {
            // controllers for old resources remains in Dictionary => may process queued tasks

            // todo : MEF

            lock (_resourcesCacheLock)
            {
                var assembly = Assembly.GetAssembly(typeof(IStatelessResourceController));
                var controllerTypes = assembly.GetExportedTypes().Where(type => type.IsClass && !type.IsAbstract && type.GetInterfaces().Contains(typeof(IResourceController)));

                foreach (var resource in resources)
                {
                    var controllerType = controllerTypes.Single(t => t.Name.ToLowerInvariant() == resource.ProviderName.ToLowerInvariant());
                    var controller = (IStatelessResourceController)controllerType.GetConstructor(Type.EmptyTypes).Invoke(null);
                    _controllers[resource.ResourceName] = controller;
                }
            }
        }

        public void ReloadResource(string resourceName)
        {
            throw new NotImplementedException();
        }
    }
}














/*****************************************************************************************************************/










using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Reflection;

namespace MITP
{
    //[ServiceBehavior(Namespace = "http://escience.ifmo.ru/nano/services/", InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class ControllerFarmService : IControllerFarmService
    {
        #region Run helper routines

        private void CheckNodeConfigConsistency(ulong taskId, IEnumerable<NodeConfig> config, Resource resource)
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

        private TaskStateInfo SubmitTask(TaskRunContext task, ResourceCache resourceCache)
        {
            TaskStateInfo taskStateInfo;

            lock (resourceCache.StateLock)
            {
                try
                {
                    bool nodesOverloaded = false;

                    var nodeStates = resourceCache.NodeStateInfo;
                    foreach (var nodeConfig in task.NodesConfig)
                    {
                        var nodeState = nodeStates.Single(n => n.NodeName == nodeConfig.NodeName);

                        if (nodeState.CoresAvailable <= nodeConfig.Cores)
                            nodesOverloaded = true;

                        nodeState.TasksSubmitted++;
                        nodeState.CoresReserved += nodeConfig.Cores;
                    }

                    if (nodesOverloaded)
                    {
                        Log.Error("Nodes overload for resource " + task.Resource.ResourceName);
                        throw new Exception("Wrong config for task " + task.TaskId.ToString() + ". Selected nodes are overloaded");
                    }

                    task.LocalId = task.Controller.Run(task);
                    taskStateInfo = new TaskStateInfo(TaskState.Started, task.LocalId.ToString());
                }
                catch (Exception e)
                {
                    RevokeTask(task, resourceCache);

                    Log.Error(String.Format("Unable to run task {1}: {2}{0}{3}", Environment.NewLine,
                        task.TaskId, e.Message, e.StackTrace
                    ));

                    throw;
                }
            }

            return taskStateInfo;
        }

        private void RevokeTask(TaskRunContext task, ResourceCache resourceCahce)
        {
            lock (resourceCahce.StateLock)
            {
                var nodeStates = resourceCahce.NodeStateInfo;
                foreach (var nodeConfig in task.NodesConfig)
                {
                    var nodeState = nodeStates.Single(n => n.NodeName == nodeConfig.NodeName);

                    nodeState.TasksSubmitted--;
                    nodeState.CoresReserved -= nodeConfig.Cores;

                    if (nodeState.TasksSubmitted < 0)
                    {
                        Log.Warn();
                        nodeState.TasksSubmitted = 0;
                    }

                    if (nodeState.CoresReserved < 0)
                    {
                        Log.Warn();
                        nodeState.CoresReserved = 0;
                    }
                }
            }
        }

        #endregion

        public void Run(TaskRunContext task)
        {
            Log.Info("Running task " + task.ToString());

            string resourceName = task.NodesConfig.First().ResourceName;
            var resourceCache = ResourceCache.GetByName(resourceName);

            lock (resourceCache.StateLock)
            {
                CheckNodeConfigConsistency(task.TaskId, task.NodesConfig, resourceCache.Resource);

                task.Resource = resourceCache.Resource;
                task.Controller = resourceCache.Controller;

                var state = SubmitTask(task, resourceCache);

                TaskCache.AddTask(task, state);
            }
        }

        public void Abort(ulong taskId)
        {
            var task = GetTaskRunContext(taskId);

            // POSSIBLE DATA RACE?!
            lock (task.Lock)
            {
                task.Controller.Abort(task);

                task.RunInfo.State = TaskState.Aborted;
                task.RunInfo.StateComment = "Aborted by request";

                RevokeTask(task);                
            }
        }

        public TaskRunInfo GetTaskRunInfo(ulong taskId)
        {
            var task = GetTaskRunContext(taskId);

            // POSSIBLE DATA RACE?!
            if (!task.CachedRunInfo.IsUpdating && task.CachedRunInfo.LastUpdateTime + TaskRunInfo.UPDATE_INTERVAL < DateTime.Now)
            {
                lock (task.Lock)
                {
                    if (!task.CachedRunInfo.IsUpdating)
                    {
                        task.CachedRunInfo = task.Controller.GetTaskRunInfo(task);
                    }
                }
            }

            return task.CachedRunInfo; 
        }

        private void UpdateNodesState(string resourceName)
        {
            IStatelessResourceController controller;
            Resource resource;
            lock (_resourcesCacheLock)
            {
                controller = _controllers[resourceName];
                resource = _resourcesCache[resourceName];

                controller.GetNodesState(resource);
            }
        }
        
        public NodeStateInfo[] GetNodesState(string resourceName)
        {
            NodeState[] nodeStates;

            lock (_resourcesCacheLock)
            {
                if (!_resourcesCache.ContainsKey(resourceName))
                    throw new Exception("No such resource on this farm");

                UpdateNodesState(resourceName);

                nodeStates = _nodeStateCache[resourceName].Select(ns => new NodeState(ns)).ToArray();
            }

            return nodeStates;
        }

        public void ReloadAllResources()
        {
            //using (var resourceBase = 
            //var resourceNames = 

            lock (_resourcesCacheLock)
            {
                _resourcesCache = Resource.Load().ToDictionary(res => res.ResourceName);
                UpdateControllers(_resourcesCache.Values);

                foreach (string resourceName in _resourcesCache.Keys)
                    UpdateNodesState(resourceName);
            }
        }

        private void UpdateControllers(IEnumerable<Resource> resources)
        {
            // controllers for old resources remains in Dictionary => may process queued tasks

            // todo : MEF

            lock (_resourcesCacheLock)
            {
                var assembly = Assembly.GetAssembly(typeof(IStatelessResourceController));
                var controllerTypes = assembly.GetExportedTypes().Where(type => type.IsClass && !type.IsAbstract && type.GetInterfaces().Contains(typeof(IResourceController)));

                foreach (var resource in resources)
                {
                    var controllerType = controllerTypes.Single(t => t.Name.ToLowerInvariant() == resource.ProviderName.ToLowerInvariant());
                    var controller = (IStatelessResourceController) controllerType.GetConstructor(Type.EmptyTypes).Invoke(null);
                    _controllers[resource.ResourceName] = controller;                    
                }
            }
        }

        public void ReloadResource(string resourceName)
        {
            throw new NotImplementedException();
        }
    }
}






















/*****************************************************************************************/



        private abstract class SshCommands
        {
            public const string Run = "nohup {0} &";
            public const string Abort = "qdel {0}";
            public const string GetTaskState = "qstat -f {0}";
            public const string GetNodeState = "pbsnodes -x";
        }

        private string SshExec(ResourceNode node, string command, string args = "")
        {
            lock (_lock)
            {
                var sshExec = new Ssh.SshExec(node.NodeAddress, node.Credentials.Username, node.Credentials.Password);
                sshExec.Connect();

                string sshOut = "";
                string sshErr = "";
                string sshCommand = command + " " + args;
                int sshResult = sshExec.RunCommand(sshCommand, ref sshOut, ref sshErr);
                sshExec.Close();

                sshErr = sshErr.Replace('.', ' '); // Cert creation emits many dots
                if (!String.IsNullOrWhiteSpace(sshErr))
                    throw new Exception(String.Format("Ssh execution error. Command: \"{0}\". Code: {1}, StdOut: {2}, StdErr: {3}", sshCommand, sshResult, sshOut, sshErr));
            }
            return sshOut;
            
        }

        private void ScpCopy(ResourceNode node, string remotePath, string localPath)
        {
            var scp = new Ssh.Scp(node.NodeAddress, node.Credentials.Username, node.Credentials.Password);

            scp.Connect();
            scp.Recursive = true;
            scp.Put(localPath, remotePath);
            scp.Close();
        }


        public object Run(TaskRunContext task)
        {
            lock (_lock)
            {
                throw new NotImplementedException();
            }
        }

        public void Abort(TaskRunContext task)
        {
            lock (_lock)
            {
                Log.Info("Trying to abort task " + (string) task.LocalId);
                var node = GetNode(task.Resource, task.NodesConfig);
                string result = SshExec(node, SshCommands.Abort, (string) task.LocalId);
                Log.Info(result);
            }
        }

        public TaskState GetTaskState(TaskRunContext task)
        {
            lock (_lock)
            {
                var node = GetNode(task.Resource, task.NodesConfig);
                string result = SshExec(node, SshCommands.GetTaskState, (string)task.LocalId).ToLowerInvariant();

                if (result.Contains("running") || result.Contains("queued"))
                    return TaskState.Started;
                // esle if (Aborted, Failed)
                else
                    return TaskState.Completed;

            }
        }

        public NodeState[] GetNodeStates(Resource resource)
        {
            lock (_lock)
            {
                string responseString = SshExec(resource.Nodes.First(), SshCommands.GetTaskState).ToLowerInvariant();
                var responseXml = XElement.Parse(responseString);

                var freeNodeNames = responseXml.Element("data").Elements("node")
                    .Where(node => node.Element("state").Value == "free")
                    .Select(node => node.Element("name").Value);

                var nodeStates = resource.Nodes.Select(node => 
                    freeNodeNames.Contains(node.NodeName)? 
                        new NodeState() { CoresAvailable = node.CoresCount } :
                        new NodeState() { CoresAvailable = 0 }
                ).ToArray();

                return nodeStates;
            }
        }

