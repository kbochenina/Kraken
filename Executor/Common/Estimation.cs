using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;

// REV: плохо, что столько типов в одном файле, сложно осуществлять навигацию и визуально оценивать состав библиотеки
namespace MITP
{
    /*
    [DataContract]
    public class Estimation : ResourceConfig
    {
        [DataMember] public ulong TaskId { get; private set; }

        [DataMember] public DateTime StartTime { get; set; }
        //[DataMember] public DateTime CompletionTime { get; set; }
        [DataMember] public TimeSpan ExecutionTime { get; set; }

        public Estimation(ulong taskId, string providerName, string resourceName, int[] cores = null)
            : base(providerName, resourceName, cores)
        {
            TaskId = taskId;
        }
    }


    [DataContract]
    public class ResourceConfig // todo: remove ResourceConfig (combine it with Estimation)
    {
        [DataMember] public string ProviderName { get; private set; }
        [DataMember] public string ResourceName { get; private set; } // todo : m.b. refactor ResourceConfig.ResourceName -> ResourceConfig.Name
        [DataMember] public int[] Cores { get; set; }

        public ResourceConfig(string providerName, string resourceName, int[] cores = null)
        {
            ProviderName = providerName;
            ResourceName = resourceName;
            Cores = (cores == null)? null: (int[]) cores.Clone();
        }

        private ResourceConfig()
        {
        }
    }
    */
}
    
