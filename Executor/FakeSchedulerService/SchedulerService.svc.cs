using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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

        private int scheduledWfCount = 0;

        private Dictionary<string, LaunchPlan> _scheduledWfs = new Dictionary<string, LaunchPlan>();
        
        // wfId, (taskId1, nodeId1, startTime), ..., (taskIdN, nodeIdN, startTime) for all WFs to be scheduled
        private Dictionary<int, List<Tuple<int, int, int>>> schedule;
        
        // nodeId, (wfId, taskId, startTime according to schedule)
        private Dictionary<int, List<Tuple<int, int, int>>> nodeQueues;

         // (wf.Item1, wfId)
        private Dictionary<string, int> wfIDs;

        // (wf.Item1, tasksCount)
        private Dictionary<string, int> tasksCount = new Dictionary<string,int>();

        // (nodeId, (wfId, currentTaskId)) - we suppose that one node can execute only one task at time!!!
        private Dictionary<int, Tuple<int, int>> nodeCurrent;

        // (wfId, taskId)
        private List<Tuple<int, int>> finishedTasks = new List<Tuple<int,int>>();

        // (globalTaskId, (wfId, taskId))
        private Dictionary<ulong, Tuple<int, int>> taskIDs;

        // dateTime of first call of RescheduleEstimated() 
        private DateTime firstCall;

        private bool isFirstActive = false;

        // Holds information about if task was in the list of active tasks
        private Dictionary<ulong, bool> wasActive;

        // (nodeIndex, (windowStartTime1, windowEndTime1),..., (windowStartTimeN, windowEndTimeN))
        private Dictionary<int, List<Tuple<int, int>>> windows = new Dictionary<int,List<Tuple<int,int>>>();

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

        public int ReadNodesCount()
        {
            string fullPathToScheduler = ConfigurationManager.AppSettings["SchedPath"] + "\\InputFiles";
            try
            {
                DirectoryInfo dir = new DirectoryInfo(fullPathToScheduler);
                foreach (FileInfo file in dir.GetFiles())
                {
                    if (file.Name.Contains("res"))
                    {
                        StreamReader reader = new StreamReader(file.FullName);
                        string s = reader.ReadLine();
                        int index = s.IndexOf("=");
                        string nodes = s.Substring(index + 2, s.Length - (index + 2));
                        return Int32.Parse(nodes);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("ReadNodesCount() error. " + ex.Message, ex);
                return -1;
            }
            return -1;
        }

        public void DeleteFinishedWorkflows(ref List<Tuple<string, IEnumerable<ActiveEstimatedTask>, IEnumerable<EstimatedTask>, IEnumerable<TasksDepenendency>>> wfs)
        {
            try
            {
                List<string> keysToRemove = new List<string>();

                
                // if workflow was already scheduled, but it is not active now (actually, it means that it is finished)
                // we remove it from the list
                // logger.Info("Scheduled wf ids: ");
                foreach (var scheduled in _scheduledWfs)
                {
                    // find count of finished tasks
                    var finished = finishedTasks.Select(w => w.Item1 == wfIDs[scheduled.Key]).ToList();
                    int finishedCount = 0;
                    foreach (var v in finished)
                    {
                        if (v) finishedCount++;
                    }
                    // if count of finished tasks is equal to total tasks count
                    if (finishedCount == tasksCount[scheduled.Key])
                        keysToRemove.Add(scheduled.Key);
                    else
                    {
                        //logger.Info(scheduled.Key);
                        var ids = wfs.Select(w => w.Item1 == scheduled.Key);

                        if (!ids.Contains(true))
                            keysToRemove.Add(scheduled.Key);
                        // delete tasks

                    }
                }

               
               // logger.Info("Keys to remove: ");
                for (int i = 0; i < keysToRemove.Count; i++ )
                {
                    var key = keysToRemove[i];
                    //logger.Info("Key to remove: " + key);
                    //logger.Info(key);
                    if (_scheduledWfs.ContainsKey(key))
                        _scheduledWfs.Remove(key);
                    //logger.Info("_scheduledWF success");

                    if (schedule.ContainsKey(wfIDs[key]))
                        schedule.Remove(wfIDs[key]);
                    //logger.Info("schedule success");

                    int wfID = wfIDs[key];

                    // find all tasks of workflow to remove
                    var tasks = taskIDs.Where(t => t.Value.Item1 == wfID).ToList();
                    for (var j = 0; j < tasks.Count(); j++ )
                    {
                        var keyToRemove = tasks[j].Key;
                        taskIDs.Remove(keyToRemove);
                    }

                    if (wfIDs.ContainsKey(key))
                        wfIDs.Remove(key);
                    //logger.Info("wfIDs success");


                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("DeleteFinishedWorkflows() error. " + ex.Message, ex);
            }
        }

        private void ReadWindows()
        {
            string pathToInputFolder = ConfigurationManager.AppSettings["SchedPath"] + "\\InputFiles";
            try
            {
                string[] files = Directory.GetFiles(pathToInputFolder, "res*");
                if (files.Count() != 1)
                {
                    logger.Info("ReadWindows() error. Wrong count of resource files: " + files.Count().ToString());
                }
                StreamReader res = new StreamReader(files.First());
                string s = "";
                while (!s.Contains("Processor "))
                {
                    s = res.ReadLine();
                    //logger.Info(s);
                }
                int nodeId = 1;
                windows.Add(nodeId, new List<Tuple<int,int>>());

                while (!res.EndOfStream)
                {
                    s = res.ReadLine();
                    //logger.Info(s);
                    if (s.Contains("Processor "))
                    {
                        nodeId++;
                        windows.Add(nodeId, new List<Tuple<int,int>>());
                    }
                    else
                    {
                        string[] args = s.Split();
                        //logger.Info("Args[0]:" + args[0]);
                        //logger.Info("Args[2]:" + args[2]);
                        int start = Int32.Parse(args[0]),
                            end = Int32.Parse(args[2]);
                        windows[nodeId].Add(new Tuple<int, int>(start, end));

                    }
                }
                //foreach (var node in windows)
                //{
                //    logger.Info("Node index: " + node.Key.ToString());
                //    foreach (var window in node.Value)
                //    {
                //        logger.Info("(" + window.Item1.ToString() + ", " + window.Item2.ToString() + ")");
                //    }
                //}
            }
            catch (Exception ex)
            {
                logger.ErrorException("ReadWindows() error. " + ex.Message, ex);
            }
        }

        private void CheckFirstActive(ref List<Tuple<string, IEnumerable<ActiveEstimatedTask>, IEnumerable<EstimatedTask>, IEnumerable<TasksDepenendency>>> wfs)
        {
            foreach (var wf in wfs)
            {
                //if (wf.Item3.Count() != tasksCount[wf.Item1])
                if (!isFirstActive)
                {
                    isFirstActive = true;
                    firstCall = DateTime.Now;
                    return;
                }
            }
        }

        public Scheduler.Estimated.LaunchPlan RescheduleEstimated(Scheduler.Estimated.EstimatedWorkflow workflow)
        {
            logger.Debug(string.Format(debugPrefixNameString, "RescheduleEstimated"));
            
            try
            {
                var wfs = ConstructWorkflows(workflow).ToList();

                

                DeleteFinishedWorkflows(ref wfs);
               
                if (scheduledWfCount == 0)
                {
                    //firstCall = DateTime.Now;
                    ReadWindows();
                    PrintActiveToLog(wfs);
                    PrintEstimatedToLog(wfs);
                    // wfId, (taskId1, nodeId1, startTime), ..., (taskIdN, nodeIdN, startTime) for all WFs to be scheduled
                    schedule = new Dictionary<int, List<Tuple<int, int, int>>>();
                    // nodeId, (wfId, taskId, startTime according to schedule)
                    nodeQueues = new Dictionary<int, List<Tuple<int, int, int>>>();
                    // (wf.Item1, wfId)
                    wfIDs = new Dictionary<string, int>();
                    // (nodeId, (wfId, currentTaskId)) - we suppose that one node can execute only one task at time!!!
                    nodeCurrent = new Dictionary<int, Tuple<int, int>>();
                    // read number of nodes from resFile
                    int nodesCount = ReadNodesCount();
                    if (nodesCount == -1)
                        logger.Info("ReadNodesCount(). Wrong nodes count");
                    for (int i = 1; i <= nodesCount; i++)
                    {
                        nodeCurrent.Add(i, null);
                        nodeQueues.Add(i, new List<Tuple<int, int, int>>());
                    }
                        // (wfId, taskId)
                       
                    // (wfId, taskId)
                    taskIDs = new Dictionary<ulong, Tuple<int, int>>();
                    // clear from previous runs
                    _scheduledWfs.Clear();

                    wasActive = new Dictionary<ulong, bool>();
                    foreach (var wf in wfs)
                    {
                        if (!tasksCount.ContainsKey(wf.Item1))
                            tasksCount.Add(wf.Item1, wf.Item3.Count());
                        foreach (var task in wf.Item3)
                        {
                            wasActive.Add(task.Id, false);
                        }
                    }
                 }

               

                PrintActiveToLog(wfs);
                SetActive(ref wfs);
                //PrintEstimatedToLog(wfs);

                var result = new LaunchPlan();

                
                if (ConfigurationManager.AppSettings["SchedMethod"] == "second"){
                    
                    //DeleteFinishedTasksFromQueues(ref wfs);
                    //logger.Info("DeleteFinishedTasksFromQueues() ended");
                    DeleteFinishedTasksFromQueues(ref wfs);
                    logger.Info("DeleteFinishedTasksFromQueues() ended");
                    DeleteFinishedWorkflows(ref wfs);
                    logger.Info("DeleteFinishedWorkflows() ended");
                    //logger.Info("RescheduleEstimated(). Keys of scheduled workflows: ");
                    //foreach (var wf in _scheduledWfs)
                    //{
                    //    logger.Info("Workflow " + wf.Key);
                    //}
                    
                    // (wfKey, wfDep, wfActiveEstimated)
                    var wfResults = new Dictionary<string, Tuple<IEnumerable<Scheduler.Estimated.TasksDepenendency>, List<ActiveEstimatedTask>>>();

                    //logger.Info("Wf ids: ");
                    foreach (var wf in wfs)
                    {
                        // if workflow was already scheduled, copy previous schedule 

                        if (_scheduledWfs.ContainsKey(wf.Item1))
                        {
                            wfResults.Add(wf.Item1, new Tuple<IEnumerable<Scheduler.Estimated.TasksDepenendency>, List<ActiveEstimatedTask>>(wf.Item4, _scheduledWfs[wf.Item1].Plan));
                        }
                     }


                    //logger.Info("Scheduled WFs count: " + _scheduledWfs.Count());
                    //logger.Info("Current plan after already scheduled wf's addition");

                    //PrintLaunchPlanToLog(result);

                    
                    var tosched = wfs.Where(wf => !_scheduledWfs.ContainsKey(wf.Item1)).ToArray();

                    if (tosched.Count() != 0)
                    {

                        // wf ids scheduled on current step
                        List<int> schedWFIds = new List<int>();

                        int idToAdd = 0;

                        if (wfIDs.Count == 0)
                            idToAdd = 0;
                        else idToAdd = wfIDs.ElementAt(wfIDs.Count - 1).Value;

                        foreach (var wf in tosched)
                        {
                            wfIDs.Add(wf.Item1, idToAdd);
                            schedWFIds.Add(idToAdd);
                            foreach (var task in wf.Item3)
                            {
                                taskIDs.Add(task.Id, new Tuple<int, int>(idToAdd, Int32.Parse(task.Parameters["id"])));
                            }

                            idToAdd++;
                        }
                        // removing old *.dat files if they exist

                        // generating *.dat files for each unscheduled wf
                        for (int i = 0; i < tosched.Length; i++)
                        {
                            //logger.Info("To sched key: {0}", tosched[i].Item1);
                            GenerateWorkflowInfo(ref tosched[i], wfIDs[tosched[i].Item1], i);
                        }

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
                            //logger.Info("Output: " + output);
                            //logger.Info("Error: " + error);
                        }
                        catch (Exception e)
                        {
                            logger.Info("WFSched exception: " + e.Message);
                        }

                        ReadScheduleFromFile(ref schedule);

                        // add information to resource queues
                        AddSchedWfToQueues(ref schedule, ref schedWFIds);


                        foreach (var wf in tosched)
                        {
                            var tasks = wf.Item3;
                            var wfID = wfIDs[wf.Item1];

                            ResourceEstimation[] estimations = null;
                            estimations = new ResourceEstimation[tasks.Count()];
                            foreach (var task in tasks)
                            {
                                ResourceEstimation estimation = new ResourceEstimation();

                                var localTaskID = int.Parse(task.Parameters["id"]) - 1;
                                //logger.Info("Task WFID:", localTaskID);
                                var taskNode = schedule[wfID].Where(t => t.Item1 == localTaskID);
                                //logger.Info("Before: wfID " + wfID + " localTaskID: " + localTaskID);
                                var nodeIndex = taskNode.First().Item2;
                                //logger.Info("After");


                                try
                                {
                                    estimation = task.Estimations[nodeIndex];
                                    estimations[localTaskID] = estimation;
                                }
                                catch (Exception ex)
                                {
                                    logger.ErrorException("Exception in getting estimations", ex);
                                }
                                scheduledWfCount++;
                            }
                            try
                            {
                                var activeestimated = processUnscheduledWfsSeq(wf, estimations);
                                wfResults.Add(wf.Item1, new Tuple<IEnumerable<Scheduler.Estimated.TasksDepenendency>, List<ActiveEstimatedTask>>(wf.Item4, activeestimated));
                            }
                            catch (Exception ex)
                            {
                                logger.ErrorException("Exception in processUnscheduledWFsSeq", ex);
                            }
                        }
                        
                 
                      }

                    

                    // update status
                    UpdateTaskStatus(ref wfResults);

                    
                    SetStatusToFinishedTasks(ref wfResults);

                    try
                    {
                        foreach (var wf in wfResults)
                        {
                            var wfResult = new LaunchPlan();
                            wfResult.Plan.AddRange(wf.Value.Item2);
                            result.Plan.AddRange(wf.Value.Item2);

                            if (_scheduledWfs.ContainsKey(wf.Key))
                            {
                                _scheduledWfs[wf.Key] = wfResult;
                            }
                            else
                            {
                                _scheduledWfs.Add(wf.Key, wfResult);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.ErrorException("Add final plan error. " + ex.Message, ex);
                    }
                    
                }
                if (isFirstActive)
                    logger.Info("Current time: " + GetTime());
                //logger.Info("Current plan: " + _scheduledWfs.Count());
                PrintLaunchPlanToLog(result);
                //logger.Info("Workflows tasks count");
                foreach (var wf in wfs)
                {
                    logger.Info(wf.Item1 + " active: " + wf.Item2.Count() + " estimated: " + wf.Item3.Count());
                }

                //prevCall = DateTime.Now;

                if (!isFirstActive)
                    CheckFirstActive(ref wfs);

                return result;
            }
            catch (Exception ex)
            {
                logger.ErrorException("Exception in scheduling. " + ex.Message, ex);
                throw;
            }

        }

        // remove finished tasks from wfResults
        private void SetStatusToFinishedTasks(ref Dictionary<string, Tuple<IEnumerable<Scheduler.Estimated.TasksDepenendency>, List<ActiveEstimatedTask>>> wfResults)
        {
            try
            {
                foreach (var finTask in finishedTasks)
                {
                    int wfId = finTask.Item1, taskId = finTask.Item2;
                    var wfKeys = wfIDs.Where(w => w.Value == wfId);
                    if (wfKeys.Count() == 0)
                    {
                        //logger.Info("SetStatusToFinishedTasks() error. Cannot find wfkey for workflow id " + wfId.ToString());
                        continue;
                    }
                    
                    string wfKey = wfKeys.First().Key;
                    if (!wfResults.ContainsKey(wfKey))
                        continue;
                    var taskList = wfResults[wfKey].Item2;
                    var globalTaskIds = taskIDs.Where(t => t.Value.Item1 == wfId && t.Value.Item2 == taskId);
                    if (globalTaskIds.Count() == 0)
                        logger.Info("SetStatusToFinishedTasks() error. Cannot find global task id for task " + taskId.ToString() + " of workflow" + wfId.ToString());
                    ulong globalTaskId = globalTaskIds.First().Key;
                    var task = taskList.Where(t => t.Id == globalTaskId);
                    // if we found finished task in wfResults, we should remove it
                    if (task.Count() != 0)
                    {
                        //task.First().State = TaskScheduler.TaskState.SCHEDULED;
                        wfResults[wfKey].Item2.Remove(task.First());
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("SetStatusToFinishedTasks() error. " + ex.Message, ex);
            }
        }

        private void SetActive(ref List<Tuple<string, IEnumerable<ActiveEstimatedTask>, IEnumerable<EstimatedTask>, IEnumerable<TasksDepenendency>>> wfs)
        {
            try
            {
                foreach (var wf in wfs)
                {
                    foreach (var task in wf.Item2)
                    {
                        wasActive[task.Id] = true;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("SetActive() error. " + ex.Message, ex);
            }
        }

        private void UpdateTaskStatus(ref Dictionary<string, Tuple<IEnumerable<Scheduler.Estimated.TasksDepenendency>, List<ActiveEstimatedTask>>> wfResults)
        {
            //logger.Info("UpdateTaskStatus() was called");
            //logger.Info("Active estimated:");
            //foreach (var wf in wfResults)
            //{
            //    foreach (var task in wf.Value.Item2)
            //        logger.Info("Wf id: " + task.WFid + " task id: " + task.Parameters["id"]);
            //}
            //foreach (var node in nodeQueues)
            //{
            //    logger.Info("Node queue key " + node.Key);
            //}
            try
            {
                // for all nodes
                foreach ( var node in nodeCurrent.ToList() )
                {
                    var nodeId = node.Key;
                    logger.Info("Node # " + node.Key);
                    //logger.Info("Node id: " + nodeId.ToString());
                    // if node is free
                    if (node.Value == null && !isNodeBusy(nodeId))
                    {
                        //logger.Info("Node is free");
                        if (!nodeQueues.ContainsKey(nodeId))
                        {
                            PrintNodeQueues();
                            logger.Info("Node current does not contain nodeId " + nodeId.ToString());
                        }
                        // if there is task waiting for destination
                        if (nodeQueues[node.Key].Count != 0)
                        {
                            bool taskWasAssigned = false,
                                taskCannotBeAssigned = false;

                            int taskIndex = 0;

                            while (!taskWasAssigned && !taskCannotBeAssigned)
                            {
                                //logger.Info("Node queue is not empty");
                                var task = nodeQueues[node.Key][taskIndex];
                                int wfId = task.Item1, taskId = task.Item2;
                                //logger.Info("Wf id: " + wfId.ToString() + " Task id: " + taskId.ToString());

                                var wfKeyPair = wfIDs.FirstOrDefault(wf => wf.Value == wfId);
                                //logger.Info("Key pair : success");

                                string wfKey = wfKeyPair.Key;
                                //logger.Info("wfKey = " + wfKey);

                                if (!wfIDs.ContainsKey(wfKey))
                                    logger.Info("Key is not presented in wf id's");

                                var taskToChange = wfResults[wfKey].Item2.Where(t => t.WFid == wfKey && t.Parameters["id"] == taskId.ToString()).ToList();
                                //logger.Info("Task to change success");
                                if (taskToChange.Count == 0)
                                    logger.Info("UpdateTaskStatus(). Cannot find task " + taskId.ToString() + " of workflow " + wfId.ToString() + " in active estimated tasks");


                                if (IsParentsFinished(taskToChange.First().Id, wfResults[wfKey].Item1))
                                {
                                    // change the status of the task
                                    taskToChange.First().State = TaskScheduler.TaskState.LAUNCHED;
                                    if (!nodeCurrent.ContainsKey(nodeId))
                                        logger.Info("Node current does not contain nodeId " + nodeId.ToString());
                                    // set task as executed on the node
                                    nodeCurrent[nodeId] = new Tuple<int, int>(wfId, taskId);
                                    // remove task from waiting list for this node
                                    nodeQueues[nodeId].RemoveAt(taskIndex);
                                    taskWasAssigned = true;
                                    //logger.Info("Node queues success");
                                    logger.Info("UpdateTaskStatus(). Task " + taskId.ToString() + " of workflow " + wfId.ToString() + " status was changed to LAUNCHED");
                                }
                                else
                                {
                                    logger.Info("Parents are not finished");

                                    taskIndex++;
                                    if (taskIndex == nodeQueues[node.Key].Count())
                                        taskCannotBeAssigned = true;
                                }
                            }
                        }
                    }
                }
                PrintNodeQueues();
                foreach (var node in nodeCurrent)
                {
                    logger.Info(node.Key);
                    if (node.Value != null)
                    {
                        logger.Info("(" + node.Value.Item1 + ", " + node.Value.Item2 + ")");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("UpdateTaskStatus() exception. " + ex.Message, ex);
            }
        }

        private bool isNodeBusy(int nodeIndex)
        {
            bool result = false;
            try
            {
                //logger.Info("isNodeBusy() called. Node index = " + nodeIndex.ToString());
                int currentTime = GetTime();
                logger.Info("Current time: " + currentTime.ToString());
                foreach (var window in windows[nodeIndex])
                {
                    if (window.Item1 <= currentTime && window.Item2 >= currentTime)
                    {
                        result = true;
                        logger.Info("Node " + nodeIndex.ToString() + " is busy, currentTime = " + currentTime.ToString() +
                            " in window (" + window.Item1.ToString() + " , " + window.Item2.ToString() + ")");
                        return result;
                    }
                }

                
            }
            catch (Exception ex)
            {
                logger.ErrorException("isNodeBusy() error. " + ex.Message, ex);
            }

            return result;
        }

        private bool IsParentsFinished(ulong globalTaskId, IEnumerable<Scheduler.Estimated.TasksDepenendency> dep)
        {
            try
            {
                var parentsGlobalIds = dep.Where(t => t.ConsumerId == globalTaskId);

                // if it is initial task
                if (parentsGlobalIds.Count() == 0)
                    return true;
                //PrintTaskIDs();
                string toLog = "Parents: ";
                foreach (var parent in parentsGlobalIds)
                {
                    //logger.Info("Parent.ProviderId:" + parent.ProviderId);
                    int wfId = taskIDs[parent.ProviderId].Item1, taskId = taskIDs[parent.ProviderId].Item2;
                    toLog += "(" + wfId.ToString() + ", " + taskId.ToString() + ") ";
                }
                logger.Info(toLog);
                foreach (var parent in parentsGlobalIds)
                {
                    int wfId = taskIDs[parent.ProviderId].Item1, taskId = taskIDs[parent.ProviderId].Item2;
                    var task = finishedTasks.Where(t => t.Item1 == wfId && t.Item2 == taskId);

                    // if any parent is not finished yet
                    if (task.Count() == 0)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.ErrorException("IsParentsFinished() exception. " + ex.Message, ex);
                return false;
            }
        }

        private void PrintTaskIDs()
        {
            foreach (var task in taskIDs)
            {
                string str = "(" + task.Key + ", (" + task.Value.Item1 + ", " + task.Value.Item2 + ")";
                logger.Info(str);
            }
        }

        private void PrintNodeQueues()
        {
            logger.Info("Node queues: ");
            foreach (var nodeQueue in nodeQueues)
            {
                logger.Info(nodeQueue.Key);
                foreach (var task in nodeQueue.Value)
                {
                    logger.Info(task.Item1 + " " + task.Item2 + " " + task.Item3);
                }
            }
        }

        private void DeleteFinishedTasksFromQueues(ref List<Tuple<string, IEnumerable<ActiveEstimatedTask>, IEnumerable<EstimatedTask>, IEnumerable<TasksDepenendency>>>wfs){
            try
            {
               
                foreach (var currentTask in nodeCurrent.ToList())
                {
                    // if there is a task executed on this node
                    if (currentTask.Value != null)
                    {
                        int wfId = currentTask.Value.Item1,
                            taskId = currentTask.Value.Item2;

                        var global = taskIDs.Where(t => t.Value.Item1 == wfId && t.Value.Item2 == taskId);
                        
                        string wfKey = wfIDs.FirstOrDefault(x => x.Value == wfId).Key;
                        
                        // if current task is in ActiveEstimated tasks, it is not finished yet
                        // else we should delete it from queues
                        var currentWf = wfs.Where(wf => wf.Item1 == wfKey).ToList();
                        bool isWFFinished = false,
                            isLaunchedNotInActive = false,
                            isLaunchedNotInActiveAndEstimated = false;
                        if (currentWf.Count == 0)
                        {
                            //logger.Info("DeleteFinishedTasksFromQueues(). Workflow " + wfId.ToString() + " (key " + wfKey + ") was not found in a list of workflows");
                            isWFFinished = true;

                        }
                        else
                        {
                            ulong globalTaskId = global.First().Key;
                            var activeTasks = currentWf.First().Item2;

                            var foundTask = activeTasks.Where(task => task.Parameters["id"] == taskId.ToString()).ToList();

                            isLaunchedNotInActive = (foundTask.Count == 0 && wasActive[globalTaskId] == true);
                            isLaunchedNotInActiveAndEstimated = false;

                            if (!isLaunchedNotInActive)
                            {
                                // second case - when task was not in active list
                                // in this case wasActive status = false, but it is neither in active nor in estimated lists
                                var estimatedTasks = currentWf.First().Item3;
                                var foundEstimatedTask = estimatedTasks.Where(task => task.Parameters["id"] == taskId.ToString()).ToList();
                                if (foundTask.Count == 0 && foundEstimatedTask.Count == 0)
                                    isLaunchedNotInActiveAndEstimated = true;
                            }
                        }
                        if (isLaunchedNotInActive || isLaunchedNotInActiveAndEstimated || isWFFinished)// && CheckTime())
                        {
                            int nodeId = currentTask.Key;
                            // remove current task from current node
                            nodeCurrent[nodeId] = null;
                           
                            //logger.Info("Node current of " + nodeId.ToString() + " was nulled");
                            finishedTasks.Add(new Tuple<int, int>(wfId, taskId));

                            //taskIDs.Remove(taskToRemove.First().Key);

                            logger.Info("DeleteFinishedTasksFromQueues(). Task " + taskId.ToString() + " of workflow " + wfId.ToString() +
                                " was finished on " + currentTask.Key.ToString());
                            //logger.Info("DeleteFinishedTasksFromQueues(). Task " + taskId.ToString() + " was removed from taskIds");
                        }
                    }
                }
               // PrintNodeQueues();
            }
            catch (Exception ex)
            {
                logger.ErrorException("DeleteFinishedTasksFromQueues() exception. " + ex.Message, ex);
            }
        }

        private int GetTime()
        {
            DateTime now = DateTime.Now;
            int hours = now.Hour, minutes = now.Minute, seconds = now.Second,
                firstHours = firstCall.Hour, firstMinutes = firstCall.Minute, firstSeconds = firstCall.Second;
            
            int nowSec = hours * 3600 + minutes * 60 + seconds,
                firstSec = firstHours * 3600 + firstMinutes * 60 + firstSeconds;
            int currentTime = nowSec - firstSec;
            //logger.Info("Current time: " + (nowSec - firstSec).ToString());
            return currentTime;
        }

        private void AddSchedWfToQueues(ref Dictionary<int, List<Tuple<int, int, int>>> schedule, ref List<int> schedWFIds){
            // adding values to queues of nodes
            // for each workflow scheduled on this step
            foreach (var wfId in schedWFIds)
            {
                // for each scheduled task
                foreach (var schedTask in schedule[wfId])
                {
                    // task and nodes in schedule are numbered from 0
                    int taskId = schedTask.Item1 + 1,
                        nodeId = schedTask.Item2 + 1,
                        startTime = schedTask.Item3;
                    if (!nodeQueues.ContainsKey(nodeId))
                    {
                        var list = new List<Tuple<int, int, int>>();
                        list.Add(new Tuple<int, int, int>(wfId, taskId, startTime));
                        nodeQueues.Add(nodeId, list);
                    }
                    else
                    {
                        nodeQueues[nodeId].Add(new Tuple<int, int, int>(wfId, taskId, startTime));
                    }
                }
            }
            // sorting values in increasing order of start times
            foreach (var nodeQueue in nodeQueues)
            {
                nodeQueue.Value.Sort((a, b) => a.Item3.CompareTo(b.Item3));
            }
            PrintNodeQueues();
        }

        private void ReadScheduleFromFile(ref Dictionary<int, List<Tuple<int, int, int>>> schedule)
        {
            try
            {
                System.IO.StreamReader file = new System.IO.StreamReader("C:\\CLAVIRE\\IIS\\SchedulerService\\Output\\schedule.txt");
                string line;
                int nodeId = -1;
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
                        int wfId = 0, taskId = 0, startTime = 0;
                        try
                        {
                            wfId = Int32.Parse(tokens[1]);
                            taskId = Int32.Parse(tokens[3]);
                            startTime = Int32.Parse(tokens[5]);    
                        }
                        catch (Exception e)
                        {
                            logger.ErrorException("Exception in ReadScheduleFromFile, conversion of wf id or task id failed ", e);
                        }
                        try
                        {
                            if (schedule.ContainsKey(wfId))
                                schedule[wfId].Add(new Tuple<int, int, int>(taskId, nodeId, startTime));
                            else
                            {
                                var list = new List<Tuple<int, int, int>>();
                                list.Add(new Tuple<int, int, int>(taskId, nodeId, startTime));
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
                            nodeId = Int32.Parse(line);
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

        private void GenerateWorkflowInfo(ref Tuple<string, IEnumerable<ActiveEstimatedTask>, IEnumerable<EstimatedTask>, IEnumerable<TasksDepenendency>> wf, int index, int wfIndex)
        {
            string fullPathToFile = ConfigurationManager.AppSettings["SchedPath"];
            
            try
            {
                fullPathToFile += "\\InputFiles";
                if (!Directory.Exists(fullPathToFile))
                    Directory.CreateDirectory(fullPathToFile);
                fullPathToFile += "\\wfset";
                if (!Directory.Exists(fullPathToFile))
                    Directory.CreateDirectory(fullPathToFile);
                // if this workflow is first in the set
                else if (wfIndex == 0)
                {
                    // if there are any previous files in wfset directory, they should be deleted
                    DirectoryInfo dir = new DirectoryInfo(fullPathToFile);
                    foreach (FileInfo file in dir.GetFiles())
                        file.Delete();
                }
                string wfFileName = "workflow_" + index.ToString() + ".dat";
                StreamWriter wfFile = new StreamWriter(fullPathToFile + "\\" + wfFileName);
                DateTime dt = DateTime.Now;
                wfFile.WriteLine("<!-- Generated: " + dt.ToString() + " -->");
                int jobCount =  wf.Item3.Count();
                wfFile.WriteLine("Job count = " + jobCount);

                Dictionary<ulong, int> taskIDs = new Dictionary<ulong, int>();
                foreach (var task in wf.Item3)
                {
                    taskIDs.Add(task.Id, Convert.ToInt32(task.Parameters["id"]));
                }


                wfFile.WriteLine();
                // logger.Info(" <!-- Part 1:  Dependencies -->");
                wfFile.WriteLine(" <!-- Part 1:  Dependencies -->");
                wfFile.WriteLine();

                int [,] dep = new int [jobCount, jobCount];
                for (int i = 0; i < jobCount; i++){
                    for (int j = 0; j < jobCount; j++)
                        dep[i,j] = 0;
                }

                foreach (var dependency in wf.Item4){
                    //logger.Info ("Provider id: " + dependency.ProviderId.ToString() + " , consumerID: " + dependency.ConsumerId.ToString());
                    int provider = taskIDs[dependency.ProviderId] - 1;
                    int consumer = taskIDs[dependency.ConsumerId] - 1;
                    dep[provider, consumer] = 1;
                }

                logger.Info("Dep finished");

                for (int i = 0; i < jobCount; i++)
                {
                    for (int j = 0; j < jobCount; j++)
                        wfFile.Write(dep[i, j].ToString() + " ");
                    wfFile.Write("\n");
                }

                wfFile.WriteLine();
                wfFile.WriteLine(" <!-- Part 2:  Execution times -->");
                //logger.Info(" <!-- Part 2:  Execution times -->");
                wfFile.WriteLine();

                Dictionary<int, double> execTimes = new Dictionary<int,double>();

                foreach (var task in wf.Item3)
                {
                    //logger.Info(task.Parameters["id"] + " " + task.Parameters["execTime"]);
                    execTimes.Add(Convert.ToInt32(task.Parameters["id"]), 
                        double.Parse(task.Parameters["execTime"], System.Globalization.CultureInfo.InvariantCulture));
                }

                var list = execTimes.Keys.ToList();
                list.Sort();

                foreach (var key in list)
                {
                    string execTime = execTimes[key].ToString();
                    execTime = execTime.Replace(',', '.');
                    wfFile.WriteLine(key.ToString() + " "  + execTime);
                }
                logger.Info("Exec times finished");
                wfFile.Close();
            }
            catch (Exception ex)
            {
                logger.ErrorException("GenerateDAX() exception. " + ex.Message, ex);
            }
        }

        private void PrintLaunchPlanToLog(LaunchPlan plan)
        {
            logger.Info("==============Current resulted Plan Start===============");
            foreach (var p in plan.Plan)
            {
                var str = string.Format("Id:{0} .ResName:{1} NodeName: {2} State: {3}", p.Id, p.Estimation.Destination.ResourceName,
                    p.Estimation.Destination.NodeNames[0], p.State.ToString());
                logger.Info(str);
            }
            logger.Info("===============Current resulted Plan End===============");
        }

        private void PrintEstimatedToLog(IEnumerable<Tuple<string, IEnumerable<ActiveEstimatedTask>, IEnumerable<EstimatedTask>, IEnumerable<TasksDepenendency>>> wfs)
        {
            logger.Info("==============Workflows information===============");
            foreach (var wf in wfs)
            {
                logger.Info("Estimated tasks");
                var str = string.Format("WF ID:{0}", wf.Item1);
                logger.Info(str);
                
                foreach (var estTask in wf.Item3)
                {
                    str = string.Format("Task ID: {0}, package name: {1}", estTask.Id, estTask.ApplicationName);
                    logger.Info(str);
                    // write id of task in workflow to log file
                    var wfTaskID = estTask.Parameters["id"];
                    logger.Info(string.Format("WF task id: {0}", wfTaskID));
                    logger.Info("Task state: " + str);
                }
            }
            logger.Info("===============End of workflows information===============");
        }

        private void PrintActiveToLog(IEnumerable<Tuple<string, IEnumerable<ActiveEstimatedTask>, IEnumerable<EstimatedTask>, IEnumerable<TasksDepenendency>>> wfs)
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
                    str = activeTask.State.ToString();
                    logger.Info("Task state: " + str);

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
                schedTasks = new List<ActiveEstimatedTask>();
                
                int localTaskID = 0;

                try
                {

                    foreach (var task in notScheduledTasks)
                    {
                        localTaskID = Convert.ToInt32(task.Parameters["id"]) - 1;
                        //logger.Info("Local task id: " + localTaskID);
                        //logger.Info(" NodeName: " + est[localTaskID].Resource.Nodes[0].DNSName);
                        ActiveEstimatedTask taskToAdd = new ActiveEstimatedTask();
                        taskToAdd.ApplicationName = task.ApplicationName;
                        taskToAdd.WFid = task.WFid;
                        taskToAdd.Id = task.Id;
                        taskToAdd.IsUrgent = false;
                        taskToAdd.Parameters = new Dictionary<string, string>(task.Parameters);
                        //This state must be the same for all tasks. Correct conversion will be done in ExecutionBroker
                        //taskToAdd.State = TaskScheduler.TaskState.LAUNCHED;
                        taskToAdd.State = TaskScheduler.TaskState.SCHEDULED;
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
                        schedTasks.Add(taskToAdd);
                    }
                    result.AddRange(schedTasks);
                    //logger.Info("Scheduled tasks count: " + schedTasks.Count);
                   
                }
                catch (Exception ex)
                {
                    logger.ErrorException("Exception in processUnscheduledWfsSeq", ex);
                } 
                return result;
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
