virtual_resources =
{
    "ResourceName": "Virtual",
    "ResourceDescription": "Виртуальный ПК",
    "SupportedArchitectures": ["SMP"],

    "Location": "59.942649;30.29706",

    "ProviderName": "Win PC",

    "HardwareParams":
    [
        { "Key": "LatencyClusterNode", "Value": "1.0" }
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
            "ExchangeUrlFromSystem": "ftp://192.168.4.1/Tasks/{task}/{phase}",
            "ExchangeUrlFromResource": "\\\\192.168.4.1\\ftp_exchange\\Tasks\\{task}\\{phase}",
            "CopyLocal": "false",
            "LocalFolder": "C:\\Temp\\{task}"
        },

        "CoresCount": 0,
        "HardwareParams":
        [
            { "Key": "CoresPerformance", "Value": "[2500]" },
            { "Key": "IS_VIRTUAL", "Value": "true" },
            { "Key": "VIRTUAL_PARAMS", "Value": "{\"Name\":\"rds-c2q\",\"Type\":\"VBOX_REMOTE\",\"Parameters\":{\"hostname\":\"rds-c2q\"}}" }
        ],

        "Packages":
        [
            {
                "Name": "TestP",
                "Version": "v1",
                "AppPath": "C:\\VMI\\_testp\\testp.cmd"
            }
        ],

        "OtherSoftware": ["windows"]
    },

    "Nodes":
    [
        {
            "NodeName": "WXP", "NodeAddress": "192.168.9.10",
            "Services": { "ExecutionUrl": "http://192.168.9.10:8787/RExService" }
        }
    ]
}



