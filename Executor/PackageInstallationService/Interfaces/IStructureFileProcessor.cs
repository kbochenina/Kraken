using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PackageInstallationService.Interfaces
{
    interface IStructureFileProcessor
    {
        List<PackageInfo> Process(string pathToStructureFile);
    }
}
