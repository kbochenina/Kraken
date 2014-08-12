using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;

using ClustersService = ServiceProxies.ClustersService;

// disable documentation-related warnings
#pragma warning disable 1591

namespace MITP
{
    public class ClusterException : Exception
    {
        public ClusterException()
            : base()
        {
        }

        public ClusterException(string message)
            : base(message)
        {
        }

        public ClusterException(ClustersService.Code errorCode)
            : this(String.Format(
                CONST.Dirty<string>("Ошибка интегратора управления кластерами: {0}"), errorCode.ToString()
            ))
        {
        }
    }

    public class ClustersProvider : AbstractResourceProvider //Metacluster
    {
        private static object _clustersServiceLock = new object();

        public override string Name
        {
            get { return CONST.Providers.Metacluster; }
        }

        //public static string GetNewProvidedTaskId()
        //{
        //    var service = EntryPointProxy.GetClustersService();
        //    ClustersService.Code errCode;
        //    ServiceProxies.ClustersService.TaskInfo taskInfo = null;

        //    lock (_clustersServiceLock)
        //    {
        //        taskInfo = service.CreateTask(out errCode);
        //    }

        //    if (errCode == ServiceProxies.ClustersService.Code.OperationSuccess)
        //        return taskInfo.TaskID;
        //    else
        //        throw new ClusterException(errCode);
        //}

        /// <summary>
        /// Get resource-specific task state and convert it to one of task's possible states
        /// </summary>
        /// <param name="providedTaskId">Task’s id specific to this resource provider</param>
        /// <returns>Tuple (task state, "fail reason or some comment to task's state")</returns>
        public override Tuple<TaskState, string> GetTaskState(ulong taskId, string providedTaskId, Resource resource, IEnumerable<NodeConfig> nodesConfig)
        {
            var service = EntryPointProxy.GetClustersService();
            ClustersService.Code errCode;
            ClustersService.TaskInfo taskInfo;

            lock (_clustersServiceLock)
            {
                taskInfo = service.GetTaskState(providedTaskId, out errCode);
            }

            if (errCode != ServiceProxies.ClustersService.Code.OperationSuccess)
                throw new ClusterException(errCode);

            if (taskInfo.State == ClustersService.TaskState.Complete)
                return Tuple.Create(TaskState.Completed, "");

            if (taskInfo.State == ClustersService.TaskState.Fail)
                return Tuple.Create(TaskState.Failed, "Failed on cluster");

            if (taskInfo.State == ClustersService.TaskState.Execute)
                return Tuple.Create(TaskState.Started, "");

            if (taskInfo.State == ClustersService.TaskState.Cancel)
                return Tuple.Create(TaskState.Aborted, "");

            return Tuple.Create(TaskState.Defined, taskInfo.State.ToString());
        }

        public override NodeState GetNodeState(ResourceNode node)
        {
            var nodeState = new NodeState();

            var service = EntryPointProxy.GetClustersService();
            ClustersService.Code errCode;
            ClustersService.ClusterStateInfo clusterStateInfo;

            lock (_clustersServiceLock)
            {
                clusterStateInfo = service.GetClusterStateInfo(node.ResourceName, out errCode);
                // GetClusterStateInfoFast is probably buggy! (doesn't update immediatly after task launch)
            }

            if (errCode != ServiceProxies.ClustersService.Code.OperationSuccess || clusterStateInfo == null)
                throw new ClusterException(errCode);

            try
            {
                var nodeStateInfo = clusterStateInfo.Node.First(stateInfo =>
                    stateInfo != null &&
					!String.IsNullOrEmpty(stateInfo.DNSName) &&
					!String.IsNullOrEmpty(node.NodeName) &&
					stateInfo.DNSName == node.NodeName
                );

                if (nodeStateInfo == null || (nodeStateInfo.TaskID != null && nodeStateInfo.TaskID.Count > 0))
                    nodeState.CoresAvailable = 0;
                else
                    nodeState.CoresAvailable = node.CoresCount;
            }
            catch
            {
                nodeState.CoresAvailable = 0;
            }

            return nodeState;
        }


        public override string Run(ulong taskId, IncarnationParams incarnation, Resource resource, IEnumerable<NodeConfig> nodesConfig)
        {
            string providedTaskId = taskId.ToString();

            var node = GetDefaultNodeSettings(resource, nodesConfig);
            var pack = node.PackageByName(incarnation.PackageName);

            var service = EntryPointProxy.GetClustersService();
            ClustersService.Code errCode;
            ClustersService.TaskInfo taskInfo;

            lock (_clustersServiceLock)
            {
                taskInfo = service.GetTaskState(providedTaskId, out errCode);
            }

            if (errCode != ServiceProxies.ClustersService.Code.OperationSuccess)
                throw new ClusterException(errCode);

            taskInfo.ClusterName = resource.ResourceName;
            taskInfo.CommandLine = String.Format(incarnation.CommandLine, pack.AppPath);
            taskInfo.PackageName = incarnation.PackageName.ToUpperInvariant();

            /*
            if (!String.IsNullOrEmpty(incarnation.StdInFile))
                taskInfo.StdinFileName = incarnation.StdInFile;
            else
                taskInfo.StdinFileName = "";

            if (!String.IsNullOrEmpty(incarnation.StdOutFile))
                taskInfo.StdoutFileName = incarnation.StdOutFile;
            else
                taskInfo.StdoutFileName = "";
            */

            // cores on nodes: {n, 0, 0} -> {n}
            taskInfo.NumberOfCores = new ClustersService.ArrayOfInt();
            taskInfo.NumberOfCores.AddRange(nodesConfig.Where(conf => conf.Cores > 0).Select(conf => conf.Cores));

            taskInfo.NumberOfNodes = taskInfo.NumberOfCores.Count;

            var logStream = new StringWriter();
            logStream.WriteLine("Задача {0} ({1}) запускается на кластере {2}",
                taskInfo.TaskID, taskInfo.PackageName, taskInfo.ClusterName);
            logStream.WriteLine("     Папка с файлами расчета: {0}", taskInfo.FTPPath);
            logStream.WriteLine("     Строка запуска: {0}", taskInfo.CommandLine);
            logStream.WriteLine("     Перенаправление вывода: {0}", taskInfo.StdoutFileName);
            logStream.Write("     Количество ядер (по каждому узлу): ");
            foreach (int coresCount in taskInfo.NumberOfCores)
                logStream.Write("{0} ", coresCount);

            Log.Info(logStream.ToString());

            lock (_clustersServiceLock)
            {
                errCode = service.ExecuteTask(taskInfo);
            }

            if (errCode != ServiceProxies.ClustersService.Code.OperationSuccess)
                throw new ClusterException(String.Format(
                    CONST.Dirty<string>("Ошибка интегратора управления кластерами при запуске задачи: {0}"), errCode.ToString()
                ));

            return providedTaskId;
        }

        public override void Abort(string providedTaskId, Resource resource, IEnumerable<NodeConfig> nodesConfig)
        {
            // todo : [5] implement ClustersProxy.Abort(clusterName)

            var service = EntryPointProxy.GetClustersService();
            ClustersService.Code errCode;

            lock (_clustersServiceLock)
            {
                service.CancelTask(providedTaskId, out errCode);
            }

            if (errCode != ClustersService.Code.OperationSuccess)
                throw new ClusterException(errCode);
        }
    }
}
