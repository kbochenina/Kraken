using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;
using ServiceProxies.ControllerFarmService;
using ServiceProxies.ResourceBaseService;

namespace PackageInstallationService.Impl
{
    class ToFarmInstallerImpl : IToFarmInstaller
    {
        public void InstallByNodeName(string farmName, InstallationTicket ticket)
        {
            Resource[] resources = ResourceBase.GetAllResources();

            var resource =
                resources.First(x => (farmName==null? true: x.Controller.FarmId == farmName) 
                                        && x.ResourceName == ticket.ResourceName 
                                        && x.Nodes.Any(y => y.NodeName == ticket.NodeName)
                                );

            if (resource == null)
            {
                throw new InvalidDataException(string.Format("Node wasn't found with FarmName: {0}, ResourceName: {1}, NodeName: {2}", farmName, ticket.ResourceName, ticket.NodeName));
            }

            Common.Utility.ExceptionablePlaceWrapper(() =>
            {
                var address = resource.Controller.Url + "PackageInstaller";

                using (var client = PackageFactoryImpl.GetInstallationServiceClient(address))
                {
                    client.InstallPackage(ticket);
                }
            });
        }
    }
}
