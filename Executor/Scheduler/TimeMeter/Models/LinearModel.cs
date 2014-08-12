using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TimeMeter.Models
{
    class LinearModel : Model
    {
        private const string A0 = "A0";
        private const string A1 = "A1";
        private const string DATA_LENGTH = "DATA_LENGTH";
        private const string PERF = "PERF";
        private const string LINEAR = "LINEAR";

        public LinearModel()
        {
            Order = 1;
        }

        protected override ICollection<string> GetConfigParameters()
        {
            return new List<string> {A0, A1};
        }

        protected override ICollection<string> GetRuntimeParameters()
        {
            return new List<string>();
        }

        protected override ICollection<string> GetDataParameters()
        {
            return new List<string> {DATA_LENGTH};
        }

        protected override ICollection<string> GetHardwareParameters()
        {
            return new List<string> {PERF};
        }

        protected override double ComputeTime(double? previousStageTime)
        {
            var a1 = GetNumericParameterValue(A1);
            var a0 = GetNumericParameterValue(A0);
            var dataLength = GetNumericParameterValue(DATA_LENGTH);
            var perf = GetNumericParameterValue(PERF);
            return a1 * dataLength / perf + a0;
        }

        protected override List<EstimationResult.ParameterValue> Optimize()
        {
            return Enumerable.Empty<EstimationResult.ParameterValue>().ToList();
        }

        [ReferenceName]
        public static string ReferenceName
        {
            get { return LINEAR; }
        }
    }
}
