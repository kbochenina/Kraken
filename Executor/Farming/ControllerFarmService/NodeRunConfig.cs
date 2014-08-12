using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace MITP
{
    [DataContract]
    public class NodeRunConfig
    {
        [DataMember] public string NodeName     { get; private set; }
        [DataMember] public string ResourceName { get; private set; }
        [DataMember] public uint   Cores        { get; private set; }

        NodeRunConfig(string resourceName, string nodeName, uint cores)
        {
            ResourceName = resourceName;
            NodeName     = nodeName;
            Cores        = cores;
        }
    }
}
