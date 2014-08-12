using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TimeMeter.Models
{
    class LPEModel : Model
    {
        private const string TIME = "TIME";
        private const string LPE = "LPE";

        protected override ICollection<string> GetConfigParameters()
        {
            return new List<string>() {TIME};
        }

        protected override ICollection<string> GetDataParameters()
        {
            return Enumerable.Empty<string>().ToList();
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
            return GetNumericParameterValue(TIME);
        }

        [ReferenceName]
        public static string Name
        {
            get
            {
                return LPE;
            }
        }

        protected override List<EstimationResult.ParameterValue> Optimize()
        {
            return Enumerable.Empty<EstimationResult.ParameterValue>().ToList();
        }
    }
}
