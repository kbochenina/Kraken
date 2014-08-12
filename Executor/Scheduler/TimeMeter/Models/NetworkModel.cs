using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace TimeMeter.Models
{
    class NetworkModel : Model
    {
        private const string AMDAHLNET = "AMDAHLNET";
        private const string ALDEG = "ALDEG";
        private const string HWDEG = "HWDEG";

        protected override ICollection<string> GetConfigParameters()
        {
            return new List<string> {ALDEG};
        }

        protected override ICollection<string> GetRuntimeParameters()
        {
            return Enumerable.Empty<string>().ToList();
        }

        protected override ICollection<string> GetDataParameters()
        {
            return Enumerable.Empty<string>().ToList();
        }

        protected override ICollection<string> GetHardwareParameters()
        {
            return new List<string> {NodesCountExtractor.NODES, HWDEG};
        }

        protected override double ComputeTime(double? previousStageTime)
        {
            Debug.Assert(previousStageTime.HasValue);
            var alDeg = GetNumericParameterValue(ALDEG);
            var nodes = GetNumericParameterValue(NodesCountExtractor.NODES);
            var hwDeg = GetNumericParameterValue(HWDEG);
            var t = previousStageTime.Value;
            return t * (1.0 / nodes + alDeg * hwDeg);
        }

        protected override List<EstimationResult.ParameterValue> Optimize()
        {
            return Enumerable.Empty<EstimationResult.ParameterValue>().ToList();
        }

        [ReferenceName]
        public static string ReferenceName
        {
            get { return AMDAHLNET; }
        }
    }
}
