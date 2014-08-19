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
using System.Diagnostics;

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
                if (_scheduledWfs.Count == 0) 
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

                    try
                    {
                        Process proc = new Process();
                        proc.StartInfo.UseShellExecute = false;
                        proc.StartInfo.RedirectStandardOutput = true;
                        proc.StartInfo.RedirectStandardError = true;
                        string fullPathToScheduler = ConfigurationManager.AppSettings["SchedPath"] + "\\WFSched.exe";
                        logger.Info("Starting " + fullPathToScheduler + "...");
                        proc.StartInfo.FileName = fullPathToScheduler;
                        proc.Start();
                        string output = proc.StandardOutput.ReadToEnd();
                        string error = proc.StandardError.ReadToEnd();
                        proc.WaitForExit();
                        var exitCode = proc.ExitCode;
                        logger.Info("Scheduler was exit with code " + exitCode.ToString());
                        logger.Info("Output: " + output);
                        logger.Info("Error: " + error);
                    }
                    catch (Exception e)
                    {
                        logger.Info("WFSched exception: " + e.Message);
                    }


                    var tosched = wfs.Where(wf => !_scheduledWfs.ContainsKey(wf.Item1)).ToArray();

                    // (wf.Item1, wfId)
                    Dictionary<string, int> wfIDs = new Dictionary<string, int>();
                    // for each wf : global task ID, local task ID
                    var taskIDs = new List<Dictionary<ulong, ulong>>();
                    int id = 0;
                    foreach (var wf in tosched)
                    {
                        wfIDs.Add(wf.Item1, id++);
                        ulong minGlobalTaskID = ulong.MaxValue;
                        foreach (var task in wf.Item3)
                        {
                            if (task.Id < minGlobalTaskID)
                                minGlobalTaskID = task.Id;
                        }
                        var wfTaskIDs = new Dictionary<ulong, ulong>();
                        foreach (var task in wf.Item3)
                        {
                            wfTaskIDs.Add(task.Id, task.Id - minGlobalTaskID);
                        }
                        taskIDs.Add(wfTaskIDs);
                    }

                    foreach (var wf in tosched)
                    {
                        logger.Info(wf.Item1 + " " + wfIDs[wf.Item1]);
                        foreach (var task in wf.Item3)
                        {
                            logger.Info(taskIDs[wfIDs[wf.Item1]][task.Id]);
                        }
                    }

                    // wfId, (taskId1, nodeId1), ..., (taskIdN, nodeIdN) for all WFs to be scheduled
                    var schedule = new Dictionary<int, List<Tuple<int, int>>>();

                    ReadScheduleFromFile(ref schedule);

                    foreach (var wf in schedule)
                    {
                        logger.Info("Workflow " + wf.Key + " ");
                        foreach (var task in wf.Value)
                        {
                            logger.Info("Task " + task.Item1 + " Node " + task.Item2);
                        }
                    }

                    
                    foreach (var wf in tosched){
                        var tasks = wf.Item3;
                        ResourceEstimation [] estimations = null;
                        estimations = new ResourceEstimation[tasks.Count()];
                        foreach (var task in tasks)
                        {
                            ResourceEstimation estimation = new ResourceEstimation();
                           
                            var wfID = wfIDs[wf.Item1];
                            var localTaskID = Convert.ToInt32(taskIDs[wfID][task.Id]);
                            var taskNode = schedule[wfID][localTaskID];
                            var nodeIndex = taskNode.Item2;

                            logger.Info("Wf ID:" + wfID.ToString() + " localTaskID: " + localTaskID.ToString() +
                                " nodeIndex: " + nodeIndex.ToString());

                            try
                            {

                                estimation = task.Estimations[nodeIndex];
                                estimations[localTaskID] = estimation;
                            }
                            catch (Exception ex)
                            {
                                logger.ErrorException("Exception in getting estimations", ex);
                            }
                        }
                        try
                        {
                            var activeestimated = processUnscheduledWfsSeq(wf, estimations);
                            result.Plan.AddRange(activeestimated);
                            PrintLaunchPlanToLog(result);
                        }
                        catch (Exception ex)
                        {
                            logger.ErrorException("Exception in processUnscheduledWFsSeq", ex);
                        }
                        
                        
                    }

                    //if (tosched.Count() > 0)
                    //{
                    //    var tasks = tosched.First().Item3;
                    //    logger.Info(string.Format("WF ID:{0}", tosched.First().Item1));
                    //    logger.Info("Tasks to sched:");
                    //    foreach (var estTasks in tasks)
                    //    {
                    //        logger.Info(string.Format("Task ID:{0}", estTasks.Id));
                    //    }

                    //    if (tasks.Count() > 0)
                    //    {
                    //        var ests = tasks.First().Estimations;
                            
                    //        ResourceEstimation[] estimations = null;

                    //        if (ests.Count() >= tosched.Count())
                    //        {
                    //            estimations = ests.Take(tosched.Count()).ToArray();
                    //        }
                    //        else
                    //        {
                    //            estimations = new ResourceEstimation[tosched.Count()];
                    //            for (int i = 0; i < estimations.Count(); ++i)
                    //            {
                    //                estimations[i] = ests[0];
                    //            }
 
                    //        }

                    //        //foreach (var est in estimations)
                    //        //{
                    //        //    foreach (var node in est.Resource.Nodes)
                    //        //    {
                    //        //        s += node.DNSName + " ";
                    //        //    }
                    //        //}

                    //        //logger.Info("Estimations:");
                    //        //logger.Info(s);

                    //        for (int i = 0; i < tosched.Count(); ++i)
                    //        {
                    //            logger.Info(string.Format("Attempt # {0}",i+1));
                    //            var activeestimated = processUnscheduledWfsSeq(wfs[i], estimations[i]);
                    //            result.Plan.AddRange(activeestimated);
                    //            PrintLaunchPlanToLog(result);
                    //        }   
                    //    }
                    //}     

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

        private void ReadScheduleFromFile(ref Dictionary<int, List<Tuple<int, int>>> schedule)
        {
            try
            {
                System.IO.StreamReader file = new System.IO.StreamReader("C:\\CLAVIRE\\IIS\\SchedulerService\\Output\\schedule.txt");
                string line;
                int nodeIndex = -1;
                while ((line = file.ReadLine()) != null)
                {
                    string removeString = "Processor ";
                    int index = line.IndexOf(removeString);
                    line = (index < 0)
                        ? line
                        : line.Remove(index, removeString.Length);
                    // if line contains information about workflow id and task id for current node index
                    if (index < 0)
                    {
                        // string format: Wf [wfID] task [taskID] tstart [startTime]
                        string[] tokens = line.Split();
                        int wfId = 0, taskId = 0;
                        try
                        {
                            wfId = Int32.Parse(tokens[1]);
                            taskId = Int32.Parse(tokens[3]);
               
                        }
                        catch (Exception e)
                        {
                            logger.ErrorException("Exception in ReadScheduleFromFile, conversion of wf id or task id failed ", e);
                        }
                        try
                        {
                            if (schedule.ContainsKey(wfId))
                                schedule[wfId].Add(new Tuple<int, int>(taskId, nodeIndex));
                            else
                            {
                                var list = new List<Tuple<int, int>>();
                                list.Add(new Tuple<int, int>(taskId, nodeIndex));
                                schedule.Add(wfId, list);
                            }
                        }
                        catch (Exception e)
                        {
                            logger.ErrorException("Exception in ReadScheduleFromFile, schedule updating failed ", e);
                        }
                    }
                    // if line contains node number
                    else
                    {
                        try
                        {
                            nodeIndex = Int32.Parse(line);
                        }
                        catch (Exception e)
                        {
                            logger.ErrorException("Exception in ReadScheduleFromFile, conversion of node index failed ", e);
                        }
                    }
                    Console.WriteLine(line);
                }
                file.Close();
            }
            catch (Exception e)
            {
                logger.ErrorException("Exception in ReadScheduleFromFile, file could not be read ", e);
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
                foreach (var activeTask in wf.Item2)
                {
                    str = string.Format("Task ID: {0}, package name: {1}", activeTask.Id, activeTask.ApplicationName);
                    logger.Info(str);
                    str = string.Format("Task destination: {0}", activeTask.Estimation.Destination.NodeNames[0]);
                    logger.Info(str);
                }
                logger.Info("Estimated tasks");
                
                foreach (var estTask in wf.Item3)
                {
                    str = string.Format("Task ID: {0}, package name: {1}", estTask.Id, estTask.ApplicationName);
                    logger.Info(str);
                    // write id of task in workflow to log file
                    var wfTaskID = estTask.Parameters["id"];
                    logger.Info(string.Format("WF task id: {0}", wfTaskID));
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

        private List<ActiveEstimatedTask> processUnscheduledWfsSeq(Tuple<string, IEnumerable<ActiveEstimatedTask>, IEnumerable<EstimatedTask>, IEnumerable<TasksDepenendency>> wf, ResourceEstimation[] est)
        {
            var result = new List<ActiveEstimatedTask>();

           // foreach (var wf in wfs)
            //{
                // Should be empty, i.e. we mustn't get here if we have a wf with active tasks
                var runningOrScheduledTasks = wf.Item2;
                var notScheduledTasks = wf.Item3;
                var dependencies = wf.Item4;

                List<ActiveEstimatedTask> schedTasks = null;
                schedTasks = new List<ActiveEstimatedTask>(notScheduledTasks.Count());
                
                int localTaskID = 0;
                ulong minGlobalTaskID = ulong.MaxValue;

                foreach (var task in schedTasks)
                {
                    if (task.Id < minGlobalTaskID)
                        minGlobalTaskID = task.Id;
                }

                foreach (var task in schedTasks)
                {
                    localTaskID = Convert.ToInt32(task.Id - minGlobalTaskID);
                    ActiveEstimatedTask taskToAdd = new ActiveEstimatedTask();
                    taskToAdd.ApplicationName = task.ApplicationName;
                    taskToAdd.WFid = task.WFid;
                    taskToAdd.Id = task.Id;
                    taskToAdd.IsUrgent = false;
                    taskToAdd.Parameters = new Dictionary<string, string>(task.Parameters);
                    //This state must be the same for all tasks. Correct conversion will be done in ExecutionBroker
                    taskToAdd.State = TaskScheduler.TaskState.LAUNCHED;
                    taskToAdd.Estimation = new ActiveEstimation()
                    {
                        Destination = new LaunchDestination()
                        {
                            ResourceName = est[localTaskID].Resource.Name,
                            NodeNames = new string[] { est[localTaskID].Resource.Nodes[0].DNSName }
                        },
                        Resource = new Resource()
                        {
                            Name = est[localTaskID].Resource.Name,

                            Nodes = new Node[] { est[localTaskID].Resource.Nodes[0] },

                            Parameters = task.Parameters,
                        },

                        LaunchTime = est[localTaskID].Result.CalculationTime,

                        Result = est[localTaskID].Result
                    };
                    schedTasks[localTaskID] = taskToAdd;
                }
                

                //var schedTasks = notScheduledTasks.Select(task => new ActiveEstimatedTask()
                //{

                //    ApplicationName = task.ApplicationName,

                //    WFid = task.WFid,

                //    Id = task.Id,
                //    IsUrgent = false,
                //    Parameters = new Dictionary<string, string>(task.Parameters),
                //    //This state must be the same for all tasks. Correct conversion will be done in ExecutionBroker
                //    State = TaskScheduler.TaskState.LAUNCHED,

                //    Estimation = new ActiveEstimation()
                //    {

                //        Destination = new LaunchDestination()
                //        {
                //            ResourceName = est[localTaskID].Resource.Name,
                //            NodeNames = new string[] { est[localTaskID].Resource.Nodes[0].DNSName },
                //        },

                //        Resource = new Resource()
                //        {
                //            Name = est[localTaskID].Resource.Name,

                //            Nodes = new Node[] { est[localTaskID].Resource.Nodes[0] },

                //            Parameters = task.Parameters,
                //        },

                //        LaunchTime = est[localTaskID].Result.CalculationTime,

                //        Result = est[localTaskID].Result,
                //    },
                //}).ToList();

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
