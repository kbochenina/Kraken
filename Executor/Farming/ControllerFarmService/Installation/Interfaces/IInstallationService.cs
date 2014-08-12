using System.ServiceModel;

namespace ControllerFarmService.Installation.Interfaces
{
    [ServiceContract]
    public interface IInstallationService
    {
        [OperationContract(IsOneWay=true)]
        void InstallPackage(InstallationTicket ticket);
    }
}
