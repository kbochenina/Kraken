using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PackageInstallationService
{
    public interface IPackageStorageDriver
    {
        List<PackageInfo> GetPackagesInfo();

        //todo think about of return type. 
        string CreateDownloadPackageTicket(PackageInfo.PackageChoice choice);
    }
}
