cluster_niinkt_1 = 
{
    "ResourceName": "cluster_niinkt_1",
    "ResourceDescription": "Кластер НИИ НКТ (HP 2x8)",
    "SupportedArchitectures": ["MPP", "SMP"],

    "ProviderName": "Cluster",

    "HardwareParams":
    [
        { "Key": "LatencyClusterNode", "Value": "1.0" }
    ],

    "Nodes": 
    [
        { "NodeName": "i-master.nanocomputer.net", "NodeAddress": "192.168.1.51" },
        { "NodeName": "i-node.nanocomputer.net",   "NodeAddress": "192.168.1.52" },
    ],

    "NodeDefaults":
    {
        "SupportedArchitectures": ["SMP"],

        "Services": { "ExecutionUrl": "http://192.168.1.189/Integrator/IntegratorService.asmx" },

        "DataFolders":
        {
            "ExchangeUrlFromSystem":   "ftp://nano:DerPar0le@192.168.1.189/{task}/{phase}",
            "ExchangeUrlFromResource": "ftp://nano:DerPar0le@192.168.1.189/{task}/{phase}",
            "CopyLocal": "true",
            "LocalFolder": "/mnt/share_from_head/{task}"
        },

        "CoresCount": 1,
        "HardwareParams":
        [
            { "Key": "CoresPerformance", "Value": "[2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000]" }
        ],

        "Packages":
        [
            { "Name": "TestP",      "Version": "v1", "AppPath": "ntestp.sh" },
            { "Name": "SEMP",       "Version": "v1", "AppPath": "zindo1.sh" },
            { "Name": "ORCA",       "Version": "v1", "AppPath": "orca" },
            { "Name": "GAMESS",     "Version": "v1", "AppPath": "gms" },
            { "Name": "JAggregate", "Version": "v1", "AppPath": "runMPI.sh" },
        ]
    }
}



