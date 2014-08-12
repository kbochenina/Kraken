using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using ControllerFarmService.Installation.Interfaces;
using MITP;
using ServiceProxies.RExInstallationService;
using ServiceProxies.ResourceBaseService;

namespace ControllerFarmService.Installation.Impl
{
    class WindowsInstallerImpl : IInstallationController
    {
        public delegate void UploadFile(ResourceNode node, string remotePath, string localPath, string taskId, string farmId);

        private UploadFile _uploadFile;

        public WindowsInstallerImpl(UploadFile uploadFile)
        {
            _uploadFile = uploadFile;
        }

        private static InstallationServiceClient GetInstallationServiceClient(ResourceNode node)
        {
            var binding = new BasicHttpBinding
            {
                MaxReceivedMessageSize = 65536,
                MaxBufferSize = 65536,
                CloseTimeout = new TimeSpan(0, 1, 0),
                OpenTimeout = new TimeSpan(0, 1, 0),
                ReceiveTimeout = new TimeSpan(0, 10, 0),
                SendTimeout = new TimeSpan(0, 1, 0)
            };

            var address = new EndpointAddress("http://" + node.NodeAddress + "/Installation");

            return new InstallationServiceClient(binding, address);
        }

        public void InstallByTicket(InstallationTicket ticket, ResourceNode node, string localAddress)
        {

            var farmId = ResourceCache.GetByName(node.ResourceName).Resource.Controller.FarmId;
            var fileName = Path.GetFileName(localAddress);

            var uri = new Uri(ticket.StorageAddress);

            var taskId = string.Format("{0}.{1}.{2}", uri.Host, uri.LocalPath,
                    String.Format("{0:dd/MM/yyyy_HH-mm-ss}", DateTime.Now));

            this._uploadFile(node, fileName, localAddress, taskId, farmId);

            using (var client = GetInstallationServiceClient(node))
            {
                ticket.FolderToInstall = client.InstallPackage(farmId, taskId, fileName);
            }
        }
    }
}
