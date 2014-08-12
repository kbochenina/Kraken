using System;
using System.Collections.Generic;
using System.Linq;
using TimeMeter.Integrator;

namespace TimeMeter
{
    public interface IApplication
    {
        string AppId { get; }
        void AddModel(string modelId);        
        bool HasModel(string modelId);
        bool HasParameter(string modelId, string parameterId);        
        void SetParametersValues(string modelId, IDictionary<string, string> values);
        string SetParameterValue(string modelId, string parameterId, string value);
        string GetParameterValue(string modelId, string parameterId);
        double GetNumericParameterValue(string modelId, string parameterName);
        double SetNumericParameterValue(string modelId, string parameterName, double newValue);
        bool GetBooleanParameterValue(string modelId, string parameterName);
        bool SetBooleanParameterValue(string modelId, string parameterName, bool newValue);

        EstimationResult Estimate(IDictionary<string, string> parameters, Common.Resource resource, Common.LaunchDestination destination, bool optimize);
    }

    public class Application : IApplication
    {
        public string AppId { get; private set; }

        private readonly IDictionary<string, IModel> _models = new Dictionary<string, IModel>();

        internal Application(StoredApplicationDescription applicationDescription, string resourceName)
            : this(applicationDescription.AppId)
        {
            foreach (var model in applicationDescription.Models)
            {
                AddModel(model.ModelId);
                var machineParams = model.MachinesSettings.SingleOrDefault(m => m.MachineId == resourceName);
                var resultParams = Enumerable.Empty<KeyValuePair<string, string>>().ToDictionary(p => p.Key, p => p.Value);
                if (machineParams != null)
                {
                    resultParams = machineParams.Parameters.ToDictionary(p => p.ParameterId, p => p.Value);
                }
                resultParams = resultParams.Union(model.Parameters.Where(p => !resultParams.ContainsKey(p.ParameterId)).ToDictionary(p => p.ParameterId, p => p.Value)).ToDictionary(p => p.Key, p => p.Value);
                GetModel(model.ModelId).SetParametersValues(resultParams);                
            }
        }

        public Application(string appId)
        {
            AppId = appId;
        }

        public void AddModel(string modelId)
        {
            if (HasModel(modelId)) return;
            var model = TaskTimeMeter.CreateModel(modelId);
            _models[modelId] = model;
        }

        public string SetParameterValue(string modelId, string parameterId, string value)
        {
            return GetModel(modelId).SetParameterValue(parameterId, value);
        }

        public bool HasModel(string modelId)
        {
            return _models.ContainsKey(modelId);
        }

        public bool HasParameter(string modelId, string parameterId)
        {
            return HasModel(modelId) && GetModel(modelId).IsParameterAllowed(parameterId);
        }

        public string GetParameterValue(string modelId, string parameterId)
        {
            return GetModel(modelId).GetParameterValue(parameterId);
        }

        public EstimationResult Estimate(IDictionary<string, string> parameters, Common.Resource resource, Common.LaunchDestination destination, bool optimize)
        {
            var models = new List<IModel>();
            models.AddRange(_models.Values);
            models.Sort(new ModelOrderer());
            EstimationResult result = null;
            foreach (var model in models)
            {
                var hwParameters = model.GetParameters(ParameterSourceType.Hardware);
                foreach (var hwParameter in hwParameters)
                {
                    model.SetParameterValue(hwParameter.Name, ClusterParameterReader.GetValue(hwParameter.Name, resource, destination));
                }
                result = model.Estimate(parameters, result, optimize);
            }
            if (!result.Parameters.Exists(p => p.Name == NodesCountExtractor.NODES))
            {
                result.Parameters.Add(new EstimationResult.ParameterValue()
                {
                    Name = NodesCountExtractor.NODES,
                    InitialValue = (1).ToString(),
                    NewValue = (1).ToString()
                });
            }
            if (!result.Parameters.Exists(p => p.Name == ProcessorCountPerNode.P))
            {
                result.Parameters.Add(new EstimationResult.ParameterValue()
                {
                    Name = ProcessorCountPerNode.P,
                    InitialValue = (1).ToString(),
                    NewValue = (1).ToString()
                });
            }
            return result;
        }

        public IEnumerable<ParameterDescription> GetParameters()
        {
            var parameters = new List<ParameterDescription>();
            foreach (var model in _models)
            {
                parameters.AddRange(model.Value.GetParameters());
            }
            return parameters;
        }

        public IEnumerable<ParameterDescription> GetParameters(ParameterSourceType sourceType)
        {
            var parameters = new List<ParameterDescription>();
            foreach (var model in _models)
            {
                parameters.AddRange(model.Value.GetParameters(sourceType));
            }
            return parameters;
        }

        protected IModel GetModel(string modelId)
        {
            if (!_models.ContainsKey(modelId))
            {
                throw new ModelNotFoundException(modelId);
            }
            return _models[modelId];
        }

        internal StoredApplicationDescription CreateStoredDescription()
        {
            return new StoredApplicationDescription
            {
                AppId = AppId,
                Models =
                    _models.Select(
                        m =>
                        new StoredModelDescription
                        {
                            ModelId = m.Key,
                            Parameters =
                                m.Value.GetParameters().Where(p =>
                                    p.SourceType == ParameterSourceType.Config).Select(
                                        p => new StoredModelParameterDescription { ParameterId = p.Name, Value = m.Value.GetParameterValue(p.Name) }).ToArray() 
                        }).ToArray()
            };
        }


        public void SetParametersValues(string modelId, IDictionary<string, string> values)
        {
            GetModel(modelId).SetParametersValues(values);
        }

        public double GetNumericParameterValue(string modelId, string parameterName)
        {
            return GetModel(modelId).GetNumericParameterValue(parameterName);
        }

        public double SetNumericParameterValue(string modelId, string parameterName, double newValue)
        {
            return GetModel(modelId).SetNumericParameterValue(parameterName, newValue);
        }

        public bool GetBooleanParameterValue(string modelId, string parameterName)
        {
            return GetModel(modelId).GetBooleanParameterValue(parameterName);
        }

        public bool SetBooleanParameterValue(string modelId, string parameterName, bool newValue)
        {
            return GetModel(modelId).SetBooleanParameterValue(parameterName, newValue);
        }
    }

    [Serializable]
    public class StoredApplicationDescription
    {
        public string AppId;

        public StoredModelDescription[] Models;
    }

    [Serializable]
    public class StoredModelDescription
    {
        public string ModelId;
        public StoredModelParameterDescription[] Parameters;
        public MachineSpecificModelSettings[] MachinesSettings;
    }

    [Serializable]
    public class MachineSpecificModelSettings
    {
        public string MachineId;
        public StoredModelParameterDescription[] Parameters;
    }

    [Serializable]
    public class StoredModelParameterDescription
    {
        public string ParameterId;
        public string Value;
    }
}