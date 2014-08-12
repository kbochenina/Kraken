using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Web;
using System.Web.Services;
using Common;
using NLog.Targets;
using Scheduler;
using Scheduler.Estimated;
using Scheduler.Execution;
using TimeMeter;

namespace SchedulerService
{
    [ServiceBehavior(Namespace = "http://escience.ifmo.ru/nano/services/", InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class SchedulerService : ISchedulerService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private const string debugPrefixNameString = "FakeFakeSchedulerService.{0} method is called";
        
        private TaskScheduler _scheduler;

        private Dictionary<string, LaunchPlan> _scheduledWfs = new Dictionary<string, LaunchPlan>(); 

        public SchedulerService()
        {
            logger.Debug(string.Format(debugPrefixNameString, "SchedulerService"));
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
            logger.Debug(string.Format(debugPrefixNameString, "Reschedule"));
            throw new ActionNotSupportedException("This method is not supported anymore - you must use RescheduleEstimated instead.");
        }


        public IEnumerable<string> GetClusterNames()
        {
            logger.Debug(string.Format(debugPrefixNameString, "GetClusterNames"));
            throw new ActionNotSupportedException();
        }

        public IEnumerable<string> GetAppNames()
        {
            logger.Debug(string.Format(debugPrefixNameString, "GetAppNames"));
            throw new ActionNotSupportedException();
        }


        public TaskScheduler.Workflow CreateTestWF()
        {
            logger.Debug(string.Format(debugPrefixNameString, "CreateTestWF"));
            throw new ActionNotSupportedException();
        }


        public TaskScheduler.LaunchPlan RescheduleUrgent(TaskScheduler.UrgentWorkflow workflow)
        {
            logger.Debug(string.Format(debugPrefixNameString, "RescheduleUrgent"));
            throw new ActionNotSupportedException("This method is deprecated - you must use RescheduleEstimated instead.");
        }

        public TaskScheduler.UrgentWorkflow CreateUrgentTestWF()
        {
            logger.Debug(string.Format(debugPrefixNameString, "CreateUrgentTestWF"));
            throw new ActionNotSupportedException();
        }


        public Scheduler.Estimated.LaunchPlan RescheduleEstimated(Scheduler.Estimated.EstimatedWorkflow workflow)
        {
            logger.Debug(string.Format(debugPrefixNameString, "RescheduleEstimated"));
            try
            {
                var wfs = ConstructWorkflows(workflow).ToList();
                PrintWorkflowsToLog(wfs);

                var result = new LaunchPlan();

                if (ConfigurationManager.AppSettings["SchedMethod"] == "first")
                {
                    //logger.Debug(string.Format(debugPrefixNameString, "First branch"));
                    foreach (var wf in wfs)
                    {
                        if (_scheduledWfs.ContainsKey(wf.Item1))
                        {
                            var ids = wf.Item3.Select(t => t.Id);
                            result.Plan.AddRange(
                                _scheduledWfs[wf.Item1].Plan.Where(t => t.WFid == wf.Item1 && ids.Contains(t.Id)));
                        }
                     
                    }

                    var tosched = wfs.Where(wf => !_scheduledWfs.ContainsKey(wf.Item1));
                    var activeestimated = processUnscheduledWfs(tosched);
                            result.Plan.AddRange(activeestimated);
                }
                else
                {
                    logger.Debug("Second branch");
                    foreach (var wf in wfs)
                    {
                        if (_scheduledWfs.ContainsKey(wf.Item1))
                        {
                            var ids = wf.Item3.Select(t => t.Id);
                            result.Plan.AddRange(
                                _scheduledWfs[wf.Item1].Plan.Where(t => t.WFid == wf.Item1 && ids.Contains(t.Id)));
                        }
                    }
                    logger.Info("Current plan after already scheduled wf's addition");
                    PrintLaunchPlanToLog(result);


                    var tosched = wfs.Where(wf => !_scheduledWfs.ContainsKey(wf.Item1)).ToArray();

                    if (tosched.Count() > 0)
                    {
                        logger.Info(string.Format("toshed.Count() = {0}", tosched.Count()));
                        var tasks = tosched.First().Item3;
                        logger.Info(string.Format("WF ID:{0}", tosched.First().Item1));
                        logger.Info("Tasks to sched:");
                        foreach (var estTasks in tasks)
                        {
                            logger.Info(string.Format("Task ID:{0}", estTasks.Id));
                        }

                        if (tasks.Count() > 0)
                        {
                            var ests = tasks.First().Estimations;
                                                        
                            ResourceEstimation[] estimations = null;

                            if (ests.Count() >= tosched.Count())
                            {
                                estimations = ests.Take(tosched.Count()).ToArray();
                            }
                            else
                            {
                                estimations = new ResourceEstimation[tosched.Count()];
                                for (int i = 0; i < estimations.Count(); ++i)
                                {
                                    estimations[i] = ests[0];
                                }
 
                            }

                           

                            for (int i = 0; i < tosched.Count(); ++i)
                            {
                                logger.Info(string.Format("Attempt # {0}",i+1));
                                var activeestimated = processUnscheduledWfsSeq(wfs[i], estimations[i]);
                                result.Plan.AddRange(activeestimated);
                                PrintLaunchPlanToLog(result);
                            }   
                        }
                    }     

                }

                



                foreach (var task in result.Plan)
                {
                    task.State = TaskScheduler.TaskState.LAUNCHED;
                }

                foreach (var wf in wfs)
                {
                    if (!_scheduledWfs.ContainsKey(wf.Item1))
                    {
                        _scheduledWfs.Add(wf.Item1, result);
                    }
                }

                PrintLaunchPlanToLog(result);

                return result;
            }
            catch (Exception ex)
            {
                logger.ErrorException("Exception in scheduling", ex);
                throw;
            }

        }

        private void PrintLaunchPlanToLog(LaunchPlan plan)
        {
            logger.Info("==============Current resulted Plan Start===============");
            foreach (var p in plan.Plan)
            {
                var str = string.Format("Id:{0} .ResName:{1} NodeName: {2}", p.Id, p.Estimation.Destination.ResourceName,
                    p.Estimation.Destination.NodeNames[0]);
                logger.Info(str);
            }
            logger.Info("===============Current resulted Plan End===============");
        }

        private void PrintWorkflowsToLog(IEnumerable<Tuple<string, IEnumerable<ActiveEstimatedTask>, IEnumerable<EstimatedTask>, IEnumerable<TasksDepenendency>>> wfs)
        {
            logger.Info("==============Workflows information===============");
            foreach (var wf in wfs)
            {
                var str = string.Format("WF ID:{0}", wf.Item1);
                logger.Info(str);
                logger.Info("Active tasks");
                foreach (var activeTasks in wf.Item2)
                {
                    str = string.Format("Task ID:{0}", activeTasks.Id);
                    logger.Info(str);
                    str = string.Format("Task destination:{0}", activeTasks.Estimation.Destination.NodeNames[0]);
                    logger.Info(str);
                }
                logger.Info("Estimated tasks");
                foreach (var estTasks in wf.Item3)
                {
                    str = string.Format("Task ID:{0}", estTasks.Id);
                    logger.Info(str);
                }
            }
            logger.Info("===============End of workflows information===============");
        }


        public string GetDefaultUHName()
        {
            logger.Debug(string.Format(debugPrefixNameString, "GetDefaultUHName"));
            return _scheduler.GetDefaultUHName();
        }

        public IEnumerable<string> GetUHNames()
        {
            logger.Debug(string.Format(debugPrefixNameString, "GetUHNames"));
            throw new ActionNotSupportedException();
        }

        public void SetDefaultUHName(string newName)
        {
            logger.Debug(string.Format(debugPrefixNameString, "SetDefaultUHName"));
            throw new ActionNotSupportedException();
        }

        public string GetDefaultHName()
        {
            logger.Debug(string.Format(debugPrefixNameString, "GetDefaultHName"));
            throw new ActionNotSupportedException();
        }

        public IEnumerable<string> GetHNames()
        {
            logger.Debug(string.Format(debugPrefixNameString, "GetHNames"));
            throw new ActionNotSupportedException();
        }

        public void SetDefaultHName(string newName)
        {
            logger.Debug(string.Format(debugPrefixNameString, "SetDefaultHName"));
            throw new ActionNotSupportedException();
        }


        private IEnumerable<Tuple<string, IEnumerable<ActiveEstimatedTask>, IEnumerable<EstimatedTask>, IEnumerable<TasksDepenendency>>> ConstructWorkflows(EstimatedWorkflow wf)
        {

            var activeTasks = wf.ActiveTasks.GroupBy(t => t.WFid).ToDictionary(group => group.Key, group => group.ToList());
            var tasks = wf.Tasks.GroupBy(t => t.WFid).ToDictionary(group => group.Key, group => group.ToList());

            var allIds = new Dictionary<string, HashSet<ulong>>();
     
            var activeTasksIds = activeTasks.ToDictionary(pair => pair.Key,
                pair => new HashSet<ulong>(pair.Value.Select(t => t.Id)));
            foreach (var pair in activeTasksIds)
            {
                if (!allIds.ContainsKey(pair.Key))
                {
                    allIds.Add(pair.Key, new HashSet<ulong>());
                }
                foreach (var v in pair.Value)
                {
                    allIds[pair.Key].Add(v);
                }
                
            }


            var tasksIds = tasks.ToDictionary(pair => pair.Key,
                pair => new HashSet<ulong>(pair.Value.Select(t => t.Id)));
            foreach (var pair in tasksIds)
            {
                if (!allIds.ContainsKey(pair.Key))
                {
                    allIds.Add(pair.Key, new HashSet<ulong>());
                }
                foreach (var v in pair.Value)
                {
                    allIds[pair.Key].Add(v);
                }
            }


            var result = allIds.Select(
                pair =>
                    new Tuple
                        <string, IEnumerable<ActiveEstimatedTask>, IEnumerable<EstimatedTask>,
                            IEnumerable<TasksDepenendency>>(
                        pair.Key,
                        activeTasks.ContainsKey(pair.Key)?activeTasks[pair.Key]:new List<ActiveEstimatedTask>(),
                        tasks.ContainsKey(pair.Key)?tasks[pair.Key]:new List<EstimatedTask>(),
                        wf.Dependencies.Where(d => pair.Value.Contains(d.ConsumerId))
                        ));
            return result;
        }

        private List<ActiveEstimatedTask> processUnscheduledWfs(IEnumerable<Tuple<string, IEnumerable<ActiveEstimatedTask>, IEnumerable<EstimatedTask>, IEnumerable<TasksDepenendency>>> wfs)
        {
            var result = new List<ActiveEstimatedTask>();

             foreach (var wf in wfs)
            {
                // Should be empty, i.e. we mustn't get here if we have a wf with active tasks
                var runningOrScheduledTasks = wf.Item2;
                var notScheduledTasks = wf.Item3;
                var dependencies = wf.Item4;
                
                var schedTasks = notScheduledTasks.Select(task => new ActiveEstimatedTask()
                {

                    ApplicationName = task.ApplicationName,

                    WFid = task.WFid,

                    Id = task.Id,
                    IsUrgent = false,
                    Parameters = new Dictionary<string, string>(task.Parameters),
                    //This state must be the same for all tasks. Correct conversion will be done in ExecutionBroker
                    State = TaskScheduler.TaskState.LAUNCHED,

                    Estimation = new ActiveEstimation()
                    {

                        Destination = new LaunchDestination()
                        {
                            ResourceName = task.Estimations[0].Resource.Name,
                            NodeNames = new string[] {task.Estimations[0].Resource.Nodes[0].DNSName},
                        },

                        Resource = new Resource()
                        {
                            Name = task.Estimations[0].Resource.Name,

                            Nodes = new Node[] {task.Estimations[0].Resource.Nodes[0]},

                            Parameters = task.Parameters,
                        },

                        LaunchTime = task.Estimations[0].Result.CalculationTime,

                        Result = task.Estimations[0].Result,
                    },
                }).ToList();

                result.AddRange(schedTasks);
            }
            return result;
        }

        private List<ActiveEstimatedTask> processUnscheduledWfsSeq(Tuple<string, IEnumerable<ActiveEstimatedTask>, IEnumerable<EstimatedTask>, IEnumerable<TasksDepenendency>> wf, ResourceEstimation est)
        {
            var result = new List<ActiveEstimatedTask>();

           // foreach (var wf in wfs)
            //{
                // Should be empty, i.e. we mustn't get here if we have a wf with active tasks
                var runningOrScheduledTasks = wf.Item2;
                var notScheduledTasks = wf.Item3;
                var dependencies = wf.Item4;

                var schedTasks = notScheduledTasks.Select(task => new ActiveEstimatedTask()
                {

                    ApplicationName = task.ApplicationName,

                    WFid = task.WFid,

                    Id = task.Id,
                    IsUrgent = false,
                    Parameters = new Dictionary<string, string>(task.Parameters),
                    //This state must be the same for all tasks. Correct conversion will be done in ExecutionBroker
                    State = TaskScheduler.TaskState.LAUNCHED,

                    Estimation = new ActiveEstimation()
                    {

                        Destination = new LaunchDestination()
                        {
                            ResourceName = est.Resource.Name,
                            NodeNames = new string[] { est.Resource.Nodes[0].DNSName },
                        },

                        Resource = new Resource()
                        {
                            Name = est.Resource.Name,

                            Nodes = new Node[] { est.Resource.Nodes[0] },

                            Parameters = task.Parameters,
                        },

                        LaunchTime = est.Result.CalculationTime,

                        Result = est.Result,
                    },
                }).ToList();

                result.AddRange(schedTasks);
           // }
            return result;
        }

        private List<ActiveEstimatedTask> processScheduledWfs(IEnumerable<Tuple<IEnumerable<ActiveEstimatedTask>, IEnumerable<EstimatedTask>, IEnumerable<TasksDepenendency>>> wfs)
        {
            var result = new List<ActiveEstimatedTask>();

            foreach (var wf in wfs)
            {
                
                var runningOrScheduledTasks = wf.Item1;
                // Should be empty, i.e. we mustn't get here if we have a wf with non-active tasks
                var notScheduledTasks = wf.Item2;
                var dependencies = wf.Item3;

                var schedTasks = runningOrScheduledTasks.Select(task => new ActiveEstimatedTask()
                {
                    ApplicationName = task.ApplicationName,

                    WFid = task.WFid,

                    Id = task.Id,
                    IsUrgent = task.IsUrgent,
                    Parameters = new Dictionary<string, string>(task.Parameters),
                    State = TaskScheduler.TaskState.LAUNCHED,

                    Estimation = new ActiveEstimation()
                    {                                
                        //Destination = new Common.LaunchDestination()
                        Destination = new LaunchDestination()
                        {
                            ResourceName = task.Estimation.Destination.ResourceName,
                            NodeNames    = task.Estimation.Destination.NodeNames.ToArray()
                        },

                        //Resource = new Common.Resource()
                        Resource = new Resource()
                        {
                            Name  = task.Estimation.Resource.Name,

                            Nodes = task.Estimation.Resource.Nodes,

                            Parameters = task.Estimation.Resource.Parameters,
                        },

                        LaunchTime = task.Estimation.LaunchTime,
                        //Math.Max(0.0, ((t.CurrentSchedule.EstimatedStartTime ?? now) - now).TotalSeconds),

                        Result = task.Estimation.Result,
                    },                            
                }).ToList();

                result.AddRange(schedTasks);
            }
            return result;
        }
    }
}
