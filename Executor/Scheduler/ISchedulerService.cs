using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace MITP
{
    [ServiceContract]
    public interface ISchedulerService
    {
        [OperationContract]
        Estimation GetCurrentEstimation(ulong taskId);

        [OperationContract]
        Schedule ReSchedule(); // todo : ReSchedule(TaskDesription[], TaskDependencies[])
    }

    [DataContract]
    public class Schedule
    {
        // todo : Schedule class

        public void AssignTo(Estimation estimation)
        {
            if (estimation == null)
            {
                this.Time.Estimated = null;
                _assignedTo = new ClusterConfig(null, null);
                _state = TaskState.NotAssigned;
            }
            else
                AssignTo(estimation.ClusterName, estimation.Cores, estimation.Time);
        }

        public void AssignTo(string clusterName, int[] cores, TimeSpan time)
        {
            this.Time.Estimated = time;
            _assignedTo = new ClusterConfig(clusterName, cores);
            _state = TaskState.Assigned;
        }

        public void AssignTo(string clusterName)
        {
            ResourceNode[] nodes = ClustersProxy.GetNodes(clusterName);

            for (int i = 0; i < nodes.Length; i++)
            {
                // no package = no available cores on this node
                if (!nodes[i].HavePackage(Package))
                    nodes[i].CoresAvailable = 0;
            }

            Estimation minEst = null;
            foreach (LaunchModel model in TaskManager.LaunchModels)
            {
                Estimation curEst = model.EstimateIfMathces(this, nodes);

                if (model.Mathces(this))
                {
                    Log.Info(String.Format(
                        "Задача {1}, на кластере {2} получена оценка в {3} очков от модели запуска {0}",
                            model.ToString(), this.TaskId, nodes[0].ClusterName,
                            (curEst == null)? "--" : "" + ((int) curEst.Time.TotalSeconds).ToString()
                    ));
                }

                if (curEst != null && (minEst == null || curEst.Time < minEst.Time))
                    minEst = new Estimation(curEst.Time, curEst.ClusterName, curEst.Cores);
            }

            AssignTo(minEst);
        }

        public void AutoAssign()
        {
            string[] clusters = ClustersProxy.GetClusterNames();
            Estimation minEst = null;

            foreach (string cluster in clusters)
            {
                AssignTo(cluster);

                if (this.Time.Estimated.HasValue)
                {
                    TimeSpan curTime = this.Time.Estimated.Value;

                    if (minEst == null || curTime < minEst.Time)
                        minEst = new Estimation(curTime, this.AssignedTo);
                }
            }

            AssignTo(minEst);
        }


    }
}
