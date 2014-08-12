using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TimeMeter;

namespace TimeMeter.Models
{
    class TestPModel : Model
    {

        public const string TESTP = "TESTP_ARITHM";

        protected override ICollection<string> GetConfigParameters()
        {
            return Enumerable.Empty<string>().ToList();
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
            return new List<string>() { AverageResourcePerformance.PERF };
        }

        protected override double ComputeTime(double? previousStageTime)
        {
            return 1 / GetNumericParameterValue(AverageResourcePerformance.PERF);
        }

        protected override List<EstimationResult.ParameterValue> Optimize()
        {
            return Enumerable.Empty<EstimationResult.ParameterValue>().ToList();
        }

        [ReferenceName]
        public static string Name
        {
            get
            {
                return TESTP;
            }
        }
    }
}
