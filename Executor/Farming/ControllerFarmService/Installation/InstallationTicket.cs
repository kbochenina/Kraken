using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace ControllerFarmService.Installation
{
    [DataContract]
    public class InstallationTicket
    {
        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public string BackAddress { get; set; }

        [DataMember]
        public string ResourceName { get; set; }

        [DataMember]
        public string NodeName { get; set; }

        [DataMember]
        public string StorageAddress { get; set;}

        //todo rename this property
        [DataMember]
        public string FolderToInstall { get; set; }

        [DataMember]
        public string PackageName { get; set; }

        [DataMember]
        public string VersionName { get; set; }
    }
}
