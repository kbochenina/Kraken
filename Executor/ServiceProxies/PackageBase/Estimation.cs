using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Easis.PackageBase;
using Easis.PackageBase.Client;
using Easis.PackageBase.Engine;
using Easis.PackageBase.Definition;
using Easis.PackageBase.Types;
using System.Runtime.Serialization;

namespace MITP
{
    [DataContract]
    public class HistoryEstimation
    {
        [DataMember] public double CalcDurationInSeconds { get; set; }

        public HistoryEstimation(double calcDurationInSeconds)
        {
            CalcDurationInSeconds = calcDurationInSeconds;
        }

        public HistoryEstimation(HistoryEstimation otherEstimation)
        {
            CalcDurationInSeconds = otherEstimation.CalcDurationInSeconds;
        }
    }

    [DataContract]
    public class Estimation
    {
        [DataMember] public ModelEstimation ByModel { get; private set; }
        [DataMember] public HistoryEstimation FromHistory { get; private set; }

        // todo : move ModelCoeffs inside ModelEstimation (modify PackageBase)
        /*[DataMember]*/ public Dictionary<string, double> ModelCoeffs { get; internal set; }

        public DateTime CalcStart { get; set; }

        private TimeSpan? _durationSurrogate = null;
        public TimeSpan CalcDuration
        {
            get
            {
                if (ByModel != null && ByModel.CalculationTime.IsSet)
                    return TimeSpan.FromSeconds(ByModel.CalculationTime.Value);

                if (FromHistory != null)
                    return TimeSpan.FromSeconds(FromHistory.CalcDurationInSeconds);

                if (_durationSurrogate != null)
                    return _durationSurrogate.Value;

                throw new Exception("Both estimations (by model & from history) aren't set");
            }

            set
            {
                _durationSurrogate = value;
            }
        }

        public TimeSpan TimeLeft(DateTime whenStarted)
        {
            TimeSpan passed = DateTime.Now - whenStarted;
            TimeSpan left = CalcDuration - passed;
            return (left.TotalMilliseconds < 0) ? TimeSpan.FromSeconds(0) : left;
        }

        /*
        public Estimation(ModelEstimation modelEstimation)
        {
            ByModel = modelEstimation;
        }

        public Estimation(HistoryEstimation historyEstimation)
        {
            FromHistory = historyEstimation;
        }
        */

        public Estimation(ModelEstimation modelEstimation = null, HistoryEstimation historyEstimation = null)
        {
            FromHistory = historyEstimation;

            ByModel = modelEstimation;
            ModelCoeffs = new Dictionary<string, double>();
        }

        public Estimation(Estimation otherEstimation)
        {
            _durationSurrogate = otherEstimation._durationSurrogate;

            if (otherEstimation.FromHistory != null)
                FromHistory = new HistoryEstimation(otherEstimation.FromHistory);

            if (otherEstimation.ModelCoeffs != null)
                ModelCoeffs = new Dictionary<string, double>(otherEstimation.ModelCoeffs);
            
            if (otherEstimation.ByModel != null)
            {
                ByModel = new ModelEstimation();

                ByModel.CalculationTime = new ValueWithDispersion<double>()
                {
                    Value = otherEstimation.ByModel.CalculationTime.Value,
                    Dispersion = otherEstimation.ByModel.CalculationTime.Dispersion
                };

                ByModel.TotalOutputFileSize = new ValueWithDispersion<ulong>()
                {
                    Value = otherEstimation.ByModel.TotalOutputFileSize.Value,
                    Dispersion = otherEstimation.ByModel.TotalOutputFileSize.Dispersion
                };

                if (otherEstimation.ByModel.ExtraValues != null)
                {
                    foreach (var pair in otherEstimation.ByModel.ExtraValues)
                        ByModel.ExtraValues[pair.Key] = pair.Value; // ExtraValues field is created in constructor of ModelEstimation
                }

                if (otherEstimation.ByModel.OutputFileSize != null)
                {
                    foreach (var pair in otherEstimation.ByModel.OutputFileSize)
                    {
                        ByModel.OutputFileSize[pair.Key] = new ValueWithDispersion<ulong>()
                        {
                            Value = pair.Value.Value,
                            Dispersion = pair.Value.Dispersion
                        };
                    }
                }
            }
        }
    }
}
