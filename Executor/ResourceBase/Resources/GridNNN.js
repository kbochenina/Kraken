grid_resources =
{
    "ResourceName": "Grid NNN",
    "ResourceDescription": "Грид ННС",
    "SupportedArchitectures": ["MPP", "SMP"],

    "ProviderName": "Grid NNN",

    "HardwareParams":
    [
        { "Key": "LatencyClusterNode", "Value": "0.0" }
    ],

    "NodeDefaults":
    {
        "SupportedArchitectures": ["SMP"],

        "Services": { "ExecutionUrl": "https://tb01.ngrid.ru:5053" },

        "DataFolders":
        {
            "ExchangeUrlFromSystem": "ftp://nano:DerPar0le@192.168.1.189/{task}/{phase}",
            "ExchangeUrlFromResource": "ftp://nano:DerPar0le@194.85.163.231/srv/ftp/{task}/{phase}",
            "CopyLocal": "true",
            "LocalFolder": "gsiftp://nnn1.pnpi.nw.ru/home/gridui060/files/Jobs/{task}"
        },

        "CoresCount": 0,
        "HardwareParams":
        [
            { "Key": "CoresPerformance", "Value": "[2000]" }
        ]
    },

    "Nodes":
    [
        {
            "NodeName": "hp-cl.escience.ifmo.ru",
            "NodeAddress": "hp-cl.escience.ifmo.ru",

            "Packages":
            [
                {
                    "Name": "ORCA", "Version": "v1",
                    "AppPath": "orca",
                    "EnvVars": [ {"Key": "PATH", "Value": "/grid/software/orca-2.6.35-nompi/bin/:$PATH"} ]
                },

                {
                    "Name": "CNM", "Version": "v1",
                    "AppPath": "/usr/bin/java",
                    "Params": [{ "Key": "requirements", "Value": "{ \"software\": \"extmodel\" }"}]
                },

                {
                    "Name": "ISM", "Version": "v1",
                    "AppPath": "/usr/bin/java",
                    "Params": [{ "Key": "requirements", "Value": "{ \"software\": \"extspread\" }"}]
                },
            ]
        },

        {
            "NodeName": "msu4",
            "NodeAddress": "gridmsu4.sinp.msu.ru",

            "Packages":
            [
                {
                    "Name": "CNM", "Version": "v1",
                    "AppPath": "/usr/bin/java",
                    "Params": [{ "Key": "requirements", "Value": "{ \"software\": \"extmodel\" }"}]
                },

                {
                    "Name": "ISM", "Version": "v1",
                    "AppPath": "/usr/bin/java",
                    "Params": [{ "Key": "requirements", "Value": "{ \"software\": \"extspread\" }"}]
                },
            ]
        }
    ]
}



