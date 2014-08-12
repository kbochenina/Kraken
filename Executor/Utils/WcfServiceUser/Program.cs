using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.IO;

namespace WcfServiceUser
{
    class Program
    {
        /*
			,"	  <step order=\"0\" type=\"exec\">"
			,"		<app name=\"ORCA\" />"
			,"		<method name=\"TDDFT\" />"
			,"		<param name=\"basis\" value=\"6-31G\" />"
			,"		<param name=\"functional\" value=\"B3LYP\" />"
			,"		<param name=\"NRoots\" value=\"3\" />"
			,"		<param name=\"Maxdim\" value=\"18\" />"
			,""
			,"		<in internalId=\"1\" storageId=\"" + sid.ToString() + "\" slotName=\"xyz_molecule\" type=\"xyz_molecule\"/>"
			,"		<out internalId=\"388\" slotName=\"output_file\" type=\"orca_output_file\" />"
			,"	</step>"
        */

            //,"	<step order=\"2\" type=\"exec\">"
            //,"		<app name=\"GAMESS\" />"
            //,"		<method name=\"DFT\" />"
            //,"		<param name=\"basis\" value=\"6-31G\" />"
            //,""
            //,"		<in internalId=\"2\" storageId=\"" + sid.ToString() + "\" type=\"xyz_molecule\" />"
            //,"	</step>"


        //private const string STORAGE_ID = "WQXD5GI9HZTGEB5THXX3"; // H2O
        private const string STORAGE_ID = "H2O"; // H2O

        public static void WriteBriefs()
        {
            using (var service = new ExecutionBrokerService.ExecutionBrokerServiceClient())
            {
                var briefs = service.GetBriefTaskList();
                var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(briefs.GetType());
                var memStream = new MemoryStream();
                serializer.WriteObject(memStream, (object) briefs);
                string json = Encoding.UTF8.GetString(memStream.ToArray());
                memStream.Close();

                Console.WriteLine();
                Console.WriteLine(json);
                Console.WriteLine();
            }
        }


        static void Main(string[] args)
        {
            /*

            var taskListProcess = new Process();
            taskListProcess.StartInfo.UseShellExecute = false;
            taskListProcess.StartInfo.RedirectStandardOutput = true;
            taskListProcess.StartInfo.RedirectStandardError = true;


            taskListProcess.StartInfo.FileName = "tasklist.exe";
            taskListProcess.StartInfo.Arguments = String.Format(
                @"/s {0} /u {1} /p {2} /fo csv /nh",
                "192.168.129.135", "nano", "Yt1NyDpQNm", 4
            );
            taskListProcess.Start();

            string table = taskListProcess.StandardOutput.ReadToEnd();
            taskListProcess.WaitForExit();

            Console.WriteLine(table);


            return;

            */


            var service = new ExecutionBrokerService.ExecutionBrokerServiceClient();
            if (service.MagicHappens())
                return;

            ulong generatedTaskId = service.GetNewTaskId();

            var tasks = new ExecutionBrokerService.TaskDescription[]
            {
                /********************* BSM *********************/

                new ExecutionBrokerService.TaskDescription()
                {
                    WfId = "WCF Test",
                    TaskId = generatedTaskId,
                    UserId = "sm",

                    LaunchMode = ExecutionBrokerService.TaskLaunchMode.Auto,

                    Package  = "BSM",

                    Params = new Dictionary<string,string>()
                    {
                        {"startCalcDate", "15.11.2008 00:00:00"},
                        {"useBSH", "false"},
                        {"useSWAN", "false"},
                        {"useAssimilation", "false"},
                    },

                    InputFiles = new ExecutionBrokerService.TaskFileDescription[]
                    {
                        new ExecutionBrokerService.TaskFileDescription
                        {
                            StorageId = "hirlam",
                            FileName  = "hirlam.zip",
                            SlotName  = "inHirlam"
                        },
                    }
                },

                /********************* CNM *********************/

                new ExecutionBrokerService.TaskDescription()
                {
                    WfId = "WCF Test",
                    TaskId = generatedTaskId,
                    UserId = "sm",

                    LaunchMode = ExecutionBrokerService.TaskLaunchMode.Auto,

                    Package  = "cnm",

                    Params = new Dictionary<string,string>()
                    {
                        {"in_format", "short"},
                    },

                    InputFiles = new ExecutionBrokerService.TaskFileDescription[]
                    {
                        new ExecutionBrokerService.TaskFileDescription
                        {
                            StorageId = "cnm_60k_p32",
                            FileName  = "cnm.in",
                            SlotName  = "inDataFile"
                        },
                    }
                },

                /********************* SWAN *********************/

                new ExecutionBrokerService.TaskDescription()
                {
                    WfId = "WCF Test",
                    TaskId = generatedTaskId,

                    LaunchMode = ExecutionBrokerService.TaskLaunchMode.Auto,

                    Package  = "SWAN",

                    Params = new Dictionary<string,string>()
                    {
                        {"startCalcDate", "15.11.2008 00:00:00"},
                        {"endCalcDate", "15.11.2008 00:00:00"},
                    },

                    InputFiles = new ExecutionBrokerService.TaskFileDescription[]
                    {
                        new ExecutionBrokerService.TaskFileDescription
                        {
                            StorageId = "hirlam",
                            FileName  = "hirlam.zip",
                            SlotName  = "inHirlam"
                        },
                    }
                },

                /********************* TESTP *********************/

                new ExecutionBrokerService.TaskDescription
                {
                    UserId = "sm",
                    WfId = "WCF Test 2",
                    TaskId = generatedTaskId,

                    LaunchMode = ExecutionBrokerService.TaskLaunchMode.Auto,

                    //Package  = "TESTP",
                    Package  = "testp",
                    Method   = "arithm",

                    Params = new Dictionary<string,string>()
                    {
                        {"operation", "plus"},
                        {"in0", "1"},
                        {"in1", "8"},
                        {"timeToWait", "30"},
                    },
/*
                    InputFiles = new ExecutionBrokerService.TaskFileDescription[]
                    {
                        new ExecutionBrokerService.TaskFileDescription
                        {
                            StorageId = "number1",
                            FileName  = "my0.in",
                            SlotName  = "inf0"
                        },

                        new ExecutionBrokerService.TaskFileDescription
                        {
                            StorageId = "number25",
                            FileName  = "my1.in",
                            SlotName  = "inf1"
                        },
                    },
*/
                    OutputFiles = new ExecutionBrokerService.TaskFileDescription[]
                    {
                        new ExecutionBrokerService.TaskFileDescription
                        {
                            StorageId = null,
                            FileName  = "out.txt",
                            SlotName  = "out_file"
                        }
                    }
                },

                /********************* Ansysmechanical *********************/

                new ExecutionBrokerService.TaskDescription
                {
                    UserId = "sm",
                    WfId = "WCF Test 2",
                    TaskId = generatedTaskId,

                    LaunchMode = ExecutionBrokerService.TaskLaunchMode.Auto,

                    //Package  = "TESTP",
                    Package  = "ansysmechanical",
                    Method   = "",

                    InputFiles = new ExecutionBrokerService.TaskFileDescription[]
                    {
                        new ExecutionBrokerService.TaskFileDescription()
                        {
                            StorageId = "50519dfa0489f5e469dea745",
                            FileName  = "svarn_dv_B.mac",
                            SlotName  = "data",
                        },
                    },
                },

                /********************* Crawler *********************/

                new ExecutionBrokerService.TaskDescription
                {
                    UserId = "sm",
                    WfId = "Crawler Test",
                    TaskId = generatedTaskId,

                    LaunchMode = ExecutionBrokerService.TaskLaunchMode.Auto,

                    Package  = "crawler",
                    Method   = "",

                    InputFiles = new ExecutionBrokerService.TaskFileDescription[]
                    {
                        new ExecutionBrokerService.TaskFileDescription()
                        {
                            FileName = "input.txt",
                            SlotName = "input",
                            StorageId = "crawler_input",
                        }
                    }
                },

                /********************* DUMMY *********************/

                new ExecutionBrokerService.TaskDescription
                {
                    UserId = "sm",
                    WfId = "WCF Test",
                    TaskId = generatedTaskId,

                    Package  = "dummy",

                    Params = new Dictionary<string,string>()
                    {
                        {"wait_time", "30000"},
                    }
                },

                /********************* COPY_PACK *********************/

                new ExecutionBrokerService.TaskDescription
                {
                    UserId = "sm",
                    WfId = "WCF Test",
                    TaskId = generatedTaskId,

                    Package  = "copy_pack",
                },

                /************************ ORCA *************************/

                new ExecutionBrokerService.TaskDescription
                {
                    WfId = "WCF Test",
                    TaskId = generatedTaskId,
                    UserId = "sm",

                    //UserCert = "number17",                    

                    LaunchMode = ExecutionBrokerService.TaskLaunchMode.Manual,

                    Package  = "ORCA",
                    Method   = "",

                    Params = new Dictionary<string,string>()
                    {
                        {"FUNCTIONS_COUNT", "1000"},
                    },

                    InputFiles = new ExecutionBrokerService.TaskFileDescription[]
                    {
                        new ExecutionBrokerService.TaskFileDescription
                        {
                            StorageId = "BZ6V7ZBZIEEMLTZL7JRJ",
                            FileName  = "",
                            SlotName  = "orca_input"
                        }
                    },

                    OutputFiles = new ExecutionBrokerService.TaskFileDescription[]
                    {
                        new ExecutionBrokerService.TaskFileDescription
                        {
                            StorageId = null,
                            FileName  = "orca.out",
                            SlotName  = "orca_out"
                        }
                    }
                },


                /********************** GAMESS ***********************/

                new ExecutionBrokerService.TaskDescription
                {
                    WfId = "WCF Test",
                    TaskId = generatedTaskId,

                    LaunchMode = ExecutionBrokerService.TaskLaunchMode.Auto,

                    Package  = "GAMESS",
                    Method   = "DFT",

                    Params = new Dictionary<string,string>()
                    {
                        {"basis", "6-31G"},
                    },

                    InputFiles = new ExecutionBrokerService.TaskFileDescription[]
                    {
                        new ExecutionBrokerService.TaskFileDescription
                        {
                            StorageId = STORAGE_ID,
                            FileName  = "",
                            SlotName  = "xyz_molecule"
                        }
                    },

                    OutputFiles = new ExecutionBrokerService.TaskFileDescription[]
                    {
                        new ExecutionBrokerService.TaskFileDescription
                        {
                            StorageId = null,
                            FileName  = "gamess_output_file.out",
                            SlotName  = "output_file"
                        }
                    }
                },

            };

            /*******************************************
            tasks = new ExecutionBrokerService.TaskDescription[]
            {
                new ExecutionBrokerService.TaskDescription()
                {
                    WfId = 1,
                    TaskId = generatedTaskId,

                    Package = "JAggregate",
                    Params = new Dictionary<string,string>(),

                    LaunchMode = ExecutionBrokerService.TaskLaunchMode.Manual,

                    InputFiles = new ExecutionBrokerService.TaskFileDescription[]
                    {
                        new ExecutionBrokerService.TaskFileDescription()
                        {
                            SlotName = "dataList",
                            StorageId = "JAggregateDataList",
                            FileName = ""
                        },

                        new ExecutionBrokerService.TaskFileDescription()
                        {
                            SlotName = "initialBeam",
                            StorageId = "JAggregateInitialBeam",
                            FileName = ""
                        },
                    },
                }
            };

            tasks = new ExecutionBrokerService.TaskDescription[]
            {
                new ExecutionBrokerService.TaskDescription()
                {
                    WfId = 1,
                    TaskId = generatedTaskId,

                    Package = "Nanoflow",
                    Params = new Dictionary<string,string>()
                    {
                        {"NumIter", "3000"},
                    },

                    LaunchMode = ExecutionBrokerService.TaskLaunchMode.Auto,

                    InputFiles = new ExecutionBrokerService.TaskFileDescription[]
                    {
                        new ExecutionBrokerService.TaskFileDescription()
                        {
                            SlotName = "strTube",
                            StorageId = "nanoflowStrTube",
                            FileName = ""
                        },
                    },
                }
            };
            ******************************************************/

            //service.Open();
            string packToGo = "dummy";
            //string packToGo = "ansysmechanical";
            var taskToGo = tasks.First(t => t.Package == packToGo);
            //taskToGo.Priority = ExecutionBrokerService.TaskPriority.Urgent;
            //taskToGo.ExecParams = new Dictionary<string, string>();
            ////taskToGo.ExecParams["Resource"] = "b4";
            //taskToGo.ExecParams["MinTime"] = "1000";
            //taskToGo.ExecParams["MaxTime"] = "1500";

            //taskToGo.Priority = ExecutionBrokerService.TaskPriority.Urgent;

            service.DefineTask(taskToGo);
            Console.WriteLine("Defijencfqlth!ed" + Environment.NewLine);
            //WriteBriefs();
            service.Execute(new[] { taskToGo.TaskId });


            Console.ReadLine();

            

            /********************************************
            var taskInParallel = tasks.First(t => t.Package == "testp");
            taskInParallel.TaskId = generatedTaskId - 1;
            taskInParallel.Priority = ExecutionBrokerService.TaskPriority.Urgent;
            taskInParallel.ExecParams = new Dictionary<string, string>();
            taskInParallel.ExecParams["MinTime"] = "1000";
            taskInParallel.ExecParams["MaxTime"] = "1500";
            service.DefineTask(taskInParallel);
            service.Execute(new[] { taskInParallel.TaskId, taskToGo.TaskId });

            Thread.Sleep(1000);
            //service.Abort(new[] { taskInParallel.TaskId, taskToGo.TaskId });
            /*********************************************/


            

            
            //System.Net.ServicePointManager.Expect100Continue = false;

            bool taskFinished = false;
            while (!taskFinished)
            {
                ulong taskId = tasks.First(t => t.Package == packToGo).TaskId;
                var task = service.GetInfo(taskId);

                Console.WriteLine("{0}: {1}", DateTime.Now, task.State);

                if (task.State == ExecutionBrokerService.TaskState.Completed ||
                    task.State == ExecutionBrokerService.TaskState.Failed ||
                    task.State == ExecutionBrokerService.TaskState.Aborted
                    )
                {
                    taskFinished = true;
                    
                    string outputs = String.Join("\n", task.OutputFiles.Select(f => f.FileName + " @ " + f.StorageId));
                    Console.WriteLine(outputs);
                    Console.ReadLine();
                }

                WriteBriefs();
                Thread.Sleep(700);
            }

            service.Close();  
            Console.ReadKey();
        }
    }
}
