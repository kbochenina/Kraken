using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TimeMeter.Models
{
    public class BSMModel : Model
    {

        private const string BSM = "BSM";        
        
        private const string ASM = "useAssimilation";      
        private const string BSH = "useBSH";
        private const string SWAN = "useSWAN";
        private const string FILESIZE = "inHirlamSize";

        private const int DEFAULT_TIMESPAN_HRS = 48;
        private const int DEFAULT_CORE_FREQUENCY = 2666;

        private const int DEFAULT_COMP_TIME = 525;
        private const int DEFAULT_COMP_TIME_ASM = 532;
        private const int DEFAULT_COMP_TIME_BSH = 529;
        private const int DEFAULT_COMP_TIME_SWAN = 863;

        private const double COMP_FACTOR = 1.02;

        private const int AVG_SINGLE_SIZE = 466084;

        protected override ICollection<string> GetConfigParameters()
        {
            return Enumerable.Empty<string>().ToList();
        }

        protected override ICollection<string> GetDataParameters()
        {
            return new List<string>() { ASM, SWAN, BSH, FILESIZE };
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
            var asm = GetBooleanParameterValue(ASM);
            var bsh = GetBooleanParameterValue(BSH);
            var swan = GetBooleanParameterValue(SWAN);
            var length = GetNumericParameterValue(FILESIZE);
            var hoursNum = (int) Math.Round((length * COMP_FACTOR) / AVG_SINGLE_SIZE / 8) * 8;
            var perf = GetNumericParameterValue(AverageResourcePerformance.PERF);
            var time = DEFAULT_COMP_TIME;
            if (asm)
            {
                time += DEFAULT_COMP_TIME_ASM - DEFAULT_COMP_TIME;
            }
            if (bsh)
            {
                time += DEFAULT_COMP_TIME_BSH - DEFAULT_COMP_TIME;
            }
            if (swan)
            {
                time += DEFAULT_COMP_TIME_SWAN - DEFAULT_COMP_TIME;
            }
            return time * ((double) DEFAULT_TIMESPAN_HRS / hoursNum) * (DEFAULT_CORE_FREQUENCY / perf);
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
                return BSM;
            }
        }
    }
}
