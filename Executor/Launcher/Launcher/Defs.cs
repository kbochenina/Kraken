using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Shooter
{
    class Defs
    {
    }
}




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




