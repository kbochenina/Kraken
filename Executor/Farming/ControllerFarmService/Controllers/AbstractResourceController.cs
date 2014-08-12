using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;
using ControllerFarmService.Controllers.Error;
using MITP.Entity;

namespace MITP
{
    public abstract class AbstractResourceController // todo: static methods?
    {
        protected ConcurrentDictionary<ulong, TaskLock> locks = new ConcurrentDictionary<ulong, TaskLock>();

        //private ConcurrentStack<>
        
        protected PackageOnNode PackageByName(ResourceNode node, string packageName)
        {
            return node.Packages.First(p => String.Equals(p.Name, packageName, StringComparison.InvariantCultureIgnoreCase));
        }

        protected ResourceNode GetNode(TaskRunContext task)
        {
            return GetNode(task.Resource, task.NodesConfig);
        }

        public bool LockOperation(ulong taskId, int state)
        {
            TaskLock tlock = null;

            if (!locks.TryGetValue(taskId, out tlock))
            {
                tlock = new TaskLock { State = state, Time = DateTime.Now };

                if (!locks.TryAdd(taskId, tlock))
                {
                    if (state != TaskLock.WRITE_OPERATION_EXECUTED)
                    {
                        if (locks.TryGetValue(taskId, out tlock))
                        {
                            if (tlock.State != TaskLock.WRITE_OPERATION_EXECUTED)
                            {
                                return TaskLock.LOCK_READER;
                            }
                        }
                    }
                    throw new Exception("Some WRITE operation currently is being executed.");
                }
                return TaskLock.LOCK_HOLDER;
            }
            if (tlock.State != TaskLock.WRITE_OPERATION_EXECUTED && state != TaskLock.WRITE_OPERATION_EXECUTED)
            {
                return TaskLock.LOCK_READER;
            }
            throw new Exception("Some WRITE operation currently is being executed.");
        }

        public void UnLockOperation(ulong taskId, bool lockHolder)
        {
            TaskLock tlock = null;

            if (lockHolder && locks.TryGetValue(taskId, out tlock) && tlock.State != TaskLock.NO_OPERATION_EXECUTED)
            {
                tlock.State = TaskLock.NO_OPERATION_EXECUTED;
            } else {
                throw new Exception(String.Format("State of the task {0} operations on the controller is in inconsistency", taskId));
            }
        }

        protected ResourceNode GetNode(Resource resource, IEnumerable<NodeRunConfig> nodesConfig)
        {
            if (nodesConfig.Count() > 1)
            {
                Log.Warn(String.Format(
                    "More than one node scheduled, but resource provider doesn’t support different settings on different nodes. Using settings from node '{0}'.",
                    nodesConfig.First().NodeName
                ));
            }

            var node = resource.Nodes.First(n => n.NodeName == nodesConfig.First().NodeName);
            return node;
        }

        
    }
}