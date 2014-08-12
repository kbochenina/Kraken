using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceProxies.ControllerFarmService;

namespace PackageInstallationService
{
    interface IToFarmInstaller
    {
        void InstallByNodeName(string farmName, InstallationTicket ticket);
    }
}
