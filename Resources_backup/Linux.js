virtual_linux_fedora_resources =
{
    "ResourceName": "virtual-fedora-nbutakov",
    "ResourceDescription": "virtual-fedora on DevMachine",
    "SupportedArchitectures": ["SMP"],

    "Controller":
    {
        "FarmId": "localFarm",
        "Type": "UnixBaseController",
        "Url": "http://localhost:8701/ControllerFarmService/"
    },

    "HardwareParams":
    [
        { "Key": "LatencyClusterNode", "Value": "0.0" }
    ],

    "NodeDefaults":
    {
        "SupportedArchitectures": ["SMP"],

        "Credentials":
        {
            "Username": "nano",
            "Password": "Yt1NyDpQNm"
        },

        "DataFolders":
        {
            "ExchangeUrlFromSystem": "ftp://192.168.4.2/{task}/{phase}",
            "ExchangeUrlFromResource": "\\\\192.168.4.2\\Tasks\\{task}\\{phase}",
            "CopyLocal": "false",
            "LocalFolder": "/home/nano/temp/{task}"
        },

        "CoresCount": 1,
        "HardwareParams":
        [
            { "Key": "CoresPerformance", "Value": "[2000]" }
        ],

        "Packages":
        [
            {
                "Name": "TestP",
                "Version": "v1",
                "AppPath": "D:\\CLAVIRE\\_testp\\testp.exe"
            }
        ],

        "OtherSoftware": ["unix"]
    },

    "Nodes":
    [
		{
            "NodeName": "virtual_fedora_nbutakov_node",
            "NodeAddress": "192.168.0.9:22",
            "Services": {
                "ExecutionUrl": "192.168.0.9:22"
            },
            "Packages": [
			    {
                    "Name": "testpfg",
                    "Version": "v1",
                    "AppPath": "/home/nano/TestP/testp"
                },
               {
                    "Name": "testp",
                    "Version": "v1",
                    "AppPath": "/home/nano/TestP/testp"
                }
            ]	
        }		
    ]
}