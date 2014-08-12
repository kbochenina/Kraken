using System;

namespace ControllerFarmService.Installation.Interfaces
{
    interface IPackageObtainer
    {
        void ObtainPackage(string address, Action<string,Exception> packageObtainedCallback);
    }
}
