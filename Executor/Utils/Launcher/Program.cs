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
            return (T)Convert.ChangeType(configString, typeof(T).GetElementType());
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
                    .Select(st => (T)Convert.ChangeType(configString, typeof(T)))
                    .ToArray();

                return values;
            }
        }

        #endregion

        const string USER_ID = "00000000-0000-0000-0000-000000000000";
        const string WF_ID = "Launcher";

        readonly static string RESOURCE_NAME = FromConfig("Resource", /*"dnasonov"*//*"nbutakov"*//*"virtual-fedora-nbutakov"*/"");//"b4.b4-131");

        readonly static string PACK_NAME   = FromConfig("Package", "testpfg");//"dummy_model");//"hmgenerator"
        readonly static int LAUNCHES_COUNT = FromConfig("LaunchesCount", 10); // zero or negative number => unlimited launches

        readonly static int[] TIME_BETWEEN_LAUNCHES = FromConfig("TimeBetweenLaunchesInMilliseconds", 35, 8000); // in milliseconds
        //readonly static int[] TIME_BETWEEN_LAUNCHES = FromConfig("TimeBetweenLaunchesInMilliseconds", 15000, 25000); // in milliseconds
        readonly static int[] TIME_BETWEEN_CHECKS = FromConfig("TimeBetweenChecksInMilliseconds", 1500, 5000); // in milliseconds

        readonly static int STARTUP_SLEEP = FromConfig("StartupSleep", 20000); // in milliseconds


        static void DomainDo()
        {
            Console.WriteLine("Hello from domain {0}", AppDomain.CurrentDomain.FriendlyName);
            /*
            Console.WriteLine("Pack is {0}", AppDomain.CurrentDomain.GetData("package"));
            Console.WriteLine("Params are");

            var packParams = (Dictionary<string, string>) AppDomain.CurrentDomain.GetData("params");
            foreach (string paramName in packParams.Keys)
                Console.WriteLine("{0} = {1}", paramName, packParams[paramName]);
            */
            //Thread.Sleep(200);
            Console.WriteLine("Domain waked up");
        }
        
        static bool MagicHappens()
        {
            return false;

            //taskToGo.Priority = ExecutionBrokerService.TaskPriority.Urgent;
            //taskToGo.ExecParams["MinTime"] = "1000";
            //taskToGo.ExecParams["MaxTime"] = "1500";

            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            /*
            for (int i = 0; i < 10; i++)
            {
                var domainSetup = new AppDomainSetup() { LoaderOptimization = LoaderOptimization.MultiDomainHost };
                var domain = AppDomain.CreateDomain("test-sandbox " + i.ToString(), securityInfo: null, info: domainSetup);

                var task = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        domain.SetData("package", "testp");
                        domain.SetData("params", new Dictionary<string, string>() { {"a", i.ToString()} });
                        domain.DoCallBack(new CrossAppDomainDelegate(DomainDo));
                    }
                    catch (Exception e)
                    {
                        // log it:
                        Console.WriteLine(e.Message);
                    }
                });

                bool inTime = task.Wait(TimeSpan.FromMilliseconds(50));
                AppDomain.Unload(domain);
                Console.WriteLine("AppDomain unloaded");

                if (!inTime)
                    Console.WriteLine("Thread #{0} exceeded time limit", i);
            }
            */


            for (int i = 0; i < 10; i++)
            {
                var thread = new Thread(() => 
                {
                    try
                    {
                        DomainDo();
                    }
                    catch (Exception e)
                    {
                        // log it:
                        Console.WriteLine(e.Message);
                    }
                });

                thread.Start();
                bool inTime = thread.Join(TimeSpan.FromMilliseconds(500));

                if (!inTime)
                {
                    Console.WriteLine("Thread #{0} exceeded time limit", i);
                    thread.Abort();
                }
            
            }
            
            //also: Thread.CurrentThread.Abort()



            Console.WriteLine("Time elapsed: {0}", watch.Elapsed);


            //Thread.Sleep(500);

            //Console.WriteLine("P
            //Console.ReadKey();


            return true;
            return false;
        }

        private static Random _rnd = new System.Random();
        private static object _rndLock = new object();
        static int GetNextRandom(int maxValue)
        {
            lock (_rndLock)
            {
                return _rnd.Next(maxValue);
            }
        }
        static int GetNextRandom(int minValue, int maxValue)
        {
            lock (_rndLock)
            {
                return _rnd.Next(minValue, maxValue);
            }
        }

        static Executor.TaskDescription GenerateTaskDescription(ulong taskId, string wfId, string specificResourceName = null)
        {
            var task = new Executor.TaskDescription()
            {
                /*
                Package = PACK_NAME,

                Params = new Dictionary<string, string>()
                {
                    {"T", "1"}
                }
                /**/

                /**/
                Package = "testp",

                Params = new Dictionary<string, string>()
                {
                    {"in0", GetNextRandom(1, 100).ToString()},
                    {"in1", GetNextRandom(1, 100).ToString()},
                    {"timeToWait", "0"},
                },
                /**/

                /*
                InputFiles = new Executor.TaskFileDescription[]
                {
                    new Executor.TaskFileDescription { FileName = "first.txt",  SlotName = "inf0", StorageId = "50f9920888135c8044f49b4b" },
                    new Executor.TaskFileDescription { FileName = "second.txt", SlotName = "inf1", StorageId = "50f9920888135c8044f49b4b" },
                }
                */
                /*
                Package = "hmgenerator",
                
                Params = new Dictionary<string,string>()
                {
                    {"sigma", "0.1"},
                    {"maskCount", "50"},
                }
                /**/
            };

            task.ExecParams = new Dictionary<string, string>();
            //task.ExecParams["RunMode"] = "Prebilling";
            /*       
            task.Priority = Executor.TaskPriority.Urgent;  
            task.ExecParams = new Dictionary<string, string>();
            task.ExecParams["MinTime"] = "0";
            task.ExecParams["MaxTime"] = "0";
            /**/
            task.TaskId = taskId;
            task.WfId   = wfId;
            task.UserId = USER_ID;

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

        static void LaunchTasks()
        {
            var resourceNames = GetAllowedResourceNames(PACK_NAME);
            for (int iterNum = 0; LAUNCHES_COUNT < 0 || iterNum < LAUNCHES_COUNT; iterNum++)
            {
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        using (var executor = new Executor.ExecutionBrokerServiceClient())
                        {
                            ulong taskId = executor.GetNewTaskId();

                            string specificResourceName = null;
                            if (GetNextRandom(100) < 10) // on every N%'th launch
                            {
                                specificResourceName = resourceNames[GetNextRandom(resourceNames.Length)];
                                Console.WriteLine("{0} on {1}", taskId, specificResourceName);
                            }

                            if (!string.IsNullOrWhiteSpace(RESOURCE_NAME))
                                specificResourceName = RESOURCE_NAME;

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

                Thread.Sleep(GetNextRandom(
                    TIME_BETWEEN_LAUNCHES.First(),
                    TIME_BETWEEN_LAUNCHES.Last()
                ));
            }
        }

        static Dictionary<Executor.TaskState, int> GetTaskStatesCount(string wfId)
        {
            using (var service = new Executor.ExecutionBrokerServiceClient())
            {
                var taskInfos = service.GetBriefTaskList();
                var statesCount = taskInfos
                    .Where(info => info.WfId == wfId)
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
                    var stateCounts = GetTaskStatesCount(WF_ID);
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

                    // todo : Fail reason (now only in event)

                    Thread.Sleep(GetNextRandom(
                        TIME_BETWEEN_CHECKS.First(),
                        TIME_BETWEEN_CHECKS.Last()
                    ));
                }
            }

            Console.WriteLine("Finished");
            Console.ReadLine();
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

            Console.WriteLine("Sleeping on startup...");
            Thread.Sleep(STARTUP_SLEEP);

            new Thread(() => LaunchTasks()).Start();
            new Thread(() => WatchTasks()).Start();
        }
    }
}



