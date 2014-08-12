using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Config = System.Configuration.ConfigurationManager;

namespace Launcher
{
    class Program
    {
        #region FromConfig functions
        /*
        public static string FromConfig<string>(string paramName, string defaults)
        {
            string configString = Config.AppSettings[paramName] ?? defaults;
            return configString;
        }

        public static T[] FromConfig<T>(string paramName, string defaults) where T: Array // string as default => array like "[1, 2, 3]"
        {
            string configString = Config.AppSettings[paramName] ?? defaults;

            var values = configString
                .Trim(new[] { '{', '}', '[', ']', '(', ')' })
                .Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(st => (T) Convert.ChangeType(configString, typeof(T).GetElementType()))
                .ToArray();

            return values;
        }
        */
        public static T FromConfig<T>(string paramName, T defaults)
        {
            string configString = Config.AppSettings[paramName];
            if (String.IsNullOrEmpty(configString))
                return defaults;
            return (T) Convert.ChangeType(configString, typeof(T).GetElementType());
        }

        public static T[] FromConfig<T>(string paramName, params T[] defaults)
        {
            string configString = Config.AppSettings[paramName];

            if (String.IsNullOrEmpty(configString))
                return defaults;
            else
            {
                var values = configString
                    .Trim(new[] { '{', '}', '[', ']', '(', ')' })
                    .Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(st => (T) Convert.ChangeType(configString, typeof(T)))
                    .ToArray();

                return values;
            }
        }

        #endregion

        const string WF_ID = "Launcher";

        readonly static string PACK_NAME = FromConfig("Package", "TestP");
        readonly static int LAUNCHES_COUNT = FromConfig("LaunchesCount", 1); // zero or negative number => unlimited launches

        readonly static int[] TIME_BETWEEN_LAUNCHES = FromConfig("TimeBetweenLaunchesInMilliseconds", 50, 200); // in milliseconds
        readonly static int[] TIME_BETWEEN_CHECKS   = FromConfig("TimeBetweenChecksInMilliseconds", 1500, 5000); // in milliseconds

        static bool MagicHappens()
        {
            //taskToGo.Priority = ExecutionBrokerService.TaskPriority.Urgent;
            //taskToGo.ExecParams["MinTime"] = "1000";
            //taskToGo.ExecParams["MaxTime"] = "1500";

            return false;
        }

        static Executor.TaskDescription GenerateTaskDescription(ulong taskId, string wfId, string specificResourceName)
        {
            var task = null;

            task.taskId = taskId;
            task.WfId = wfId;

            if (!String.IsNullOrWhiteSpace(specificResourceName))
            {
                if (task.ExecParams == null)
                    task.ExecParams = new Dictionary<string, string>();
                task.ExecParams["Resource"] = specificResourceName;
            }

            return task;
        }
        
        static string[] GetAllowedResourceNames(string packageName)
        {
            using (var resourceBase = new ResourceBase.ResourceBaseServiceClient())
            {
                string[] names = resourceBase.GetResourceNames().ToArray();

                var allowedNames = new List<string>();
                foreach (string name in names)
                {
                    var res = resourceBase.GetResourceByName(name);
                    var nodes = res.Nodes.Where(n => n.Packages.Any(p => p.Name.ToLowerInvariant() == packageName.ToLowerInvariant()));

                    foreach (var node in nodes)
                        allowedNames.Add(node.ResourceName + "." + node.NodeName);
                }

                return allowedNames.ToArray();
            }
        }

        [ThreadStatic]
        static Random rnd = new System.Random();

        static void LaunchTasks()
        {
            var resourceNames = GetAllowedResourceNames(PACK_NAME);
            for (int iterNum = 0; LAUNCHES_COUNT < 0 || iterNum < LAUNCHES_COUNT; iterNum++)
            {
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        rnd = rnd ?? new System.Random();
                        using (var executor = new Executor.ExecutionBrokerServiceClient())
                        {
                            ulong taskId = executor.GetNewTaskId();

                            string specificResourceName = null;
                            if (rnd.Next(100) < 10) // on every N%'th launch
                            {
                                specificResourceName = resourceNames[rnd.Next(resourceNames.Length)];
                                Console.WriteLine("{0} on {1}", taskId, specificResourceName);
                            }

                            var task = GenerateTaskDescription(taskId, WF_ID, specificResourceName);
                            executor.DefineTask(task);
                            executor.Execute(new[] { task.TaskId });
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Thread.Sleep(5000);
                    }
                });

                Thread.Sleep(rnd.Next(
                    TIME_BETWEEN_LAUNCHES.First(),
                    TIME_BETWEEN_LAUNCHES.Last()
                ));
            }
        }

        static Dictionary<Executor.TaskState, int> GetTaskStatesCount(ulong[] taskIds)
        {
            using (var service = new Executor.ExecutionBrokerServiceClient())
            {
                var taskInfos = service.GetBriefTaskList();
                var statesCount = taskInfos
                    .Where(info => taskIds.Contains(info.TaskId))
                    .GroupBy(info => info.State)
                    .ToDictionary(group => group.Key, group => group.Count());

                return statesCount;
            }
        }

        static void WatchTasks()
        {
            using (var executor = new Executor.ExecutionBrokerServiceClient())
            {
                var timeStarted = DateTime.Now;
                Console.WriteLine("Started at {0}.", timeStarted);

                int finishedTasksCount = 0;
                while (finishedTasksCount < LAUNCHES_COUNT)
                {
                    var stateCounts = GetTaskStatesCount(taskIds);
                    finishedTasksCount = stateCounts.Where(s =>
                        s.Key == Executor.TaskState.Completed ||
                        s.Key == Executor.TaskState.Failed ||
                        s.Key == Executor.TaskState.Aborted
                    ).Sum(s => s.Value);

                    double avgSecondsOnTask =
                        (DateTime.Now - timeStarted).TotalSeconds / ((finishedTasksCount == 0) ? 1 : finishedTasksCount);

                    Console.Write("{0}, ~{1} sec/task: ", DateTime.Now.ToString("HH:mm:ss"), avgSecondsOnTask);
                    foreach (var state in stateCounts.Keys)
                        Console.Write("{0} = {1} ", state, stateCounts[state]);
                    Console.WriteLine();

                    // todo : write to title

                    // todo : Fail reason

                    Thread.Sleep(rnd.Next(
                        TIME_BETWEEN_CHECKS.First(),
                        TIME_BETWEEN_CHECKS.Last()
                    ));
                }
            }
        }

        static void Main(string[] args)
        {
            using (var service = new Executor.ExecutionBrokerServiceClient())
            {
                if (Program.MagicHappens() || service.MagicHappens())
                {
                    Console.WriteLine("Magic happened here or on the service side. Returning...");
                    return;
                }
            }

            new Thread(() => LaunchTasks()).Start();
            new Thread(() => WatchTasks()).Start();
        }
    }
}



