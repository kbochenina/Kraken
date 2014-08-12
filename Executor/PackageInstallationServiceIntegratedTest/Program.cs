using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using ServiceProxies.PackageInstallationService;

namespace PackageInstallationServiceIntegratedTest
{
    class Program
    {
        static void Main(string[] args)
        {
            
            InstallPackageToLinux();
            //InstallPackage();
            //ReturnTicket();



//            using (
//                var client =
//                    new ServiceProxies.ControllerFarmService.InstallationServiceClient(
//                        "http://localhost:8701/ControllerFarmService/PackageInstaller"))
//            {
//                
//                client.InstallPackage();    
//            }

            Console.WriteLine("Press any key to terminate");
            Console.ReadLine();

        }

        static void InstallPackage()
        {
            using (var packageInstallationClient =
                new ServiceProxies.PackageInstallationService.PackageInstallationServiceClient(new BasicHttpBinding(), new EndpointAddress("http://localhost:8755/PackageInstallationService")))
            {
                packageInstallationClient.InstallPackageToNode("localFarm", "nbutakov_dbg", "nbutakov_node_dbg_1", new PackageInfo.PackageChoice() { FileName = "testpackage.zip", FolderToInstall = "D:\\testdir\\installation\\", OSName = "Windows", VersionName = null, PackageName = "FirstPackage" });
            }
        }

        static void InstallPackageToLinux()
        {
            using (var packageInstallationClient =
                new ServiceProxies.PackageInstallationService.PackageInstallationServiceClient(new BasicHttpBinding(), new EndpointAddress("http://localhost:8755/PackageInstallationService")))
            {
                packageInstallationClient.InstallPackageToNode("localFarm", "virtual_ubu_dbg", "ubu_manual", new PackageInfo.PackageChoice() { FileName = "linux_test_package.tar.gz", FolderToInstall = "/home/nano/installation", OSName = "Unix", VersionName = null, PackageName = "FirstPackage" });
            }
        }


        static void ReturnTicket()
        {
            using (var packageInstallationClient =
                new ServiceProxies.PackageInstallationService.ResultForTicketServiceClient(new BasicHttpBinding(), new EndpointAddress("http://localhost:8755/PackageInstallationService/ResultForTicket")))
            {
                var ticketResult = new TicketResult()
                {
                    TicketId = "assssd",
                    IsSuccessful = true,
                    OccuredException = new Exception("dddsssss"),
                };

                packageInstallationClient.ReportResult(ticketResult);
            }
        }
    }
}
