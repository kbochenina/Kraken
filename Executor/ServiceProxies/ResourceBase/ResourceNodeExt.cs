using System;
using System.Collections.Generic;
using System.Linq;

namespace ServiceProxies.ResourceBaseService
{
    public partial class ResourceNode
    {
        public int CoresAvailable { get; set; }
        public int SubmissionsAvailable { get; set; }

        public IDictionary<string, string> StaticHardwareParams { get { return HardwareParams; } }

        public bool HasPackage(string packageName)
        {
            return Packages.Any(p => String.Equals(p.Name, packageName, StringComparison.InvariantCultureIgnoreCase));
        }

        public PackageOnNode PackageByName(string packageName)
        {
            return Packages.First(p => String.Equals(p.Name, packageName, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}