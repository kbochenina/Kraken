using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using ServiceProxies.ResourceBaseService;

namespace MITP
{
    [DataContract]
    public class ResourceTotals // todo : remove, others should use ResourceBase 
    {
        [DataMember] public string ResourceName { get; set; } 
        [DataMember] public string ResourceDescription { get; set; }
        [DataMember] public string Location { get; set; }
        [DataMember] public string ProviderName { get; set; }
        [DataMember] public int NodesTotal { get; set; }
        [DataMember] public IEnumerable<string> SupportedArchitectures { get; set; }

        public static ResourceTotals FromSchedule(TaskSchedule schedule)
        {
            if (schedule == null || String.IsNullOrEmpty(schedule.ResourceName))
                return null;
            ResourceTotals totals = null;

            try
            {
                var resource = ResourceBase.GetResourceByName(schedule.ResourceName);

                totals = new ResourceTotals()
                {
                    ProviderName = resource.Controller.Type, // resource.ProviderName,
                    ResourceName = resource.ResourceName,
                    ResourceDescription = resource.ResourceDescription,
                    Location = resource.Location,
                    NodesTotal = resource.Nodes.Length,
                    SupportedArchitectures = resource.SupportedArchitectures,
                };
            }
            catch (Exception e)
            {
                Log.Warn("Can't make ResourceTotals: " + e.ToString());
                totals = null;
            }

            return totals;
        }
    }

    [DataContract]
    public class NodeTotals // todo : remove, others should use ResourceBase 
    {
        [DataMember] public int CoresUsed { get; set; }
        [DataMember] public string NodeName { get; set; }
        [DataMember] public IEnumerable<string> SupportedArchitectures { get; set; }

        public string NodeAddress { get; set; }

        public static IEnumerable<NodeTotals> FromSchedule(TaskSchedule schedule)
        {
            if (schedule == null || String.IsNullOrEmpty(schedule.ResourceName) || 
                schedule.Nodes == null || !schedule.Nodes.Any())
                return null;
            IEnumerable<NodeTotals> totals = null;

            try
            {
                var resource = ResourceBase.GetResourceByName(schedule.ResourceName);

                var res = schedule.Nodes.Select(nodeConf => new NodeTotals()
                {
                    NodeName = nodeConf.NodeName,
                    NodeAddress = resource.Nodes.First(n => n.NodeName == nodeConf.NodeName).NodeAddress,
                    CoresUsed = (int)nodeConf.Cores,
                    SupportedArchitectures = resource.Nodes.First(n => n.NodeName == nodeConf.NodeName).SupportedArchitectures,
                });

                totals = res.ToArray();
            }
            catch (Exception e)
            {
                Log.Warn("Can't make NodeTotals: " + e.ToString());
                totals = null;
            }

            return totals;
        }
    }
}

