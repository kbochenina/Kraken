using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace PackageInstallationService
{
    [DataContract]
    public class PackageInfo
    {
        public PackageInfo()
        {
            VersionedInstances = new Dictionary<string, List<NamedInstance>>();
        }

        [DataMember]
        public string PackageName{get; set;}

        [DataMember]
        public Dictionary<string/*OS_name*/, List<NamedInstance>> VersionedInstances { get; set; } 

        [DataContract]
        public class NamedInstance
        {
            [DataMember]
            public string VersionName { get; set; }

            [DataMember]
            public string FileName { get; set; }

            [DataMember]
            public MetaData Metadata { get; set; }
        }

        [DataContract]
        public class PackageChoice
        {
            [DataMember]
            public string PackageName { get; set; }

            [DataMember]
            public string OSName { get; set; }

            [DataMember]
            public string VersionName { get; set; }

            [DataMember]
            public string FileName { get; set; }

            [DataMember]
            public string FolderToInstall { get; set; }      
        }

        [DataContract]
        public class MetaData
        {
            [DataMember]
            public string FolderToInstall { get; set; }
        }

        public static PackageChoice CreateChoiceFromInfo(PackageInfo info, string osName, string versionName)
        {
           var versionInstance = info.VersionedInstances[osName].Find(x => x.VersionName == versionName);
            var choice = new PackageChoice()
            {
                PackageName = info.PackageName,
                OSName = osName,
                VersionName = versionName,
                FileName = versionInstance.FileName,
                FolderToInstall = versionInstance.Metadata.FolderToInstall,
            };
            return choice;
        }
    }

    

}
