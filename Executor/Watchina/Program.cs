using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;

namespace Watchina
{
    class Program
    {
        const string CSV_PATH = @"\\192.168.4.1\ExecutionLogs";
        const int MAX_LAUNCHES = 3;
        
        const string PACK_NAME = "cnm";

        const string TIME_FORMAT = "H:mm:ss.fff";
        const int WATCH_IN = 5*1000; // minimum time to update in milliseconds
        const int WATCH_OUT = 10*1000; // maximum time to update in milliseconds

        static ExecutionService.TaskDescription[] GenerateTasks(System.Random rnd)
        {
            var resourcesService = new ResourceBaseService.ResourceBaseServiceClient();
            string[] names = resourcesService.GetResourceNames().Where(name => name == "b14").ToArray();

            List<string> allowedNames = new List<string>();
            foreach (string name in names)
            {
                var res = resourcesService.GetResourceByName(name);
                if (res.Nodes.Any(n => n.Packages.Any(p => p.Name.ToLowerInvariant() == PACK_NAME.ToLowerInvariant())))
                    allowedNames.Add(name);
            }

            int nameIndex = rnd.Next(allowedNames.Count);
            string resourceName = allowedNames[nameIndex];

            var executionService = new ExecutionService.ExecutionBrokerServiceClient();            
            ulong generatedTaskId = executionService.GetNewTaskId();

            var tasks = new ExecutionService.TaskDescription[]
            {
                new ExecutionService.TaskDescription()
                {
                    WfId = "Watchina",
                    TaskId = generatedTaskId,
                    UserId = "sm",

                    LaunchMode = ExecutionService.TaskLaunchMode.Auto,

                    ExecParams = new Dictionary<string,string>()
                    {
                        {"Resource", resourceName},
                    },

                    Package  = "cnm",

                    Params = new Dictionary<string,string>()
                    {
                        {"in_format", "short"},
                    },

                    InputFiles = new ExecutionService.TaskFileDescription[]
                    {
                        new ExecutionService.TaskFileDescription
                        {
                            StorageId = "cnm_60k_p32",
                            FileName  = "cnm.in",
                            SlotName  = "inDataFile"
                        },
                    }
                },

                new ExecutionService.TaskDescription()
                {
                    WfId = "Watchina",
                    TaskId = generatedTaskId,
                    UserId = "sm",

                    LaunchMode = ExecutionService.TaskLaunchMode.Auto,

                    ExecParams = new Dictionary<string,string>()
                    {
                        {"Resource", resourceName},
                    },

                    Package  = "testp",
                    Method   = "arithm",

                    Params = new Dictionary<string,string>()
                    {
                        {"operation", "plus"},
                    },

                    InputFiles = new ExecutionService.TaskFileDescription[]
                    {
                        new ExecutionService.TaskFileDescription
                        {
                            StorageId = "number1",
                            FileName  = "my0.in",
                            SlotName  = "inf0"
                        },

                        new ExecutionService.TaskFileDescription
                        {
                            StorageId = "number25",
                            FileName  = "my1.in",
                            SlotName  = "inf1"
                        },
                    },

                    OutputFiles = new ExecutionService.TaskFileDescription[]
                    {
                        new ExecutionService.TaskFileDescription
                        {
                            StorageId = null,
                            FileName  = "out.txt",
                            SlotName  = "out_file"
                        }
                    }
                }
            };

            var packTasks = tasks.Where(t => t.Package.ToLowerInvariant() == PACK_NAME.ToLowerInvariant()).ToArray();
            return packTasks;
        }

        static void GetStatistics(string path)
        {
            string[] fileNames = Directory.GetFiles(path, "*.csv");
            foreach (string name in fileNames)
            {
                string localName = ".\\" + Path.GetFileName(name);
                string content = File.ReadAllText(name);
                File.WriteAllText(localName, content);
            }
        }

        static void Main(string[] args)
        {
            var rnd = new System.Random();

            for (int iterNum = 0; MAX_LAUNCHES < 0 || iterNum < MAX_LAUNCHES; iterNum++)
            {
                try
                {
                    using (var service = new ExecutionService.ExecutionBrokerServiceClient())
                    {
                        while (true)
                        {
                            var tasks = GenerateTasks(rnd);
                            foreach (var task in tasks)
                                service.DefineTask(task);
                            service.Execute(tasks.Select(t => t.TaskId).ToArray());

                            Console.WriteLine(
                                DateTime.Now.ToString(TIME_FORMAT) + 
                                String.Join(", ", tasks.Select(t => t.TaskId + " on " + t.ExecParams["Resource"]))
                            );

                            if (MAX_LAUNCHES < 0 && (iterNum % 10 == 0))
                                GetStatistics(CSV_PATH);

                            Thread.Sleep(rnd.Next(WATCH_IN, WATCH_OUT));
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Thread.Sleep(5000);
                }
            }

            GetStatistics(CSV_PATH);
        }
    }
}
