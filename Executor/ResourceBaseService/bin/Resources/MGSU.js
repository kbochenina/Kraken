hadoop_resources =
{
    "ResourceName": "MGSU",
    "ResourceDescription": "МГСУ с ANSYS",
    "SupportedArchitectures": ["MPP", "SMP"],

    "ProviderName": "Linux",

    "HardwareParams":
    [
        
    ],

    "NodeDefaults":
    {
        "SupportedArchitectures": ["MPP", "SMP"],

        "Credentials":
        {
            "Username": "clavire",
            "Password": "20k%H?ih"
        },

        "DataFolders":
        {
            "ExchangeUrlFromSystem": "ftp://192.168.4.11/Tasks/{task}/{phase}",
            "ExchangeUrlFromResource": "\\\\192.168.4.11\\ftp_exchange\\Tasks\\{task}\\{phase}",
            "CopyLocal": "false",
            "LocalFolder": "/home/clavire/tasks/"
        },

        "CoresCount": 8,
        "HardwareParams":
        [
            
        ],

        "Packages":
        [
            {
                "Name": "ANSYS",
                "Version": "v1",
                "AppPath": "TorqueIt2.py"
            }            
        ],

        "OtherSoftware": ["linux"]
    },

    "Nodes":
    [
        { "NodeName": "node02", "NodeAddress": "psi.mgsu.ru", "Services": { "ExecutionUrl": "psi.mgsu.ru"} },
        { "NodeName": "node03", "NodeAddress": "psi.mgsu.ru", "Services": { "ExecutionUrl": "psi.mgsu.ru"} },
        { "NodeName": "node04", "NodeAddress": "psi.mgsu.ru", "Services": { "ExecutionUrl": "psi.mgsu.ru"} },
        { "NodeName": "node05", "NodeAddress": "psi.mgsu.ru", "Services": { "ExecutionUrl": "psi.mgsu.ru"} },
        { "NodeName": "node06", "NodeAddress": "psi.mgsu.ru", "Services": { "ExecutionUrl": "psi.mgsu.ru"} },
        { "NodeName": "node07", "NodeAddress": "psi.mgsu.ru", "Services": { "ExecutionUrl": "psi.mgsu.ru"} },
        { "NodeName": "node08", "NodeAddress": "psi.mgsu.ru", "Services": { "ExecutionUrl": "psi.mgsu.ru"} },
        { "NodeName": "node09", "NodeAddress": "psi.mgsu.ru", "Services": { "ExecutionUrl": "psi.mgsu.ru"} },
        { "NodeName": "node10", "NodeAddress": "psi.mgsu.ru", "Services": { "ExecutionUrl": "psi.mgsu.ru"} },
        { "NodeName": "node11", "NodeAddress": "psi.mgsu.ru", "Services": { "ExecutionUrl": "psi.mgsu.ru"} },
        { "NodeName": "node12", "NodeAddress": "psi.mgsu.ru", "Services": { "ExecutionUrl": "psi.mgsu.ru"} }
    ]
}



