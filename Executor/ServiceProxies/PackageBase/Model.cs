using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Easis.PackageBase;
using Easis.PackageBase.Client;
using Easis.PackageBase.Engine;
using Easis.PackageBase.Definition;
using Easis.PackageBase.Types;
using ServiceProxies.ResourceBaseService;

namespace MITP
{
    // todo : remove this hack. History have to be read from database
    public class HistorySample
    {
        public string Package { get; private set; }
        public string ResourceName { get; private set; }
        public NodeConfig[] NodesConfig { get; private set; }

        public Dictionary<string, string> PackParams { get; private set; }
        public Dictionary<string, double> ModelCoefs { get; private set; }

        public TimeSpan CalcTime { get; private set; }
        
        public PackageEngine EstimatorEngine { get; private set; }

        public HistorySample(
            string package, string resourceName, NodeConfig[] nodesConfig, 
            Dictionary<string, string> packParams, Dictionary<string, double> modelCoefs,
            TimeSpan calcTime, PackageEngine estimatorEngine)
        {
            Package = package;
            ResourceName = resourceName;
            NodesConfig = nodesConfig;

            PackParams = packParams;
            ModelCoefs = modelCoefs;

            CalcTime = calcTime;

            EstimatorEngine = estimatorEngine;
        }
    }

    public partial class PackageBaseProxy
    {
        private const string FIXED_COEF_PREFIX = "model.";
        private const string ADJUSTABLE_COEF_PREFIX = "model*.";

        #region BSM Estimator

        private class BSMSimpleEstimator
        {
            private const double BsmA = 10.0;
            private const double BsmB = 24.5;

            /// <summary>
            /// Оценка параметра perf модели производительности BSM
            /// T(N) = (a * N + b) * perf
            /// </summary>
            /// <param name="fTime">Массив времени прогноза (N)</param>
            /// <param name="cTime">Массив времени счета</param>
            /// <returns>Оценка параметра perf</returns>
            public static double BsmEstimatePerf(double[] fTime, double[] cTime)
            {
                var nom = 0.0;
                var denom = 0.0;
                for (int i = 0; i < fTime.Length; i++)
                {
                    nom += (BsmA * fTime[i] + BsmB) * cTime[i];
                    denom += (BsmA * fTime[i] + BsmB) * (BsmA * fTime[i] + BsmB);
                }
                return nom / denom;
            }

            /// <summary>
            /// Оценка СКО относительно погрешности
            /// T(N) = (a * N + b) * perf
            /// Ширина интервала 2СКО: (a * N * perf) * z * 2
            /// </summary>
            /// <param name="perf">Параметра модели времени BSM</param>
            /// <param name="fTime">Массив времени прогноза (N)</param>
            /// <param name="cTime">Массив времени счета</param>
            /// <returns>Величина относительной погрешности z</returns>
            public static double BsmRelativeStDev(double perf, double[] fTime, double[] cTime)
            {
                var relErr = new double[fTime.Length];
                for (int i = 0; i < fTime.Length; i++)
                    relErr[i] = ((BsmA * fTime[i] + BsmB) * perf - cTime[i]) / (BsmA * fTime[i] * perf);
                return StDev(relErr);
            }

            /// <summary>
            /// Математическое ожидание
            /// </summary>
            /// <param name="x">Массив чисел</param>
            /// <returns>Математическое ожидание</returns>
            private static double Mean(double[] x)
            {
                var sum = 0.0;
                for (int i = 0; i < x.Length; i++)
                    sum += x[i];
                return sum / x.Length;
            }

            /// <summary>
            /// СКО
            /// </summary>
            /// <param name="x">Массив чисел</param>
            /// <returns>СКО</returns>
            private static double StDev(double[] x)
            {
                var sum = 0.0;
                var m = Mean(x);
                for (int i = 0; i < x.Length; i++)
                    sum += (m - x[i]) * (m - x[i]);
                return Math.Sqrt(sum / x.Length);
            }
        }

        #endregion

        #region History
        // todo : remove this hack. History have to be read from database

        private static object _historyLock = new object();
        private static List<HistorySample> _history = new List<HistorySample>();

        public static void AddHistorySample(HistorySample historySample)
        {
            lock (_historyLock)
            {
                _history.Add(historySample);
            }
        }

        public static HistorySample[] GetHistorySamples(string package, string resourceName, string nodeName)
        {
            lock (_historyLock)
            {
                return _history.Where(hs =>
                    hs.Package.ToLowerInvariant() == package &&
                    hs.ResourceName == resourceName &&
                    hs.NodesConfig.Single().NodeName == nodeName)
                .ToArray();
            }
        }
        #endregion

        private static Dictionary<string, double> AutoAdjustCoefsByHistory(PackageEngine engine, ResourceNode node, Dictionary<string, object> fixedCoefs, Dictionary<string, double> adjustableCoefs)
        {
            lock (_historyLock)
            {
                //Log.Info("Adjusting coefs");
                string packageName = engine.CompiledMode.ModeQName.PackageName.ToLowerInvariant();
                var history = GetHistorySamples(packageName, node.ResourceName, node.NodeName);

                if (history.Length < 1)
                {
                    //Log.Debug("Not enough history samples to adjust coefs");
                    return null;
                }

                if (packageName == "bsm")
                {
                    double[] fTime = history.Select(hs => double.Parse(hs.PackParams["ForecastSize"])).ToArray();
                    double[] cTime = history.Select(hs => hs.CalcTime.TotalSeconds).ToArray();

                    double perf = BSMSimpleEstimator.BsmEstimatePerf(fTime, cTime);
                    double dev = BSMSimpleEstimator.BsmRelativeStDev(perf, fTime, cTime);

                    return new Dictionary<string, double>()
                    {
                        { "Perf", perf },
                        { "D", dev }
                    };
                }
                else
                {
                    Log.Warn("Unsupported package to adjust coefs: " + packageName);
                    return null;
                }
            }
        }

        private static Dictionary<string, object> GetModelCoefs(PackageEngine engine, ResourceNode node)
        {
            var fixedCoefs = new Dictionary<string, object>(engine.CompiledMode.Models.DefaultCoeffs);
            var adjustableCoefs  = new Dictionary<string, double>();

            string packageName = engine.CompiledMode.ModeQName.PackageName; // engineState._taskDescription.Package
            var packParams = node.PackageByName(packageName).Params;
            foreach (string paramKey in packParams.Keys)
            {
                string paramValue = packParams[paramKey];
                double paramValueAsDouble;
                bool isDouble = double.TryParse(paramValue,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture.NumberFormat,
                    out paramValueAsDouble);

                if (paramKey.ToLowerInvariant().StartsWith(FIXED_COEF_PREFIX))
                {
                    string coefName = paramKey.Remove(0, FIXED_COEF_PREFIX.Length);
                    if (isDouble)
                        fixedCoefs[coefName] = paramValueAsDouble;
                    else
                        fixedCoefs[coefName] = paramValue;
                }
                else
                if (paramKey.ToLowerInvariant().StartsWith(ADJUSTABLE_COEF_PREFIX))
                {
                    string coefName = paramKey.Remove(0, ADJUSTABLE_COEF_PREFIX.Length);
                    if (isDouble)
                        adjustableCoefs[coefName] = paramValueAsDouble;
                    else
                    {
                        fixedCoefs[coefName] = paramValue;
                        Log.Warn(String.Format(
                            "Cannot adjust param '{0}' for pack '{1}' on resource node '{2}.{3}' because it's value is not number",
                            paramKey, packageName,
                            node.ResourceName, node.NodeName
                        ));
                    }
                }
            }

            var adjustedCoefs = AutoAdjustCoefsByHistory(engine, node, fixedCoefs, adjustableCoefs) ?? new Dictionary<string, double>();
            if (adjustableCoefs.Any())
                Log.Info("Model coefs were adjusted");

            var newCoefNames = adjustedCoefs.Keys.Except(adjustableCoefs.Keys);
            if (newCoefNames.Any())
                Log.Warn("Autoadjust created new coefs (ignoring them): " + String.Join(", ", newCoefNames));

            var modelCoefs = new Dictionary<string, object>(fixedCoefs);
            foreach (var coefName in adjustableCoefs.Keys)
            {
                if (adjustedCoefs.ContainsKey(coefName))
                    modelCoefs[coefName] = adjustedCoefs[coefName];
                else
                {
                    modelCoefs[coefName] = adjustableCoefs[coefName];
                    Log.Warn("Coef " + coefName + " was not returned as adjusted. Using non-adjusted value.");
                }
            }
            return modelCoefs;
        }

        public static Dictionary<NodeConfig, Estimation> GetEstimationsByModel(PackageEngineState engineState, IEnumerable<Resource> resources, IEnumerable<ResourceNode> permittedNodes)
        {
            var estims = new Dictionary<NodeConfig, Estimation>();
            Log.Debug("Estimating by model...");

            var engine = new PackageEngine(engineState.CompiledDef, engineState.EngineContext);
            if (!engine.CanEstimate())
            {
                Log.Debug("Can't estimate by model");
                return estims;
            }

            try
            {
                var allRes = resources.Select(r => new Common.Resource()
                {
                    Name = r.ResourceName,
                    Nodes = r.Nodes.Select(n => new Common.Node()
                    {
                        ResourceName = n.ResourceName,
                        DNSName = n.NodeName,
                        Parameters = new Dictionary<string, string>(n.StaticHardwareParams),
                        CoresAvailable = n.CoresAvailable,
                        CoresTotal = (int)n.CoresCount,
                    }).ToArray(),
                    Parameters = new Dictionary<string, string>(r.HardwareParams),
                });

                Log.Debug("Permitted nodes: " + String.Join(", ", permittedNodes.Select(n => n.ResourceName + "." + n.NodeName)));

                foreach (var node in permittedNodes)
                {
                    try
                    {
                        var res = allRes.Single(r => r.Name == node.ResourceName);
                        var dest = new Common.LaunchDestination()
                        {
                            ResourceName = node.ResourceName,
                            NodeNames = new[] { node.NodeName },
                        };

                        var modelExecParams = new Dictionary<string, object>();
                        foreach (string paramId in TimeMeter.ClusterParameterReader.GetAvailableParameterIds())   // from scheduler. Why?
                        {
                            try { modelExecParams[paramId] = TimeMeter.ClusterParameterReader.GetValue(paramId, res, dest); }
                            catch (Exception) { /* it's ok not to extract all possible params */ }
                        }

                        var modelCoefs = GetModelCoefs(engine, node);
                        var modelEstimation = engine.Estimate(modelExecParams, modelCoefs);
                        if (engine.Ctx.Result.Messages.Any())
                        {
                            Log.Warn("Messages on estimation: " + String.Join("\n", engine.Ctx.Result.Messages.Select(mess => mess.Message)));
                        }

                        //double estimationInSeconds = engine.Estimate(modelExecParams, modelCoefs);
                        if (modelEstimation != null /* == no errors */ && modelEstimation.CalculationTime.IsSet &&
                            !Double.IsInfinity(modelEstimation.CalculationTime.Value) && !Double.IsNaN(modelEstimation.CalculationTime.Value))
                        {
                            var modelCoeffsToRemember = new Dictionary<string, double>();
                            foreach (var pair in modelCoefs)
                            {
                                if (pair.Value is double)
                                    modelCoeffsToRemember[pair.Key] = (double)pair.Value;
                            }

                            estims.Add
                            (
                                new NodeConfig() 
                                {
                                    ResourceName = node.ResourceName,
                                    NodeName = node.NodeName,
                                    Cores = (uint) 1, // was node.CoresAvailable
                                        // todo : node.pack.MinCores or node.pack.MaxCores
                                        // todo : estimation from model -> cores
                                },

                                new Estimation(modelEstimation)
                                {
                                    ModelCoeffs = modelCoeffsToRemember
                                }
                            );

                            /*
                            Log.Debug(String.Format("Estim by model on {0}.{1}: {2}",
                                node.ResourceName, node.NodeName,
                                (modelEstimation.CalculationTime.IsSet ? modelEstimation.CalculationTime.Value.ToString() : "not set")
                            ));
                            */
                        }
                        else
                        {
                            // todo : else Log.Trace estimation by model is NaN or Infinity
                            Log.Warn("Model estimation failed for task " + engineState._taskDescription.TaskId.ToString());
                        }
                    }
                    catch (Exception estimEx)
                    {
                        Log.Warn(String.Format(
                            "Exception while estimating task {1} on node '{2}' by models in PackageBase : {0}",
                            estimEx, engineState._taskDescription.TaskId, node.NodeName
                        ));
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(String.Format(
                    "Exception while getting estimations from models in PackageBase for task {2}: {0}\n{1}",
                    e.Message, e.StackTrace,
                    engineState._taskDescription.TaskId
                ));
            }

            return estims;
        }    
    }
}