using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.ServiceModel;
using System.Text;
using ControllerFarmService.Controllers.Entity;
using Renci.SshNet;
using ServiceProxies.PackageInstallationService;

namespace VMIntegrationTests
{
    class Utility
    {

        public static void InstallPackageToLinux(string install_dir = "/home/nano/installation")
        {
            using (var packageInstallationClient =
                new ServiceProxies.PackageInstallationService.PackageInstallationServiceClient(new BasicHttpBinding(), new EndpointAddress("http://localhost:8755/PackageInstallationService")))
            {
                packageInstallationClient.InstallPackageToNode("localFarm", "virtual_ubu_dbg", "nbutakov_vm_ubu", new PackageInfo.PackageChoice() { FileName = "linux_test_package.tar.gz", FolderToInstall = install_dir, OSName = "Unix", VersionName = null, PackageName = "FirstPackage" });
            }
        }

        public static bool IsMachineRunning(string address)
        {
            var ping = new Ping();

            var reply = ping.Send(address, 10000);

            return (reply.Status == IPStatus.Success) ? true : false;
        }

        public static bool IsPackageInstalled(string host, string filePath)
        {
            int port = 22;
            string username = "nano";
            string password = "Yt1NyDpQNm";

            var command = string.Format("cd {0}; ls | wc -l;", filePath);

            var result = SshExec(host, port, username, password, command);

            return (Int32.Parse(result) > 0);
        }

        public static string SshExec(string host, int port, string username, string password, string command)
        {
            var sshClient = new SshClient(host, port, username, password);
            sshClient.Connect();
            var executedCommand = sshClient.RunCommand(command);

            var result = string.Format("Result: {0}\r\nExitStatus: {1}\r\nError: {2}",
                                       executedCommand.Result,
                                       executedCommand.ExitStatus,
                                       executedCommand.Error);
            Console.WriteLine(result);

            if (executedCommand.ExitStatus == 0)
            {
                return executedCommand.Result;
            }
            else
            {
                throw new Exception(result);
            }
        }
    }
}
