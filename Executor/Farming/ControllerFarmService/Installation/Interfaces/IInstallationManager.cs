//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;

namespace ControllerFarmService.Installation.Interfaces
{
    interface IInstallationManager
    {
        void InstallByTicket(Resource resource, InstallationTicket ticket,IInstallationController controller);
    }
}
