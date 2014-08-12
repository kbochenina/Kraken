using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ControllerFarmService.Controllers.Entity;
using VMServiceInterlayerManager.Debug;

namespace VMIntegrationTests
{
    static class Scenarios
    {
         

        public static void RunVmAndInstallSoftware()
        {
            string vmAddress = "192.168.0.87";
            int port = 22;
            string username = "nano";
            string password = "Yt1NyDpQNm";
            string vmName = "ubu";
            string installationDir = "/home/nano/installation";


            //run ResourceBase
            //run ExecutionBroker
            //run ControllerFarmHost
            //run PackageInstallationService
            //run VBoxWebSrv.exe
            //run VMService

           

            //create mock for scheduler?

            var vm = VMLauncher.RunVM("ubu");

            Thread.Sleep(30000);

            bool isRunning = Utility.IsMachineRunning(vmAddress);

            
            //make addtional actions
            string createDirectory = "cd /home/nano; rm -rf installation; mkdir installation;";
            Utility.SshExec(vmAddress, port, username, password, createDirectory);

            
            Utility.InstallPackageToLinux(installationDir);
            Thread.Sleep(30000);
            bool isInstalled = Utility.IsPackageInstalled(vmAddress,installationDir);
            
            //create task and submit to clavire
            //remove package (optional)
            
            vm.Stop();

        }
    }
}
