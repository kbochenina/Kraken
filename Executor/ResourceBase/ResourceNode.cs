using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;

namespace MITP
{
    [DataContract]
    public class NodeCredentials
    {
        [DataMember(Order=0, IsRequired=true)]  public string Username { get; private set; }
        [DataMember(Order=1, IsRequired=true)]  public string Password { get; private set; }
        [DataMember(Order=2, IsRequired=false)] public string CertFile { get; private set; }

        public NodeCredentials(string userName, string password, string certFile = null)
        {
            Username = userName;
            Password = password;
            CertFile = certFile;
        }
    }

    [DataContract]
    public class NodeServices
    {
        [DataMember(IsRequired=true)] public string ExecutionUrl { get; private set; }
        //[DataMember] public string MonitoringUrl;
        //[DataMember] public string TaskInfoUrl;

        public NodeServices(NodeServices otherServices = null)
        {
            if (otherServices != null)
            {
                this.ExecutionUrl = otherServices.ExecutionUrl;
            }
        }
    }

    [DataContract]
    public class ResourceNode
    {
        [DataMember(Order=0)]  // , IsRequired=true
        public string NodeName { get; private set; }

        [DataMember(Order=1)]  // , IsRequired=true
        public string NodeAddress { get; private set; }  // may differ from provider’s        

        [DataMember(Order=2)]
        public IEnumerable<string> SupportedArchitectures { get; private set; }

        [DataMember]
        public uint CoresCount { get; private set; }

        [DataMember]
        public uint TasksSubmissionLimit  { get; private set; }

        [DataMember] public NodeServices    Services    { get; private set; }
        [DataMember] public NodeDataFolders DataFolders { get; private set; }
        [DataMember] public NodeCredentials Credentials { get; private set; }

        [DataMember(Name="HardwareParams")] 
        public IDictionary<string, string> StaticHardwareParams { get; private set; } 

        // todo : ResourceNode.IntervalUpdatingTaskStates?

        [DataMember(Name="Packages")]
        private IList<PackageOnNode> _packages { get; set; }
        public ReadOnlyCollection<PackageOnNode> Packages { get; private set; }

        [DataMember(Name="OtherSoftware")]
        private IList<string> _otherSoftware { get; set; }
        public ReadOnlyCollection<string> OtherSoftware { get; private set; }

        [DataMember]
        public string ResourceName { get; internal set; } 

        internal void CheckConsistency()
        {
            // todo : Check Resource node Consistency
            // todo : Custom Exception
        }
        
        private void Init()
        {
            if (Credentials == null)
                Credentials = new NodeCredentials(null, null, null);

            if (SupportedArchitectures == null)
                SupportedArchitectures = new string[0];

            if (_packages == null)
                _packages = new List<PackageOnNode>();
            if (Packages == null)
                Packages = new ReadOnlyCollection<PackageOnNode>(_packages);

            if (_otherSoftware == null)
                _otherSoftware = new List<string>();
            if (OtherSoftware == null)
                OtherSoftware = new ReadOnlyCollection<string>(_otherSoftware);

            if (StaticHardwareParams == null)
                StaticHardwareParams = new Dictionary<string, string>();

            if (TasksSubmissionLimit == 0) // todo : ok?
                TasksSubmissionLimit = 1;
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Init();
        }

        private ResourceNode()
        {
            // Constructor is private. ResourceNode should be loaded only from config
            Init();
        }

        public void OverrideNulls(ResourceNode defaultValues)
        {
            Init();

            if (defaultValues == null)
            {
                CheckConsistency();
                return;
            }

            //ResourceName { get;  set; }   //  private set
            //public string ProviderName;

            if (!SupportedArchitectures.Any() && defaultValues.SupportedArchitectures.Any())
                SupportedArchitectures = defaultValues.SupportedArchitectures; // it's ok, because field is read-only

            if (Services == null)
                Services = new NodeServices(defaultValues.Services);

            if (DataFolders == null)
                DataFolders = new NodeDataFolders(defaultValues.DataFolders);

            Credentials = new NodeCredentials
            (
                userName: (Credentials.Username ?? defaultValues.Credentials.Username),
                password: (Credentials.Password ?? defaultValues.Credentials.Password),
                certFile: (Credentials.CertFile ?? defaultValues.Credentials.CertFile)
            );

            if (CoresCount == 0 && defaultValues.CoresCount > 0)
                CoresCount = defaultValues.CoresCount;

            //if (CoresAvailable == 0 && defaultValues.CoresAvailable > 0) // todo : unnecessary?
                //CoresAvailable = defaultValues.CoresAvailable;

            if (TasksSubmissionLimit <= 1 && defaultValues.TasksSubmissionLimit > 1) // do not override if defaultValue == (0 or 1) or non-default value for node is set
                TasksSubmissionLimit = defaultValues.TasksSubmissionLimit;

            //if (SubmissionsAvailable == 0 && defaultValues.SubmissionsAvailable > 0) // todo : unnecessary?
                //SubmissionsAvailable = defaultValues.SubmissionsAvailable;

            if (defaultValues.StaticHardwareParams != null)
                foreach (var key in defaultValues.StaticHardwareParams.Keys)
                {
                    if (!StaticHardwareParams.ContainsKey(key))
                        StaticHardwareParams[key] = defaultValues.StaticHardwareParams[key];
                }

            if (!_packages.Any())
            {
                foreach (var pack in defaultValues._packages)
                    _packages.Add(new PackageOnNode(pack));
            }

            if (!_otherSoftware.Any())
            {
                foreach (var soft in defaultValues._otherSoftware)
                    _otherSoftware.Add(soft);
            }

            CheckConsistency();
        }

        public bool HasPackage(string packageName)
        {
            return Packages.Any(p => String.Equals(p.Name, packageName, StringComparison.InvariantCultureIgnoreCase));
        }

        public PackageOnNode PackageByName(string packageName)
        {
            return Packages.First(p => String.Equals(p.Name, packageName, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}


