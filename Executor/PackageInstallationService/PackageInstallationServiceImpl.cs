using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceProxies.ControllerFarmService;
using ServiceProxies.ResourceBaseService;
using ServiceProxies.SchedulerService;

namespace PackageInstallationService
{
    public class PackageInstallationServiceImpl : IPackageInstallationService, IResultForTicketService
    {

       private IPackageStorageDriver packageStorageDriver = PackageFactoryImpl.GetInstance().GetPackageStorageDriver();

        private IToFarmInstaller farmInstaller = PackageFactoryImpl.GetInstance().GetToFarmInstaller();

       public List<PackageInfo> GetPackagesInfo()
       {
           List<PackageInfo> result = null;
           Common.Utility.ExceptionablePlaceWrapper(() =>
           {
               result = packageStorageDriver.GetPackagesInfo();
           },"GetPackagesInfo failed","GetPackagesInfo succeseded");
           return result;
       }

       public void InstallPackageToNode(string farmName, string resourceName, string nodeName, PackageInfo.PackageChoice choice)
       {
           Common.Utility.ExceptionablePlaceWrapper(() =>
           {
               string address = packageStorageDriver.CreateDownloadPackageTicket(choice);

               var installationTicket = CreateInstallationTicket(resourceName,nodeName, address, PackageFactoryImpl.CallbackAddress, choice.FolderToInstall, choice.FileName, choice.PackageName, choice.VersionName);

               farmInstaller.InstallByNodeName(farmName, installationTicket);
           }, "InstallPackageToNode failed", "InstallPackageToNode succeseded");
       }

       private InstallationTicket CreateInstallationTicket(string resourceName, 
                                                           string nodeName, 
                                                           string downloadAddress, 
                                                           string callbackAddress, 
                                                           string folderToInstall, 
                                                           string fileName, 
                                                           string packageName, 
                                                           string versionName)
       {
           return new InstallationTicket()
           {
               Id = PackageFactoryImpl.GetInstance().GetUniqueId(),
               BackAddress = callbackAddress,
               ResourceName = resourceName,
               NodeName = nodeName,
               StorageAddress = downloadAddress,
               FolderToInstall = folderToInstall,
               PackageName = packageName,
               VersionName = versionName
           };
       }

        public void ReportResult(TicketResult result)
        {
            
            if (result.IsSuccessful)
            {
                //todo add to resource base
                Common.Utility.LogInfo(string.Format("Installation was successful for ticket: {0}",result.TicketId));

                Common.Utility.ExceptionablePlaceWrapper(() =>
                {
                    using (var client = Utility.GetResourceBaseServiceClient())
                    {
                        client.AddInstalledPackage(result.ResourceName, result.NodeName, new PackageOnNode()
                        {
                            AppPath = result.FolderToInstall,
                            Name = result.PackageName,
                            Version = result.VersionName
                        });
                    }
                },
                Common.Utility.Message("Failed to add package description to ResourceBase.",Common.Utility.Arg("PackageName",result.PackageName),
                                                                                            Common.Utility.Arg("VersionName", result.VersionName)),
                Common.Utility.Message("Succeseded to add package description to ResourceBase.", Common.Utility.Arg("PackageName", result.PackageName),
                                                                                            Common.Utility.Arg("VersionName", result.VersionName)));
                
            }
            else
            {
                //todo send exception somewhere
                Common.Utility.LogError(string.Format("Installation was unsuccessful for ticket: {0}. Exception: {1}", result.TicketId, result.OccuredException));
            }
        }
    }
}
