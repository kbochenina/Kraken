using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TimeMeter.Models
{
    public class ExtAmdahlModel : Model
    {
        private const string ALPHA = "ALPHA";
        private const string GAMMA = "GAMMA";        
        private const string AMDAHLNETEX = "AMDAHLNETEX";

        protected override ICollection<string> GetConfigParameters()
        {
            return new List<string> {ALPHA, GAMMA};
        }

        protected override ICollection<string> GetRuntimeParameters()
        {
            return new List<string>();
        }

        protected override ICollection<string> GetDataParameters()
        {
            return new List<string>();
        }

        protected override ICollection<string> GetHardwareParameters()
        {
            return new List<string> {NodesCountExtractor.NODES};
        }

        protected override double ComputeTime(double? previousStageTime)
        {
            Debug.Assert(previousStageTime.HasValue);
            var alpha = GetNumericParameterValue(ALPHA);
            var gamma = GetNumericParameterValue(GAMMA);
            var nodes = GetNumericParameterValue(NodesCountExtractor.NODES);
            return previousStageTime.Value * (alpha * gamma * (nodes - 1) + gamma / nodes + (1 - gamma));
        }

        protected override List<EstimationResult.ParameterValue> Optimize()
        {
            var result = new List<EstimationResult.ParameterValue>();
            var dP = Math.Sqrt(1.0 / GetNumericParameterValue(ALPHA));
            var iP = (int)Math.Round(dP);
            if (iP == 0)
                iP = 1;
            if (iP < GetNumericParameterValue(NodesCountExtractor.NODES))
            {
                result.Add(new EstimationResult.ParameterValue() { Name = NodesCountExtractor.NODES, NewValue = iP.ToString(), InitialValue = GetParameterValue(NodesCountExtractor.NODES) });
                SetNumericParameterValue(NodesCountExtractor.NODES, iP);
            }
            return result;
        }

        [ReferenceName]
        public static string ReferenceName
        {
            get { return AMDAHLNETEX; }
        }
    }
}