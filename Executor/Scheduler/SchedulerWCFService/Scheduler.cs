using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using Common;
using Scheduler;
using TimeMeter;

namespace SchedulerWCFService
{
    public class Scheduler : IScheduler
    {
        private TaskScheduler _scheduler;
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public Scheduler()
        {
            try
            {
                _scheduler = new TaskScheduler();
            }
            catch (IOException e)
            {
                throw new RemoteException(ErrorCode.IO, "Failed to create Scheduler object", e.Message);
            }
            catch (ConfigurationException e)
            {
                throw new RemoteException(ErrorCode.CONFIGURATION, "Failed to create Scheduler object", e.Message);
            }
            catch (Exception e)
            {
                logger.Log(NLog.LogLevel.Error, String.Format("Scheduler fail {0}", e.Message + (e.InnerException != null ? e.InnerException.Message : "")), e.Message);
                throw new RemoteException(ErrorCode.CONFIGURATION, "Failed to create Scheduler object", e.Message);
            }
        }

        public TaskScheduler.LaunchPlan Reschedule(TaskScheduler.Workflow workflow)
        {
            throw new ActionNotSupportedException("This method is not supported anymore - you must use RescheduleEstimated instead.");
        }


        public IEnumerable<string> GetClusterNames()
        {
            return _scheduler.GetClusterNames();
        }

        public IEnumerable<string> GetAppNames()
        {
            return _scheduler.GetAppNames();
        }


        public TaskScheduler.Workflow CreateTestWF()
        {
            var wf = new TaskScheduler.Workflow();
            var cls = GetClusterNames();
            wf.Resources = new Resource[0]; //cls.Select(name => TaskTimeMeter.CreateResourceRecord(name)).ToArray();
            wf.Resources = wf.Resources.Union(new List<Resource>
            {
                new Resource{
                    Name = "VM1",
                    Nodes = new[] {
                        new Node {
                            DNSName = "machine",
                            CoresAvailable = 1,
                            ResourceName = "VM1",
                            Parameters = new Dictionary<string, string>() {
                                {"IS_VIRTUAL", "True"},
                                { "SERVICE_URL", "http://192.168.9.10:8787/RExService" },
                                { VirtualConnectionParams.VCP, Newtonsoft.Json.JsonConvert.SerializeObject(new global::Common.VManager.DataTypes.HostConnectionParams()
                                    {
                                        Name = "lo",
                                        Type = "VBOX",
                                        Parameters = new Dictionary<string,string>()
                                    })}
                            }
                        }
                    },
                    Parameters = new Dictionary<string, string>()
                }
            }).ToArray();
            wf.ResourcesNames = new string[0];
            var ss = GetAppNames();
            ulong i = 0;
            for (var j = 0; j < 5 && j < ss.Count(); j++)
            {
                var task = new TaskScheduler.Task();
                task.ApplicationName = ss.ElementAt(j);
                task.Parameters = new Dictionary<string, string>();
                task.Parameters["FUNCTIONS_COUNT"] = 1000.ToString();
                task.Id = i++;
                task.SuitableDestinations = new LaunchDestination[1] {
                    new LaunchDestination()
                    {
                        ResourceName = "cluster_niinkt_1",
                        NodeNames = new string[1] { "i-master.nanocomputer.net" }
                    }
                };
                wf.Tasks.Add(task);
            };

            return wf;
        }


        public TaskScheduler.LaunchPlan RescheduleUrgent(TaskScheduler.UrgentWorkflow workflow)
        {
            throw new ActionNotSupportedException("This method is not supported anymore - you must use RescheduleEstimated instead.");
        }

        public TaskScheduler.UrgentWorkflow CreateUrgentTestWF()
        {
            var wf = new TaskScheduler.UrgentWorkflow();
            wf.Optimize = true;
            var cls = GetClusterNames();
            wf.Resources = cls.Select(name => TaskTimeMeter.CreateResourceRecord(name)).ToArray();
            wf.ResourcesNames = new string[0];
            var ss = GetAppNames();
            ulong i = 0;
            wf.ActiveTasks = new List<TaskScheduler.ActiveTask>();
            var runningTask = new TaskScheduler.ActiveTask() { Id = 500000 };
            runningTask.Destination = new LaunchDestination() { ResourceName = wf.Resources[0].Name, NodeNames = wf.Resources[0].Nodes.Select(n => n.DNSName).ToArray() };
            wf.ActiveTasks.Add(runningTask);
            for (var j = 0; j < 5 && j < ss.Count(); j++)
            {
                var task = new TaskScheduler.Task();
                task.ApplicationName = ss.ElementAt(j);
                task.Parameters = new Dictionary<string, string>();
                task.Parameters["FUNCTIONS_COUNT"] = 20000.ToString();
                task.Id = i++;
                wf.Tasks.Add(task);
            }
            return wf;
        }


        public Scheduler.Estimated.LaunchPlan RescheduleEstimated(Scheduler.Estimated.EstimatedWorkflow workflow)
        {
            var before = DateTime.Now;
            var result = _scheduler.RescheduleEstimated(workflow);
            var after = DateTime.Now;
            if (workflow.IsUrgent)
            {
                var uwf = (Scheduler.Estimated.EstimatedUrgentWorkflow)workflow;
                var ts = "{" + String.Join(",", uwf.Tasks.Select(t => t.Id)) + "}";
                logger.Debug("{0} {1} {2} {3} {4}", ts, GetDefaultUHName(), uwf.MinExecutionTime, uwf.MaxExecutionTime, (after - before).TotalSeconds);
            }
            return result;
        }

        public string GetDefaultUHName()
        {
            return _scheduler.GetDefaultUHName();
        }

        public IEnumerable<string> GetUHNames()
        {
            return _scheduler.GetUHNames();
        }

        public void SetDefaultUHName(string newName)
        {
            _scheduler.SetDefaultUHName(newName);
        }

        public string GetDefaultHName()
        {
            return _scheduler.GetDefaultHName();
        }

        public IEnumerable<string> GetHNames()
        {
            return _scheduler.GetHNames();
        }

        public void SetDefaultHName(string newName)
        {
            _scheduler.SetDefaultHName(newName);
        }
    }
}
