using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using MITP;
using PackageInstallationService.Interfaces;

namespace PackageInstallationService.Impl
{
    class XmlMetadataProcessor : IMetadataProccessor
    {
        public void Process(string filePath, PackageInfo.NamedInstance inst)
        {
            var doc = XDocument.Load(filePath);
            inst.Metadata = new PackageInfo.MetaData();
            var pathToInstall = doc.Root.Element("PathToInstall");
            if (pathToInstall != null)
            {
                inst.Metadata.FolderToInstall = pathToInstall.Value;    
            }
        }
    }
}
