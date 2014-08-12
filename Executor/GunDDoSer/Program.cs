using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace GunDDoSer
{
    partial class Program
    {
        [ThreadStatic]
        static Random rnd = new System.Random();

        static readonly Func<ulong, ExecutionService.TaskDescription> TaskGenFunc = 
            CreateTestpTask;

        const string RESOURCE = ""; // not constrained = empty or null

        const int LAUNCHES_START_ID = 4030;
        const int LAUNCHES_COUNT = 10;

        static readonly TimeSpan MIN_SLEEP_TIME_AFTER_LAUNCH = TimeSpan.FromMilliseconds(050);
        static readonly TimeSpan MAX_SLEEP_TIME_AFTER_LAUNCH = TimeSpan.FromMilliseconds(200);

        static readonly TimeSpan MIN_SLEEP_TIME_AFTER_CHECK = TimeSpan.FromMilliseconds(1500);
        static readonly TimeSpan MAX_SLEEP_TIME_AFTER_CHECK = TimeSpan.FromMilliseconds(5000);

        static ulong[] GetTaskIds()
        {
            /*
            var ids = new List<ulong>();
            for (int i=0; i<LAUNCHES_COUNT; i++)
            {
                using (var service = new ExecutionService.ExecutionBrokerServiceClient())
                {
                    ids.Add(service.GetNewTaskId());
                }
            }

            return ids.ToArray();
            /* */

            /* */
            var ids = Enumerable.Range(LAUNCHES_START_ID, LAUNCHES_COUNT).Select(i => (ulong) i).ToArray();
            return ids;
            /* */
        }

        static void LaunchThemAll(object taskIdsAsObjects)
        {
            ulong[] taskIds = (ulong[]) taskIdsAsObjects;

            rnd = rnd ?? new System.Random();
            for (int i=0; i < taskIds.Length; i++)
            {
                using (var service = new ExecutionService.ExecutionBrokerServiceClient())
                {
                    ulong taskId = taskIds[i];
                    var taskToLaunch = TaskGenFunc(taskId);

                    service.DefineTask(taskToLaunch);
                    service.Execute(new ulong[] { taskId });

                    Thread.Sleep(rnd.Next(
                        (int) MIN_SLEEP_TIME_AFTER_LAUNCH.TotalMilliseconds,
                        (int) MAX_SLEEP_TIME_AFTER_LAUNCH.TotalMilliseconds
                    ));
                }
            }
        }

        static Dictionary<ExecutionService.TaskState, int> GetTaskStatesCount(ulong[] taskIds)
        {
            using (var service = new ExecutionService.ExecutionBrokerServiceClient())
            {
                var taskInfos = service.GetBriefTaskList();
                var statesCount = taskInfos
                    .Where(info => taskIds.Contains(info.TaskId))
                    .GroupBy(info => info.State)
                    .ToDictionary(group => group.Key, group => group.Count());

                return statesCount;
            }
        }

        static void Main(string[] args)
        {
            var service = new ExecutionService.ExecutionBrokerServiceClient();

            var timeStarted = DateTime.Now;
            Console.WriteLine("Started at {0}.", timeStarted);

            ulong[] taskIds = GetTaskIds();
            var launcherThread = new Thread(LaunchThemAll);
            launcherThread.Start(taskIds);

            int finishedTasksCount = 0;
            while (finishedTasksCount < LAUNCHES_COUNT)
            {
                var stateCounts = GetTaskStatesCount(taskIds);
                finishedTasksCount = stateCounts.Where(s =>
                    s.Key == ExecutionService.TaskState.Completed ||
                    s.Key == ExecutionService.TaskState.Failed ||
                    s.Key == ExecutionService.TaskState.Aborted
                ).Sum(s => s.Value);

                double avgSecondsOnTask =                     
                    (DateTime.Now - timeStarted).TotalSeconds / ((finishedTasksCount == 0)? 1: finishedTasksCount);

                Console.Write("{0}, ~{1} sec/task: ", DateTime.Now.ToString("HH:mm:ss"), avgSecondsOnTask);
                foreach (var state in stateCounts.Keys)
                    Console.Write("{0} = {1} ", state, stateCounts[state]);
                Console.WriteLine();

                Thread.Sleep(rnd.Next(
                    (int) MIN_SLEEP_TIME_AFTER_CHECK.TotalMilliseconds,
                    (int) MAX_SLEEP_TIME_AFTER_CHECK.TotalMilliseconds
                ));
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}
