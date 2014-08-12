using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common;
using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class Resource
    {
        public Resource()
        {
            Parameters = new Dictionary<string, string>();
        }

        [DataMember]
        public Dictionary<string, string> Parameters
        {
            get;
            set;
        }

        [DataMember]
        public string Name
        {
            get;
            set;
        }

        [DataMember]
        public Node[] Nodes
        {
            get;
            set;
        }

        /*[DataMember]
        public double LatencyClusterNode
        {
            get;
            set;
        }

        [DataMember]
        public double LatencyPlannerCluster
        {
            get;
            set;
        }

        [DataMember]
        public double ThroughputClusterNode
        {
            get;
            set;
        }

        [DataMember]
        public double ThroughputPlannerCluster
        {
            get;
            set;
        }*/
    }

    [DataContract]
    public class Node
    {
        public Node()
        {
            Parameters = new Dictionary<string, string>();
        }

        [DataMember]
        public string ResourceName
        {
            get;
            set;
        }

        [DataMember]
        public string DNSName
        {
            get;
            set;
        }

        /*[DataMember]
        public double[] FrequencyOfCores
        {
            get;
            set;
        }*/

        [DataMember]
        public int CoresAvailable
        {
            get;
            set;
        }

        [DataMember]
        public int CoresTotal
        {
            get;
            set;
        }

        /*[DataMember]
        public double[] PerformanceOfCores
        {
            get;
            set;
        }

        [DataMember]
        public double RAMSize
        {
            get;
            set;
        }*/

        [DataMember]
        public Dictionary<string, string> Parameters
        {
            get;
            set;
        }
    }

    [DataContract]
    [Serializable]
    public class LaunchDestination : IEquatable<LaunchDestination>
    {
        [DataMember]
        public string ResourceName;

        [DataMember]
        public string[] NodeNames;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (obj.GetType() != typeof(LaunchDestination)) return false;
            return Equals((LaunchDestination)obj);
        }

        public bool Equals(LaunchDestination other)
        {
            /*if (ReferenceEquals(null, other) || !Equals(other.ResourceName, ResourceName))
            {
                return false;
            }
            var q = from a in NodeNames join b in other.NodeNames on a equals b select a;                
            return NodeNames.Length == other.NodeNames.Length && q.Count() == NodeNames.Length;*/
            return !ReferenceEquals(null, other) && other.ResourceName == ResourceName && NodeNames.SequenceEqual(other.NodeNames);
        }

        public bool IsLike(LaunchDestination d)
        {
            if (d == null)
            {
                return false;
            }
            return d.ResourceName == ResourceName && NodeNames != null && ReferenceEquals(NodeNames, null) == ReferenceEquals(d.NodeNames, null) && NodeNames.Intersect(d.NodeNames).Count() > 0;
        }

        public override int GetHashCode()
        {
            return ResourceName.GetHashCode() + NodeNames.GetHashCode();
        }

        public static bool operator ==(LaunchDestination left, LaunchDestination right)
        {
            if (ReferenceEquals(left, null) != ReferenceEquals(right, null))
            {
                return false;
            }
            if (ReferenceEquals(left, null))
            {
                return true;
            }
            return left.Equals(right);
        }

        public static bool operator !=(LaunchDestination left, LaunchDestination right)
        {
            if (ReferenceEquals(left, null) != ReferenceEquals(right, null))
            {
                return true;
            }
            if (ReferenceEquals(left, null))
            {
                return false;
            }
            return !left.Equals(right);
        }
    }

    public class DestComparer : IEqualityComparer<LaunchDestination>
    {
        public bool Equals(LaunchDestination x, LaunchDestination y)
        {
            if (ReferenceEquals(null, x) != ReferenceEquals(null, y))
            {
                return false;
            }
            else if (ReferenceEquals(null, x))
            {
                return true;
            }
            else
            {
                return x.Equals(y);
            }
        }

        public int GetHashCode(LaunchDestination obj)
        {
            return obj.GetHashCode();
        }
    }
}
