using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TimeMeter.Models
{
    public class PowerModel : Model
    {
        private const string A = "A";
        private const string B = "B";
        private const string FUNCTIONS_COUNT = "FUNCTIONS_COUNT";
        private const string PERF = "PERF";
        private const string POWER_MODEL = "POWER_MODEL";

        public PowerModel()
        {
            Order = 1;
        }

        protected override ICollection<string> GetConfigParameters()
        {
            return new List<string> {A, B};
        }

        protected override ICollection<string> GetDataParameters()
        {
            return new List<string> {FUNCTIONS_COUNT};
        }

        protected override ICollection<string> GetRuntimeParameters()
        {
            return Enumerable.Empty<string>().ToList();
        }

        protected override ICollection<string> GetHardwareParameters()
        {
            return new List<string> {PERF};
        }

        protected override double ComputeTime(double? previousStageTime)
        {
            var a = GetNumericParameterValue(A);
            var b = GetNumericParameterValue(B);
            var functionsCount = GetNumericParameterValue(FUNCTIONS_COUNT);
            var perf = GetNumericParameterValue(PERF);
            return 1e-2 * a * functionsCount * Math.Pow(functionsCount, b) / perf;
        }

        protected override List<EstimationResult.ParameterValue> Optimize()
        {
            return Enumerable.Empty<EstimationResult.ParameterValue>().ToList();
        }

        [ReferenceName]
        public static string ReferenceName
        {
            get { return POWER_MODEL; }
        }
    }
}
