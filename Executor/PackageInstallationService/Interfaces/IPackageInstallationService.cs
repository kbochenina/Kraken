using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;

namespace PackageInstallationService
{
    [ServiceContract]
    public interface IPackageInstallationService
    {
        [OperationContract]
        List<PackageInfo> GetPackagesInfo();

        [OperationContract]
        void InstallPackageToNode(string farmName, string resourceName, string nodeName, PackageInfo.PackageChoice choice);
    }
}
