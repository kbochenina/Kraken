using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using ServiceProxies.ControllerFarmService;

namespace PackageInstallationService
{
    [DataContract]
    public class TicketResult
    {
        [DataMember]
        public string TicketId { get; set; }

        [DataMember]
        public bool IsSuccessful { get; set; }

        [DataMember]
        public Exception OccuredException { get; set; }

        [DataMember]
        public string PackageName { get; set; }

        [DataMember]
        public string VersionName { get; set; }

        [DataMember]
        public string ResourceName { get; set; }

        [DataMember]
        public string NodeName { get; set; }

        [DataMember]
        public string FolderToInstall { get; set; }
    }
}
