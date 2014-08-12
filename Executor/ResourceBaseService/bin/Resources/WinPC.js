winpc_resources =
{
    "ResourceName": "b4",
    "ResourceDescription": "Класс ПК под управлением windows на Б-4",
    "SupportedArchitectures": ["SMP", "GPGPU"],

    "ProviderName": "Win PC",

    "HardwareParams":
    [
        { "Key": "LatencyClusterNode", "Value": "0.0" }
    ],

    "NodeDefaults":
    {
        "SupportedArchitectures": ["SMP", "GPGPU"],

        "Credentials":
        {
            "Username": "nano",
            "Password": "Yt1NyDpQNm"
        },

        "DataFolders":
        {
            "ExchangeUrlFromSystem": "ftp://192.168.4.1/Tasks/{task}/{phase}",
            "ExchangeUrlFromResource": "\\\\192.168.4.1\\ftp_exchange\\Tasks\\{task}\\{phase}",
            "CopyLocal": "false",
            "LocalFolder": "C:\\Temp\\{task}"
        },

        "CoresCount": 4,
        "HardwareParams":
        [
            { "Key": "CoresPerformance", "Value": "[2000, 2000, 2000, 2000]" }
        ],

        "Packages":
        [
            {
                "Name": "TestP",
                "Version": "v1",
                "AppPath": "D:\\CLAVIRE\\_testp\\testp.cmd"
            },

            {
                "Name": "SWAN",
                "Version": "v1",

                "AppPath": "SWANExecutor.exe",

                "CopyOnStartup": [
                    "D:\\CLAVIRE\\MITP_U\\SwanPackage"
                ],

                "Cleanup": [
                    "data",
                    "Results",
                    "utils",
                    "swan\\archive",
                    "swan\\Bathymetry",
                    "swan\\current",
                    "swan\\Grads",
                    "swan\\Hirlam",
                    "swan\\mc",
                    "swan\\Results",
                    "swan\\fgulfhot"
                ]
            },

            {
                "Name": "BSM",
                "Version": "v1",

                "AppPath": "SPUVExecutor.exe",

                "CopyOnStartup": [
                    "D:\\CLAVIRE\\MITP_U\\ARMMPackage"
                ],

                "Cleanup": [
                    "ARMmodeller.exe",
                    "ARMMTemplate",
                    "AssimilationTemplate",
                    "Data\\BSH",
                    "Data\\Hirlam",
                    "Data\\Swan",
                    "Execution",
                    "EGF",
                    "utils"
                ]
            },

            {
                "Name": "ADB",
                "Version": "v1",

                "AppPath": "AssimilationPrepare\\AssimilationPrepare.exe",

                "CopyOnStartup": [
                    "D:\\CLAVIRE\\MITP_U\\AssimilationPackage"
                ],

                "Cleanup": [
                    "Assimilation 1.8.0",
                    "Assimilation4dTest",
                    "Data"
                ]
            },

            {
                "Name": "SXDSOPD",
                "Version": "v1",

                "AppPath": "D:\\CLAVIRE\\MITP_K\\shipXdsPackage\\engine\\launch.exe"
            },

            {
                "Name": "CNM",
                "Version": "v1",

                "AppPath": "D:\\CLAVIRE\\MITP_M\\ExConMod\\ExtModel.jar",
                "Params": [ {"Key": "model.Perf", "Value": "0.0321"} ]
            },

            {
                "Name": "ISM",
                "Version": "v1",

                "AppPath": "D:\\CLAVIRE\\MITP_M\\SpeadMod\\ExtSpread.jar"
            }
        ],

        "OtherSoftware": ["windows"]
    },

    "Nodes":
    [
        {
            "NodeName": "b4-132", "NodeAddress": "192.168.129.132",
            "Services": { "ExecutionUrl": "http://192.168.129.132:8787/RExService" }
        },

        {
            "NodeName": "b4-133", "NodeAddress": "192.168.129.133",
            "Services": { "ExecutionUrl": "http://192.168.129.133:8787/RExService" }
        },

        {
            "NodeName": "b4-134", "NodeAddress": "192.168.129.134",
            "Services": { "ExecutionUrl": "http://192.168.129.134:8787/RExService" },
            "CoresCount": 1
        },

        {
            "NodeName": "b4-135", "NodeAddress": "192.168.129.135",
            "Services": { "ExecutionUrl": "http://192.168.129.135:8787/RExService" },

            "Packages":
            [
                {
                    "Name": "TestP",
                    "Version": "v1",
                    "AppPath": "D:\\CLAVIRE\\_testp\\testp.cmd"
                },

                {
                    "Name": "dummy",
                    "Version": "v1",
                    "AppPath": "D:\\CLAVIRE\\Dummy\\dummy.exe",
                    "Params": [{ "Key": "bonus", "Value": "5"}]
                }
            ]
        },

        {
            "NodeName": "b4-136", "NodeAddress": "192.168.129.136",
            "Services": { "ExecutionUrl": "http://192.168.129.136:8787/RExService" },

            "Packages":
            [
    	        {
    	            "Name": "copy_pack", "Version": "v1",
    	            "AppPath": "D:\\CLAVIRE\\Copy_Pack\\copy_pack.bat"
    	        }
            ]
        },

        {
            "NodeName": "b4-138", "NodeAddress": "192.168.129.138",
            "Services": { "ExecutionUrl": "http://192.168.129.138:8787/RExService" }
        },

        {
            "NodeName": "b4-139", "NodeAddress": "192.168.129.139",
            "Services": { "ExecutionUrl": "http://192.168.129.139:8787/RExService" }
        },

        {
            "NodeName": "b4-130", "NodeAddress": "192.168.129.130",
            "Services": { "ExecutionUrl": "http://192.168.129.130:8787/RExService" }
        },

        {
            "NodeName": "b4-140", "NodeAddress": "192.168.129.140",
            "Services": { "ExecutionUrl": "http://192.168.129.140:8787/RExService" }
        }
    ]
}



