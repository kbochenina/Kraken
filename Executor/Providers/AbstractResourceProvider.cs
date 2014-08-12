using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// disable documentation-related warnings
#pragma warning disable 1591

namespace MITP
{
    public abstract class AbstractResourceProvider
	{
        public abstract string Name { get; }

        /// <summary>
        /// Get resource-specific task state and convert it to one of task's possible states
        /// </summary>
        /// <param name="taskId">Task's id in system</param>        
        /// <param name="providedTaskId">Task's id specific to this resource provider</param>
        /// <returns>Tuple (task state, "fail reason or some comment to task's state")</returns>
        public abstract Tuple<TaskState, string> GetTaskState(ulong taskId, string providedTaskId, Resource resource, IEnumerable<NodeConfig> nodesConfig);

        public abstract NodeState GetNodeState(ResourceNode node);

        /// <summary>
        /// Run task on selected resource
        /// </summary>
        /// <param name="incarnation"></param>
        /// <param name="resource"></param>
        /// <returns>Provided task id (unique for resource)</returns>
        public abstract string Run(ulong taskId, IncarnationParams incarnation, Resource resource, IEnumerable<NodeConfig> nodesConfig);

        public abstract void Abort(string providedTaskId, Resource resource, IEnumerable<NodeConfig> nodesConfig);

        public ResourceNode GetDefaultNodeSettings(Resource resource, IEnumerable<NodeConfig> nodesConfig)
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
