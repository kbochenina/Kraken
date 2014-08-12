{
  "ResourceName": "nbutakov_dbg",
  "ResourceDescription": "nbutakov on DevMachine",
  "SupportedArchitectures": [
    "SMP"
  ],
  "Controller": {
    "FarmId": "localFarm",
    "Type": "ExtendedWindowsController",
    "Url": "http://localhost:8701/ControllerFarmService/"
  },
  "HardwareParams": [
    {
      "Key": "LatencyClusterNode",
      "Value": "0.0"
    }
  ],
  "NodeDefaults": {
    "SupportedArchitectures": [
      "SMP"
    ],
    "Credentials": {
      "Username": "nano",
      "Password": "Yt1NyDpQNm"
    },
    "DataFolders": {
      "ExchangeUrlFromSystem": "ftp://192.168.4.2/{task}/{phase}",
      "ExchangeUrlFromResource": "\\\\192.168.4.2\\Tasks\\{task}\\{phase}",
      "CopyLocal": "false",
      "LocalFolder": "C:\\Temp\\{task}"
    },
    "CoresCount": 4,
    "HardwareParams": [
      {
        "Key": "CoresPerformance",
        "Value": "[2000, 2000, 2000, 2000]"
      }
    ],
    "Packages": [
      {
        "Name": "TestP",
        "Version": "v1",
        "AppPath": "D:\\CLAVIRE\\_testp\\testp.exe"
      }
    ],
    "OtherSoftware": [
      "windows"
    ]
  },
  "Nodes": [
    {
      "NodeName": "nbutakov_node_dbg_1",
      "NodeAddress": "192.168.0.222:8740",
      "Services": {
        "ExecutionUrl": "http://192.168.0.222:8740/RExService"
      },
      "Packages": [
        {
          "Name": "testpfg",
          "Version": "v1",
          "AppPath": "D:\\ClavireWorkspace\\Farming\\TestP\\testp.exe"
        },
        {
          "Name": "testp",
          "Version": "v1",
          "AppPath": "D:\\ClavireWorkspace\\Farming\\TestP\\testp.exe"
        },
        {
          "Name": "FirstPackage",
          "Version": null,
          "AppPath": "D:\\testdir\\"
        }
      ]
    }
  ]
}