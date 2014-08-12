using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using TimeMeter.Integrator;
using Newtonsoft.Json;
using System.Configuration;
using NLog;
using Common;

namespace TimeMeter
{
    public static class TaskTimeMeter
    {
        private static readonly IDictionary<string, Type> ModelTypes = new Dictionary<string, Type>();
        private static readonly Type ModelType = typeof (IModel);
        private static readonly IDictionary<string, StoredApplicationDescription> AppDescriptions = new Dictionary<string, StoredApplicationDescription>();
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(StoredApplicationDescription[]));
        private static readonly IntegratorServiceSoapClient ServiceClient = new IntegratorServiceSoapClient();

        private static IEnumerable<string> ClusterNames = null;
        private static IDictionary<string, ClusterInfo> Clusters = new Dictionary<string, ClusterInfo>();

        private const string PBRepoPath = "PBRepoPath";

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly AppSettingsReader settingsReader = new AppSettingsReader();


        static TaskTimeMeter()
        {
            /*var modelTypes = AppDomain.CurrentDomain.GetAssemblies().ToList()
                .SelectMany(s => s.GetTypes())
                .Where(IsTypeModel);

            foreach (var modelType in modelTypes)
            {
                var referenceName = CreateModel(modelType).ReferenceName;
                ModelTypes[referenceName] = modelType;
            }

            try
            {
                pbRepoPath = (string)settingsReader.GetValue(PBRepoPath, typeof(string));
            }
            catch (Exception e)
            {
                throw new ConfigurationException("Unable to read TimeMeter service configuration params", e);
            }

            if (string.IsNullOrEmpty(pbRepoPath)) throw new ArgumentException("pathOrUrl");

            defResolver = null;
            if (pbRepoPath.StartsWith("http"))
            {
                defResolver = new ScriptedRepository(new HttpFileLoader(pbRepoPath), new RubyScriptEngine());
            }
            else
            {
                defResolver = new ScriptedRepository(new FileSystemFileLoader(pbRepoPath), new RubyScriptEngine());
            }*/
        }

        public static IEnumerable<string> GetClusterNames()
        {
            if (ClusterNames == null)
            {
                Code errorCode;
                ClusterNames = ServiceClient.GetClusterList(out errorCode);
                CheckCode(errorCode);                
            }
            return ClusterNames;
        }

        public static IEnumerable<string> GetAppNames()
        {
            return AppDescriptions.Keys.AsEnumerable();
        }
        
        private static void CheckCode(Code errorCode)
        {
            if (errorCode != Code.OperationSuccess)
            {
                throw new RemoteServiceException(errorCode); 
            }
        }
        
        public static ClusterInfo GetClusterInfo(string clusterName)
        {
            if (!Clusters.ContainsKey(clusterName))
            {
                Code errorCode;
                Clusters[clusterName] = ServiceClient.GetClusterInfo(clusterName, out errorCode);
                CheckCode(errorCode);
            }
            return Clusters[clusterName];
        }

        public static void RefreshData()
        {
            ClusterNames = null;
            Clusters.Clear();
        }

        public static void LoadParameters(string fileName)
        {
            var readStream = File.OpenRead(fileName);
            LoadParameters(readStream);
            readStream.Close();
        }

        public static void LoadParameters(Stream readStream)
        {
            AppDescriptions.Clear();
            foreach (var description in ((StoredApplicationDescription[]) Serializer.Deserialize(readStream)))
            {
                AppDescriptions[description.AppId] = description;
            }
        }

        public static void StoreParameters(string fileName)
        {
            var writeStream = File.Open(fileName, FileMode.Truncate, FileAccess.Write);
            StoreParameters(writeStream);
            writeStream.Close();
        }

        public static void StoreParameters(Stream writeStream)
        {
            Serializer.Serialize(writeStream, AppDescriptions.Values.ToArray());
        }

        public static IModel CreateModel(string modelType)
        {
            if (!ModelTypes.ContainsKey(modelType))
            {
                throw new ModelNotFoundException(modelType);
            }
            return CreateModel(ModelTypes[modelType]);
        }

        private static IModel CreateModel(Type modelType)
        {
            Debug.Assert(IsTypeModel(modelType));
            return (IModel)modelType.GetConstructor(Type.EmptyTypes).Invoke(null);
        }

        public static EstimationResult MeasureAppTime(string applicationId, IDictionary<string, string> parameters, Common.Resource resource, Common.LaunchDestination destination, bool optimize)
        {
            if (!AppDescriptions.ContainsKey(applicationId))
            {
                throw new ApplicationNotFoundException(applicationId);
            }
            var app = new Application(AppDescriptions[applicationId], resource.Name);
            return app.Estimate(parameters, resource, destination, optimize);
        }

        public static Common.Resource CreateResourceRecord(string resourceName)
        {
            var info = TaskTimeMeter.GetClusterInfo(resourceName);
            var res = new Common.Resource();
            res.Nodes = new Common.Node[info.NumberOfNodes];
            res.Nodes = info.Node.Select(n => CreateNodeRecord(resourceName, n)).ToArray();
            res.Parameters[NetworkDegradation.LCN] = info.LatencyClusterNode.ToString();
            //res.LatencyPlannerCluster = info.LatencyPlannerCluster;
            res.Name = info.Name;
            /*res.ThroughputClusterNode = info.ThroughputClusterNode;
            res.ThroughputPlannerCluster = info.ThroughputPlannerCluster;*/
            return res;
        }

        private static Common.Node CreateNodeRecord(string resourceName, TimeMeter.Integrator.NodeInfo info)
        {
            var res = new Common.Node();
            res.ResourceName = resourceName;
            res.DNSName = info.DNSName;
            res.CoresAvailable = info.NumberOfCores;
            res.CoresTotal = info.NumberOfCores;
            res.Parameters[SummaryNodePerformance.CoresPerformance] = JsonConvert.SerializeObject(info.FrequencyOfCores.ToArray());
            return res;
        }

        public static EstimationResult MeasureAppTime(string applicationId, IDictionary<string, string> parameters, string clusterName, Common.LaunchDestination destination, bool optimize)
        {
            if (GetClusterNames().Contains(clusterName))
            {
                var resource = new IntegratorResource(GetClusterInfo(clusterName));
                /*var dst = new Common.LaunchDestination {
                    ResourceName = resource.Name,
                    NodeNames = resource.Nodes.Select(n => n.DNSName).ToArray()
                };*/
                return MeasureAppTime(applicationId, parameters, resource, destination, optimize);
            }
            else
            {
                if (!AppDescriptions.ContainsKey(applicationId))
                {
                    throw new ApplicationNotFoundException(applicationId);
                }
                var app = new Application(AppDescriptions[applicationId], clusterName);
                return app.Estimate(parameters, null, destination, optimize);
            }
        }
        

        private static bool IsTypeModel(Type type)
        {
            return type.GetInterface(ModelType.FullName) != null && !type.IsAbstract && !type.IsInterface && type.GetConstructor(Type.EmptyTypes) != null;
        }

        public class RemoteServiceException : ApplicationException
        {
            public Code ErrorCode { get; private set; }

            public RemoteServiceException(Code errorCode)
            {
                Debug.Assert(errorCode != Code.OperationSuccess);
                ErrorCode = errorCode;
            }

            public override string Message
            {
                get { return String.Format("Integrator web service exception: {0}", ErrorCode); }
            }
        }

        public static void UpdateDescriptions()
        {
            var descr = new StoredApplicationDescription();
            descr.AppId = "SWAN";
            descr.Models = new StoredModelDescription[1];
            descr.Models[0] = new StoredModelDescription();
            descr.Models[0].ModelId = "SWAN_AMDAHL";
            descr.Models[0].MachinesSettings = new MachineSpecificModelSettings[3];
            descr.Models[0].MachinesSettings[0] = new MachineSpecificModelSettings();
            descr.Models[0].MachinesSettings[0].MachineId = "machine1";
            descr.Models[0].MachinesSettings[0].Parameters = new StoredModelParameterDescription[4];
            descr.Models[0].MachinesSettings[0].Parameters[0] = new StoredModelParameterDescription() { ParameterId = "ALPHA", Value = "0.224" };
            descr.Models[0].MachinesSettings[0].Parameters[1] = new StoredModelParameterDescription() { ParameterId = "GAMMA", Value = "0.9" };
            descr.Models[0].MachinesSettings[0].Parameters[2] = new StoredModelParameterDescription() { ParameterId = "P", Value = "2" };
            descr.Models[0].MachinesSettings[0].Parameters[3] = new StoredModelParameterDescription() { ParameterId = "T0", Value = "1445" };
            descr.Models[0].MachinesSettings[1] = new MachineSpecificModelSettings();
            descr.Models[0].MachinesSettings[1].MachineId = "machine2";
            descr.Models[0].MachinesSettings[1].Parameters = new StoredModelParameterDescription[4];
            descr.Models[0].MachinesSettings[1].Parameters[0] = new StoredModelParameterDescription() { ParameterId = "ALPHA", Value = "0.0001" };
            descr.Models[0].MachinesSettings[1].Parameters[1] = new StoredModelParameterDescription() { ParameterId = "GAMMA", Value = "0.593" };
            descr.Models[0].MachinesSettings[1].Parameters[2] = new StoredModelParameterDescription() { ParameterId = "P", Value = "6" };
            descr.Models[0].MachinesSettings[1].Parameters[3] = new StoredModelParameterDescription() { ParameterId = "T0", Value = "816" };
            descr.Models[0].MachinesSettings[2] = new MachineSpecificModelSettings();
            descr.Models[0].MachinesSettings[2].MachineId = "machine3";
            descr.Models[0].MachinesSettings[2].Parameters = new StoredModelParameterDescription[4];
            descr.Models[0].MachinesSettings[2].Parameters[0] = new StoredModelParameterDescription() { ParameterId = "ALPHA", Value = "0.0007" };
            descr.Models[0].MachinesSettings[2].Parameters[1] = new StoredModelParameterDescription() { ParameterId = "GAMMA", Value = "0.602" };
            descr.Models[0].MachinesSettings[2].Parameters[2] = new StoredModelParameterDescription() { ParameterId = "P", Value = "8" };
            descr.Models[0].MachinesSettings[2].Parameters[3] = new StoredModelParameterDescription() { ParameterId = "T0", Value = "703" };
            descr.Models[0].Parameters = new StoredModelParameterDescription[4];
            descr.Models[0].Parameters[0] = new StoredModelParameterDescription() { ParameterId = "ALPHA", Value = "0.224" };
            descr.Models[0].Parameters[1] = new StoredModelParameterDescription() { ParameterId = "GAMMA", Value = "0.9" };
            descr.Models[0].Parameters[2] = new StoredModelParameterDescription() { ParameterId = "P", Value = "2" };
            descr.Models[0].Parameters[3] = new StoredModelParameterDescription() { ParameterId = "T0", Value = "1445" };

            AppDescriptions[descr.AppId] = descr;

            descr = new StoredApplicationDescription();
            descr.AppId = "BSM";
            descr.Models = new StoredModelDescription[1];
            descr.Models[0] = new StoredModelDescription();
            descr.Models[0].ModelId = "BSM";
            descr.Models[0].MachinesSettings = new MachineSpecificModelSettings[0];
            descr.Models[0].Parameters = new StoredModelParameterDescription[0];
            
            /*descr.Models[0].MachinesSettings[0] = new MachineSpecificModelSettings();
            descr.Models[0].MachinesSettings[0].MachineId = "machine1";
            descr.Models[0].MachinesSettings[0].Parameters = new StoredModelParameterDescription[2];
            descr.Models[0].MachinesSettings[0].Parameters[0] = new StoredModelParameterDescription() { ParameterId = "NO_ASM", Value = "825" };
            descr.Models[0].MachinesSettings[0].Parameters[1] = new StoredModelParameterDescription() { ParameterId = "WITH_ASM", Value = "856" };
            descr.Models[0].MachinesSettings[1] = new MachineSpecificModelSettings();
            descr.Models[0].MachinesSettings[1].MachineId = "machine2";
            descr.Models[0].MachinesSettings[1].Parameters = new StoredModelParameterDescription[2];
            descr.Models[0].MachinesSettings[1].Parameters[0] = new StoredModelParameterDescription() { ParameterId = "NO_ASM", Value = "668" };
            descr.Models[0].MachinesSettings[1].Parameters[1] = new StoredModelParameterDescription() { ParameterId = "WITH_ASM", Value = "669" };
            descr.Models[0].MachinesSettings[2] = new MachineSpecificModelSettings();
            descr.Models[0].MachinesSettings[2].MachineId = "machine3";
            descr.Models[0].MachinesSettings[2].Parameters = new StoredModelParameterDescription[2];
            descr.Models[0].MachinesSettings[2].Parameters[0] = new StoredModelParameterDescription() { ParameterId = "NO_ASM", Value = "525" };
            descr.Models[0].MachinesSettings[2].Parameters[1] = new StoredModelParameterDescription() { ParameterId = "WITH_ASM", Value = "532" };            
            descr.Models[0].Parameters[0] = new StoredModelParameterDescription() { ParameterId = "NO_ASM", Value = "825" };
            descr.Models[0].Parameters[1] = new StoredModelParameterDescription() { ParameterId = "WITH_ASM", Value = "856" };*/

            AppDescriptions[descr.AppId] = descr;

            var resCount = 10;

            descr = new StoredApplicationDescription();
            descr.AppId = "SHIPX";
            descr.Models = new StoredModelDescription[1];
            descr.Models[0] = new StoredModelDescription();
            descr.Models[0].ModelId = "SHIPX";
            descr.Models[0].MachinesSettings = new MachineSpecificModelSettings[resCount];
            var r = new Random(System.DateTime.Now.Millisecond);
            
            for (var i = 0; i < resCount; i++)
            {
                descr.Models[0].MachinesSettings[i] = new MachineSpecificModelSettings();
                descr.Models[0].MachinesSettings[i].MachineId = String.Format("machine{0}", i + 1);
                descr.Models[0].MachinesSettings[i].Parameters = new StoredModelParameterDescription[1];
                descr.Models[0].MachinesSettings[i].Parameters[0] = new StoredModelParameterDescription() { ParameterId = "SIM_COUNT", Value = r.Next(20, 1200).ToString() };
            }

            descr.Models[0].Parameters = new StoredModelParameterDescription[1];
            descr.Models[0].Parameters[0] = new StoredModelParameterDescription() { ParameterId = "SIM_COUNT", Value = r.Next(20, 1200).ToString() };

            AppDescriptions[descr.AppId] = descr;

            descr = new StoredApplicationDescription();
            descr.AppId = "LPE";
            descr.Models = new StoredModelDescription[1];
            descr.Models[0] = new StoredModelDescription();
            descr.Models[0].ModelId = "LPE";
            descr.Models[0].MachinesSettings = new MachineSpecificModelSettings[3];
            descr.Models[0].MachinesSettings[0] = new MachineSpecificModelSettings();
            descr.Models[0].MachinesSettings[0].MachineId = "machine1";
            descr.Models[0].MachinesSettings[0].Parameters = new StoredModelParameterDescription[1];
            descr.Models[0].MachinesSettings[0].Parameters[0] = new StoredModelParameterDescription() { ParameterId = "TIME", Value = "6" };
            descr.Models[0].MachinesSettings[1] = new MachineSpecificModelSettings();
            descr.Models[0].MachinesSettings[1].MachineId = "machine2";
            descr.Models[0].MachinesSettings[1].Parameters = new StoredModelParameterDescription[1];
            descr.Models[0].MachinesSettings[1].Parameters[0] = new StoredModelParameterDescription() { ParameterId = "TIME", Value = "2" };
            descr.Models[0].MachinesSettings[2] = new MachineSpecificModelSettings();
            descr.Models[0].MachinesSettings[2].MachineId = "machine3";
            descr.Models[0].MachinesSettings[2].Parameters = new StoredModelParameterDescription[1];
            descr.Models[0].MachinesSettings[2].Parameters[0] = new StoredModelParameterDescription() { ParameterId = "TIME", Value = "2" };
            descr.Models[0].Parameters = new StoredModelParameterDescription[1];
            descr.Models[0].Parameters[0] = new StoredModelParameterDescription() { ParameterId = "TIME", Value = "3" };

            AppDescriptions[descr.AppId] = descr;

            foreach (var d in AppDescriptions)
            {
                foreach (var m in d.Value.Models)
                {
                    if (m.MachinesSettings == null)
                    {
                        m.MachinesSettings = new MachineSpecificModelSettings[0];
                    }
                }
            }

            descr = new StoredApplicationDescription()
            {
                AppId = Models.TestPModel.TESTP,
                Models = new StoredModelDescription[] {
                    new StoredModelDescription()
                    {
                        MachinesSettings = new MachineSpecificModelSettings[] {},
                        ModelId = Models.TestPModel.TESTP,
                        Parameters = new StoredModelParameterDescription[] {}
                    }
                }
            };

            AppDescriptions[descr.AppId] = descr;
        }
    }
}
