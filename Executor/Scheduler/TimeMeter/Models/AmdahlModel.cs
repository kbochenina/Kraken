using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TimeMeter.Models
{
    class AmdahlModel : Model
    {
        private const string ALPHA = "ALPHA";
        private const string GAMMA = "GAMMA";
        private const string P = "P";
        private const string AMDAHLEX = "AMDAHLEX";

        protected override ICollection<string> GetConfigParameters()
        {
            return new List<string> {ALPHA, GAMMA};
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
            return new List<string> {P};
        }

        protected override double ComputeTime(double? previousStageTime)
        {
            Debug.Assert(previousStageTime.HasValue);
            var alpha = GetNumericParameterValue(ALPHA);
            var gamma = GetNumericParameterValue(GAMMA);
            var p = GetNumericParameterValue(P);
            var t = previousStageTime.Value;
            var result = t * (alpha * gamma * (p - 1) + gamma / p + (1 - gamma));
            return result;
        }

        protected override List<EstimationResult.ParameterValue> Optimize()
        {
            var result = new List<EstimationResult.ParameterValue>();
            var dP = Math.Sqrt(1.0 / GetNumericParameterValue(ALPHA));
            var iP = (int)Math.Round(dP);
            if (iP == 0)
                iP = 1;
            if (iP < GetNumericParameterValue(P))
            {                
                result.Add(new EstimationResult.ParameterValue() { Name = P, NewValue = iP.ToString(), InitialValue = GetParameterValue(P) });
                SetNumericParameterValue(P, iP);
            }
            return result;
        }

        [ReferenceName]
        public static String ReferenceName
        {
            get { return AMDAHLEX; }
        }
    }
}
