using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace MITP
{
    [DataContract]
    public class NodeDataFolders
    {
        [DataMember(Order=0)] public string ExchangeUrlFromSystem { get; protected set; }
        [DataMember(Order=1)] public string ExchangeUrlFromResource { get; protected set; }

        [DataMember(Order=2)] public bool CopyLocal { get; protected set; }
        [DataMember(Order=3)] public string LocalFolder { get; protected set; }

        public NodeDataFolders(NodeDataFolders otherFolders)
        {
            if (otherFolders != null)
            {
                this.ExchangeUrlFromSystem   = otherFolders.ExchangeUrlFromSystem;
                this.ExchangeUrlFromResource = otherFolders.ExchangeUrlFromResource;

                this.CopyLocal   = otherFolders.CopyLocal;
                this.LocalFolder = otherFolders.LocalFolder;
            }
        }
    }
}