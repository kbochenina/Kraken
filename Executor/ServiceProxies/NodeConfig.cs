using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;

namespace MITP
{
    public struct NodeConfig
    {
        public string NodeName { get; set; }
        public string ResourceName { get; set; }
        public uint Cores { get; set; }
    }
}

