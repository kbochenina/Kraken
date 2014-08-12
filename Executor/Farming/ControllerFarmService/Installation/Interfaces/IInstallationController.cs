//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;

namespace ControllerFarmService.Installation.Interfaces
{
    interface IInstallationController
    {
        void InstallByTicket(InstallationTicket ticket, ResourceNode node, string localAddress);
    }
}
