using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TimeMeter.Models
{
    class ShipXModel : Model
    {
        private const string SIM_COUNT = "SIM_COUNT";
        private const string SHIPX = "SHIPX";
        private const string DATA_LENGTH = "DATA_LENGTH";

        protected override ICollection<string> GetConfigParameters()
        {
            return new List<string>() {SIM_COUNT};
        }

        protected override ICollection<string> GetDataParameters()
        {
            return new List<string>() { DATA_LENGTH };
        }

        protected override ICollection<string> GetRuntimeParameters()
        {
            return Enumerable.Empty<string>().ToList();
        }

        protected override ICollection<string> GetHardwareParameters()
        {
            return Enumerable.Empty<string>().ToList();
        }

        protected override double ComputeTime(double? previousStageTime)
        {
            return Math.Min(Math.Max(20, GetNumericParameterValue(SIM_COUNT) * GetNumericParameterValue(DATA_LENGTH)), 1200);
        }

        [ReferenceName]
        public static string Name
        {
            get
            {
                return SHIPX;
            }
        }

        protected override List<EstimationResult.ParameterValue> Optimize()
        {
            return Enumerable.Empty<EstimationResult.ParameterValue>().ToList();
        }
    }
}
