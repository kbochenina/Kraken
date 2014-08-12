using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Globalization;

namespace TimeMeter.Models
{
    public abstract class Model : IModel
    {
        protected class ReferenceNameAttribute : Attribute
        {
        }

        protected class ParameterAttribute : Attribute
        {
            public ParameterAttribute(ParameterSourceType sourceType)
            {
                Debug.Assert(sourceType == ParameterSourceType.Code || sourceType == ParameterSourceType.Computed);
                SourceType = sourceType;
            }

            public ParameterSourceType SourceType { get; private set; }
        }

        protected int Order;
        private readonly string _referenceName;
        private readonly ReadOnlyCollection<ParameterDescription> _parameters;
        private readonly IDictionary<string, string> _values = new Dictionary<string, string>();
        private readonly IList<PropertyInfo> _props;
        private static readonly ReadOnlyCollection<Type> AllowedTypes = new ReadOnlyCollection<Type>(new List<Type> { typeof(Boolean), typeof(String), typeof(Double) } );


        protected Model()
        {
            Order = 10;
            _props = GetType().GetProperties().ToList();
            var nameProperty =
                _props.FirstOrDefault(
                    p => p.GetCustomAttributes(typeof (ReferenceNameAttribute), false).Length > 0 && p.GetGetMethod().IsStatic && p.PropertyType == typeof(string));
            if (nameProperty != null)
            {
                try
                {
                    _referenceName = (string) nameProperty.GetValue(this, null);
                }
                catch(Exception)
                {
                    _referenceName = GetType().Name;
                }
            }
            else
            {
                _referenceName = GetType().Name;
            }
            var parameters = GetRuntimeParameters().Select(name => new ParameterDescription(name, ParameterSourceType.Runtime)).ToList();
            parameters.AddRange(GetHardwareParameters().Select(name => new ParameterDescription(name, ParameterSourceType.Hardware)).ToList());
            parameters.AddRange(GetConfigParameters().Select(name => new ParameterDescription(name, ParameterSourceType.Config)).ToList());
            parameters.AddRange(GetDataParameters().Select(name => new ParameterDescription(name, ParameterSourceType.Data)).ToList());

            var props = _props.Where(
                    p => p.GetCustomAttributes(typeof(ParameterAttribute), false).Length > 0 && AllowedTypes.Contains(p.PropertyType));
            foreach (var property in props)
            {
                var attrs = property.GetCustomAttributes(typeof (ParameterAttribute), false);
                Debug.Assert(attrs.Length > 0);
                var attribute = (ParameterAttribute) attrs[0];
                Debug.Assert((attribute.SourceType == ParameterSourceType.Code || attribute.SourceType == ParameterSourceType.Computed) &&
                    ((attribute.SourceType == ParameterSourceType.Code) == property.GetGetMethod().IsStatic));
                parameters.Add(new ParameterDescription(property.Name, attribute.SourceType));
            }
            _parameters = new ReadOnlyCollection<ParameterDescription>(parameters);
        }

        protected abstract ICollection<string> GetConfigParameters();
        protected abstract ICollection<string> GetDataParameters();
        protected abstract ICollection<string> GetRuntimeParameters();
        protected abstract ICollection<string> GetHardwareParameters();

        public bool IsParameterWritable(string parameterName)
        {
            var parameter = GetParameter(parameterName);
            return parameter != null && IsParameterTypeAllowed(parameter.SourceType);
        }

        public string SetParameterValue(string parameterName, string newValue)
        {
            Debug.Assert(IsParameterWritable(parameterName));
            var prevValue = _values.ContainsKey(parameterName) ? _values[parameterName] : null;
            _values[parameterName] = newValue;
            return prevValue;
        }

        public bool SetBooleanParameterValue(string parameterName, bool newValue)
        {
            var prevValue = SetParameterValue(parameterName, newValue.ToString());
            bool result;
            return Boolean.TryParse(prevValue, out result) ? result : false;
        }

        public double SetNumericParameterValue(string parameterName, double newValue)
        {
            var prevValue = SetParameterValue(parameterName, newValue.ToString());
            double result;
            return Double.TryParse(prevValue, NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out result) ? result : 0;
        }

        public void SetParametersValues(IDictionary<string, string> values)
        {
            Debug.Assert(values.Keys.All(IsParameterWritable));
            foreach (var pair in values)
            {
                _values.Add(pair);
            }
        }

        public EstimationResult Estimate(IDictionary<string, string> runtimeParameters, EstimationResult previousStageResult, bool optimize)
        {
            var result = new EstimationResult(previousStageResult);
            var optimized = Enumerable.Empty<EstimationResult.ParameterValue>();
            if (optimize)
            {
                optimized = Optimize();
                if (previousStageResult != null && previousStageResult.Parameters != null && previousStageResult.Parameters.Count > 0)
                {
                    result.Parameters.AddRange(optimized);
                }
                else
                {
                    result.Parameters = optimized.ToList();
                }
            }            
            foreach (var runtimeParameter in runtimeParameters)
            {
                if (!_values.ContainsKey(runtimeParameter.Key))
                {
                    _values.Add(runtimeParameter);
                }
            }
            result.CalculationTime = ComputeTime(previousStageResult != null ? previousStageResult.Time : 0);
            result.Parameters.AddRange(
                GetParameters().Where(p => optimized.All(op => op.Name != p.Name)).
                    Select(p =>
                        new EstimationResult.ParameterValue {
                            Name = p.Name,
                            InitialValue = GetParameterValue(p.Name),
                            NewValue = GetParameterValue(p.Name) })
            );
            foreach (var runtimeParameter in runtimeParameters)
            {
                _values.Remove(runtimeParameter.Key);
            }
            return result;
        }

        protected abstract double ComputeTime(double? previousStageTime);

        protected abstract List<EstimationResult.ParameterValue> Optimize();

        public double GetNumericParameterValue(string parameterName)
        {
            return Double.Parse(GetParameterValue(parameterName), CultureInfo.InvariantCulture.NumberFormat);
        }

        public bool GetBooleanParameterValue(string parameterName)
        {
            return Boolean.Parse(GetParameterValue(parameterName));
        }

        public string GetParameterValue(string parameterName)
        {
            Debug.Assert(IsParameterAllowed(parameterName));
            var parameter = GetParameter(parameterName);
            switch (parameter.SourceType)
            {
                case ParameterSourceType.Computed:
                case ParameterSourceType.Code:
                    {
                        var property = _props.First(p => p.Name == parameterName && AllowedTypes.Contains(p.PropertyType));
                        var attrs = property.GetCustomAttributes(typeof(ParameterAttribute), false);
                        Debug.Assert(attrs.Length > 0);
                        var attribute = (ParameterAttribute)attrs[0];
                        Debug.Assert((attribute.SourceType == ParameterSourceType.Code) == property.GetGetMethod().IsStatic);
                        return property.GetValue(this, null).ToString();
                    }
                default:
                    {
                        if (_values.ContainsKey(parameterName))
                        {
                            return _values[parameterName];
                        }
                        throw new ParameterNotSetException(_referenceName, parameterName);
                    }
            }
        }

        public bool Check()
        {
            var parameters =
            _parameters.Where(
                p => p.SourceType == ParameterSourceType.Config || p.SourceType == ParameterSourceType.Hardware || p.SourceType == ParameterSourceType.Data);
            return parameters.All(p => _values.ContainsKey(p.Name));
        }

        public bool IsParameterAllowed(string parameterName)
        {
            return _parameters.Any(p => p.Name == parameterName);
        }

        public IEnumerable<ParameterDescription> GetParameters()
        {
            return _parameters;
        }

        public IEnumerable<ParameterDescription> GetParameters(ParameterSourceType sourceType)
        {
            return _parameters.Where(parameter => parameter.SourceType == sourceType).ToList();
        }

        int IModel.Order
        {
            get { return Order; }
        }


        string IModel.ReferenceName
        {
            get { return _referenceName; }
        }

        public ParameterDescription GetParameter(String parameterName)
        {
            return _parameters.SingleOrDefault(p => p.Name == parameterName);
        }

        protected static bool IsParameterTypeAllowed(ParameterSourceType sourceType)
        {
            return sourceType == ParameterSourceType.Config || sourceType == ParameterSourceType.Hardware || sourceType == ParameterSourceType.Runtime || sourceType == ParameterSourceType.Data;
        }
        
    }
}
