using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PackageInstallationService.Interfaces
{
    interface IMetadataProccessor
    {
        void Process(string filePath, PackageInfo.NamedInstance inst);
    }
}
