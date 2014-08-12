using System;
using System.Collections.Generic;
using System.Linq;
using TimeMeter.Integrator;
using Common;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using System.Globalization;

namespace TimeMeter
{
    public static class ClusterParameterReader
    {
        private static readonly Type ResourceParameterType = typeof (IResourceHardwareParameter);
        private static readonly Type NodeParameterType = typeof(INodeHardwareParameter);

        private static readonly IDictionary<string, Type> ResourceParameterTypes = new Dictionary<string, Type>();
        private static readonly IDictionary<string, Type> NodeParameterTypes = new Dictionary<string, Type>();

        static ClusterParameterReader()
        {
            var resParameterTypes = AppDomain.CurrentDomain.GetAssemblies().ToList()
                .SelectMany(s => s.GetTypes())
                .Where(IsResourceParameter);
            foreach (var type in resParameterTypes)
            {
                ResourceParameterTypes[((IResourceHardwareParameter) type.GetConstructor(Type.EmptyTypes).Invoke(null)).Id] = type;
            }

            var nodeParameterTypes = AppDomain.CurrentDomain.GetAssemblies().ToList()
                .SelectMany(s => s.GetTypes())
                .Where(IsNodeParameter);
            foreach (var type in nodeParameterTypes)
            {
                NodeParameterTypes[((INodeHardwareParameter)type.GetConstructor(Type.EmptyTypes).Invoke(null)).Id] = type;
            }
        }

        internal static string GetValue(string parameterId, ClusterInfo cluster, LaunchDestination destination)
        {
            return GetValue(parameterId, new IntegratorResource(cluster), destination);
        }

        internal static string GetValue(string parameterId, string resourceName, NodeInfo node)
        {
            return GetValue(parameterId, new IntegratorResource.IntegratorNode(resourceName, node));
        }

        public static string GetValue(string parameterId, Resource resource, LaunchDestination destination)
        {
            if (resource == null || !ResourceParameterTypes.ContainsKey(parameterId))
            {
                throw new HardwareParameterException(parameterId, resource == null ? null : resource.Name);
            }

            return ((IResourceHardwareParameter)ResourceParameterTypes[parameterId].GetConstructor(Type.EmptyTypes).Invoke(null)).GetValue(resource, destination);
        }

        public static string GetValue(string parameterId, Node node)
        {
            if (node == null || !NodeParameterTypes.ContainsKey(parameterId))
            {
                throw new NodeParameterException(parameterId, node == null ? null : node.ResourceName, node == null ? null : node.DNSName);
            }

            return ((INodeHardwareParameter)NodeParameterTypes[parameterId].GetConstructor(Type.EmptyTypes).Invoke(null)).GetValue(node);
        }

        public static IEnumerable<string> GetAvailableParameterIds()
        {
            return ResourceParameterTypes.Keys.AsEnumerable();
        }

        internal static bool IsResourceParameter(Type type)
        {
            return type.GetInterface(ResourceParameterType.FullName) != null && !type.IsAbstract && !type.IsInterface && type.GetConstructor(Type.EmptyTypes) != null;
        }

        internal static bool IsNodeParameter(Type type)
        {
            return type.GetInterface(NodeParameterType.FullName) != null && !type.IsAbstract && !type.IsInterface && type.GetConstructor(Type.EmptyTypes) != null;
        }
    }

    public interface IResourceHardwareParameter
    {
        string Id { get; }
        string GetValue(Resource resource, LaunchDestination destination);
    }

    public interface INodeHardwareParameter
    {
        string Id { get; }
        string GetValue(Node resource);
    }

    public abstract class BoldExtractor : IResourceHardwareParameter
    {
        public string GetValue(Resource resource, LaunchDestination destination)
        {
            if (resource.Parameters.ContainsKey(Key))
            {
                return resource.Parameters[Key];
            }
            else
            {
                throw new HardwareParameterException(Id, resource.Name);
            }
        }

        public abstract string Id { get; }

        protected abstract string Key { get; }
    }

    public abstract class BoldNodeExtractor : INodeHardwareParameter
    {
        public abstract string Id { get; }

        protected abstract string Key { get; }


        public virtual string GetValue(Node node)
        {
            if (node.Parameters.ContainsKey(Key))
            {
                return node.Parameters[Key];
            }
            else
            {
                throw new NodeParameterException(Id, node.ResourceName, node.DNSName);
            }
        }
    }

    public class SummaryNodePerformance : BoldNodeExtractor
    {

        public const string CoresPerformance = "CoresPerformance";

        public override string Id
        {
            get { return AverageResourcePerformance.PERF; }
        }

        protected override string Key
        {
            get { return CoresPerformance; }
        }

        protected double[] GetPerfs(Node node)
        {
            var str = base.GetValue(node);
            return JsonConvert.DeserializeObject<double[]>(str);
        }

        public override string GetValue(Node node)
        {            
            return GetPerfs(node).Sum().ToString(CultureInfo.InvariantCulture.NumberFormat);
        }
    }

    public class TotalNodeCores : SummaryNodePerformance
    {
        public const string TCORES = "P_TOTAL";

        public override string Id
        {
            get { return TCORES; }
        }

        protected override string Key
        {
            get { return SummaryNodePerformance.CoresPerformance; }
        }

        public override string GetValue(Node node)
        {
            return GetPerfs(node).Length.ToString(CultureInfo.InvariantCulture);
        }
    }

    public class AverageNodeCorePerformance : SummaryNodePerformance
    {
        public const string AvgNodeCorePerf = "AvgNodeCorePerf";

        public override string Id
        {
            get
            {
                return AvgNodeCorePerf;
            }
        }

        public override string GetValue(Node node)
        {
            return (Double.Parse(base.GetValue(node), CultureInfo.InvariantCulture.NumberFormat) / node.CoresTotal).ToString(CultureInfo.InvariantCulture.NumberFormat);
        }
    }

    /// <summary>
    /// Количество узлов
    /// </summary>
    public class NodesCountExtractor : IResourceHardwareParameter
    {
        public const string NODES = "NODES";

        public string Id
        {
            get { return NODES; }
        }

        public string GetValue(Resource resource, LaunchDestination destination)
        {
            return resource.Nodes.Join(destination.NodeNames, n => n.DNSName, n => n, (n, name) => n).Distinct().Count().ToString();
        }
    }

    /// <summary>
    /// Среднее количество процессоров на узел
    /// Округление вниз
    /// </summary>
    public class ProcessorCountPerNode : IResourceHardwareParameter
    {
        public const string P = "P";

        public string Id
        {
            get { return P; }
        }

        public string GetValue(Resource resource, LaunchDestination destination)
        {
            var nodes = resource.Nodes.Join(destination.NodeNames, n => n.DNSName, n => n, (n, name) => n).Distinct();
            return Math.Floor((double)nodes.Sum(node => node.CoresTotal) / nodes.Count()).ToString();
        }
    }

    /// <summary>
    /// Коэффициент сетевой деградации
    /// </summary>
    public class NetworkDegradation : BoldExtractor
    {
        public const string LCN = "LatencyClusterNode";
        public const string HWDEG = "HWDEG";

        public override string Id
        {
            get { return HWDEG; }
        }

        protected override string Key
        {
            get { return LCN; }
        }
    }

    /// <summary>
    /// Средняя производительность узлов
    /// </summary>
    public class AverageResourcePerformance : IResourceHardwareParameter
    {
        public const string PERF = "PERF";

        public string Id
        {
            get { return PERF; }
        }

        public string GetValue(Resource resource, LaunchDestination destination)
        {
            var nodes = resource.Nodes.Join(destination.NodeNames, n => n.DNSName, n => n, (n, name) => n).Distinct();
            var cores = nodes.Select(n => Int32.Parse(ClusterParameterReader.GetValue(TotalNodeCores.TCORES, n), CultureInfo.InvariantCulture)).Sum();
            var perf = nodes.Select(n => Double.Parse(ClusterParameterReader.GetValue(PERF, n), CultureInfo.InvariantCulture.NumberFormat)).Sum();
            return (perf / cores).ToString(CultureInfo.InvariantCulture.NumberFormat);
        }
    }

    public class IsNodeVirtual : BoldNodeExtractor
    {
        public const string IS_VIRTUAL = "IS_VIRTUAL";

        public override string Id
        {
            get { return IS_VIRTUAL; }
        }

        protected override string Key
        {
            get { return IS_VIRTUAL; }
        }

        public override string GetValue(Node node)
        {
            try
            {
                return base.GetValue(node);
            }
            catch (NodeParameterException)
            {
                return false.ToString();
            }
        }
    }

    public class VirtualConnectionParams : BoldNodeExtractor
    {
        public const string VCP = "VIRTUAL_PARAMS";

        public override string Id
        {
            get { return VCP; }
        }

        protected override string Key
        {
            get { return VCP; }
        }
    }

    public class HardwareParameterException : ApplicationException
    {
        public string ParameterId { get; private set; }
        public string ResourceName { get; private set; }
        

        public HardwareParameterException(string parameterId, string resourceName)
        {
            ParameterId = parameterId;
            ResourceName = resourceName;
        }

        public override string Message
        {
            get { return String.Format("Could not extract hardware parameter: {0}, resource: {1}", ParameterId, ResourceName == null ? "NULL" : ResourceName); }            
        }
    }

    public class NodeParameterException : HardwareParameterException
    {
        public string NodeName { get; private set; }

        public NodeParameterException(string parameterId, string resourceName, string nodeName)
            : base(parameterId, resourceName)
        {
            NodeName = nodeName;            
        }

        public override string Message
        {
            get { return String.Format("{0}, node: {1}", base.Message, NodeName == null ? "NULL" : NodeName); }
        }
    }

    public class IntegratorResource : Resource
    {
        public IntegratorResource(ClusterInfo info)
        {
            Nodes = info.Node.Select(n => new IntegratorNode(info.Name, n)).ToArray();
            Parameters[NetworkDegradation.LCN] = info.LatencyClusterNode.ToString();
            //LatencyPlannerCluster = info.LatencyPlannerCluster;
            Name = info.Name;
            /*ThroughputClusterNode = info.ThroughputClusterNode;
            ThroughputPlannerCluster = info.ThroughputPlannerCluster;*/
        }
        
        internal class IntegratorNode : Node
        {
            public IntegratorNode(string resourceName, NodeInfo info)
                : base()
            {
                ResourceName = resourceName;
                DNSName = info.DNSName;
                Parameters[SummaryNodePerformance.CoresPerformance] = JsonConvert.SerializeObject(info.FrequencyOfCores.ToArray());
                CoresAvailable = info.NumberOfCores;
                CoresTotal = info.NumberOfCores;
            }
        }
    }
}
