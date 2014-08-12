using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;

namespace MITP
{
    [DataContract]
    public class PackageOnNode
    {
        [DataMember(Order=0, IsRequired=true)] 
        public string Name { get; private set; }

        [DataMember(Order=1, IsRequired=true)]
        public string Version { get; private set; }

        [DataMember(Order=2, IsRequired=true)]
        public string AppPath { get; private set; }

        [DataMember(Order = 3, IsRequired = false)]
        public string LocalDir { get; private set; }

        [DataMember]
        public IDictionary<string, string> EnvVars { get; private set; }

        [DataMember]
        public IDictionary<string, string> Params { get; private set; }

        [DataMember]
        public IEnumerable<string> CopyOnStartup { get; private set; }

        [DataMember]
        public IEnumerable<string> Cleanup { get; private set; }

        [DataMember]
        public IEnumerable<string> CleanupIgnore { get; private set; }

        private void Init()
        {
            if (CopyOnStartup == null)
                CopyOnStartup = new string[0];

            if (Cleanup == null)
                Cleanup = new string[0];

            if (CleanupIgnore == null)
                CleanupIgnore = new string[0];

            if (EnvVars == null)
                EnvVars = new Dictionary<string, string>();

            if (Params == null)
                Params = new Dictionary<string, string>();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Init();
        }

        private PackageOnNode()
        {
            // Constructor is private. Resources with it's nodes and packages can only be loaded from base
            Init();
        }

        public PackageOnNode(PackageOnNode other)
        {
            Init();

            if (other == null)
                return;

            Name = other.Name;
            Version = other.Version;
            AppPath = other.AppPath;
            LocalDir = other.LocalDir;

            // it's ok, because those enumerable fields are read-only:
            if (other.CopyOnStartup != null) CopyOnStartup = other.CopyOnStartup.ToArray();
            if (other.CleanupIgnore != null) CleanupIgnore = other.CleanupIgnore.ToArray();
            if (other.Cleanup != null) Cleanup = other.Cleanup.ToArray();

            if (other.EnvVars != null)
                EnvVars = new Dictionary<string, string>(other.EnvVars);

            if (other.Params != null)
                Params = new Dictionary<string, string>(other.Params);
        }
    }
}