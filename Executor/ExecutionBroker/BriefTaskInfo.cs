using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace MITP
{
    [DataContract]
    public class BriefTaskInfo
    {
        [DataMember] public ulong  TaskId { get; internal set; }
        [DataMember] public string WfId   { get; internal set; }
        [DataMember] public string UserId { get; internal set; }

        [DataMember] public string Package { get; internal set; }
        
        [DataMember] public string ResourceName { get; internal set; }
        [DataMember] public IEnumerable<string> NodeAddresses { get; internal set; }

        [DataMember] public TaskState State { get; internal set; }
    }
}