hadoop_resources =
{
    "ResourceName": "hadoop",
    "ResourceDescription": "Сервер распределенных вычислений под управлением Apache Hadoop",
    "SupportedArchitectures": ["MapReduce"],

    "ProviderName": "Hadoop",

    "HardwareParams":
    [
        
    ],

    "NodeDefaults":
    {
        "SupportedArchitectures": ["MapReduce"],

        "Credentials":
        {
            "Username": "clavire",
            "Password": "7777777"
        },

        "DataFolders":
        {
            "ExchangeUrlFromSystem": "ftp://Anonymous@192.168.4.1/Tasks/{task}/{phase}",
            "ExchangeUrlFromResource": "ftp://Anonymous@10.253.0.54/Tasks/{task}/{phase}",
            "CopyLocal": "true",
            "LocalFolder": "/home/clavire/temp/{task}"
        },

        "CoresCount": 1,
        "HardwareParams":
        [
            
        ],

        "Packages":
        [
            {
                "Name": "crawler",
                "Version": "v1",
                "AppPath": "/home/clavire/temp/{task}/clavire.sh"
            }            
        ],

        "OtherSoftware": ["linux"]
    },

    "Nodes":
    [
        {
            "NodeName": "Hadoop", "NodeAddress": "77.234.203.186",
            "Services": { "ExecutionUrl": "77.234.203.186:10822" }
        }        
    ]
}



