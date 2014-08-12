using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;

namespace TimeMeter
{
    /// <summary>
    /// Интерфейс модели
    /// </summary>
    public interface IModel
    {
        /// <summary>
        /// Номер модели в последовательности запуска
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Имя модели для использования в файлах конфигурации
        /// </summary>
        String ReferenceName { get; }

        /// <summary>
        /// Проверка корректности построения модели
        /// </summary>
        /// <returns>Результат проверки</returns>
        bool Check();

        /// <summary>
        /// Проверка допустимости параметра по его имени
        /// </summary>
        /// <param name="parameterName">Имя параметра</param>
        /// <returns>Результат проверки</returns>
        bool IsParameterAllowed(string parameterName);

        bool IsParameterWritable(string parameterName);

        ParameterDescription GetParameter(string parameterName);
        IEnumerable<ParameterDescription> GetParameters();
        IEnumerable<ParameterDescription> GetParameters(ParameterSourceType sourceType);

        void SetParametersValues(IDictionary<string, string> values);

        /// <summary>
        /// Получение текущего значения параметра по его имени
        /// </summary>
        /// <param name="parameterName">Имя параметра</param>
        /// <returns>Значение указанного параметра, либо null при его отсутствии</returns>
        double GetNumericParameterValue(string parameterName);

        /// <summary>
        /// Получение текущего значения параметра по его имени
        /// </summary>
        /// <param name="parameterName">Имя параметра</param>
        /// <returns>Значение указанного параметра, либо null при его отсутствии</returns>
        bool GetBooleanParameterValue(string parameterName);

        /// <summary>
        /// Получение текущего значения параметра по его имени
        /// </summary>
        /// <param name="parameterName">Имя параметра</param>
        /// <returns>Значение указанного параметра, либо null при его отсутствии</returns>
        string GetParameterValue(string parameterName);

        string SetParameterValue(string parameterName, string newValue);
        bool SetBooleanParameterValue(string parameterName, bool newValue);

        /// <summary>
        /// Установка нового значения для параметра по его имени
        /// </summary>
        /// <param name="parameterName">Имя параметра</param>
        /// <param name="newValue">Значение параметра</param>
        /// <returns>Предыдущее значение указанного параметра, либо null при его отсутствии</returns>
        double SetNumericParameterValue(string parameterName, double newValue);
        EstimationResult Estimate(IDictionary<string, string> runtimeParameters, EstimationResult previousStageResult, bool optimize);
    }

    public struct ParameterDescription : IEquatable<ParameterDescription>
    {
        public ParameterDescription(string name, ParameterSourceType sourceType)
            : this()
        {            
            Name = name;
            SourceType = sourceType;
        }

        public bool Equals(ParameterDescription other)
        {
            return ReferenceEquals(null, other) ? false : Equals(other.Name, Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (obj.GetType() != typeof (ParameterDescription)) return false;
            return Equals((ParameterDescription) obj);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public static bool operator ==(ParameterDescription left, ParameterDescription right)
        {
            if (ReferenceEquals(left, null) != ReferenceEquals(right, null))
            {
                return false;
            }
            if (ReferenceEquals(left, null))
            {
                return true;
            }
            return left.Equals(right);
        }

        public static bool operator !=(ParameterDescription left, ParameterDescription right)
        {
            if (ReferenceEquals(left, null) != ReferenceEquals(right, null))
            {
                return true;
            }
            if (ReferenceEquals(left, null))
            {
                return false;
            }
            return  !left.Equals(right);
        }

        public string Name { get; private set; }
        public ParameterSourceType SourceType { get; private set; }
    }

    public enum ParameterSourceType
    {
        Code,
        Computed,
        Config,
        Data,
        Hardware,
        Runtime
    }

    [DataContract]
    public class EstimationResult
    {
        [DataMember]
        public double CalculationTime;

        [DataMember]
        public double Overheads;

        public double Time
        {
            get
            {
                return CalculationTime + Overheads;
            }
        }

        [DataMember]
        public List<ParameterValue> Parameters = new List<ParameterValue>();

        [DataContract]
        public struct ParameterValue
        {
            [DataMember]
            public string Name;

            [DataMember]
            public string InitialValue;

            [DataMember]
            public string NewValue;
        }
        
        public EstimationResult(EstimationResult previousStageResult)
        {
            if (previousStageResult != null && previousStageResult.Parameters != null && previousStageResult.Parameters.Count > 0)
            {
                Parameters.AddRange(previousStageResult.Parameters);
            }
        }
    }

    public class ModelOrderer : IComparer<IModel>
    {

        #region IComparer<IModel> Members

        public int Compare(IModel x, IModel y)
        {
            return x.Order - y.Order;
        }

        #endregion
    }

    public abstract class ModelException : ApplicationException
    {
        protected string ModelName { get; private set; }

        protected ModelException(string modelName)
        {
            ModelName = modelName;
        }
    }

    public class ApplicationNotFoundException : ModelException
    {
        public ApplicationNotFoundException(string modelName) : base(modelName)
        {
        }

        public override string Message
        {
            get { return String.Format("Application not found: {0}", ModelName); }
        }
    }

    public class ModelNotFoundException : ModelException
    {
        public ModelNotFoundException(string modelName) : base(modelName)
        {
        }

        public override string Message
        {
            get { return String.Format("Model not found: {0}", ModelName); }
        }
    }

    public class ModelReferenceNameException : ModelException
    {
        public ModelReferenceNameException(string modelName) : base(modelName)
        {
        }

        public override string Message
        {
            get { return String.Format("Reference name is not set for the model: {0}", ModelName); }
        }
    }

    public abstract class ModelParameterException : ModelException
    {
        protected string ParameterName { get; private set; }
        

        protected ModelParameterException(string modelName, string parameterName) : base(modelName)
        {   
            ParameterName = parameterName;
        }
    }

    public class ParameterNotSetException : ModelParameterException
    {
        public ParameterNotSetException(string modelName, string parameterName) : base(modelName, parameterName)
        {
        }

        public override string Message
        {
            get
            {   
                return String.Format("Parameter is not set: model {0}, parameter {1}", ModelName, ParameterName);
            }
        }
    }

    public class ParameterDeclarationException : ModelParameterException
    {
        public ParameterDeclarationException(string modelName, string parameterName)
            : base(modelName, parameterName)
        {
        }

        public override string Message
        {
            get
            {
                return String.Format("Parameter declared incorrectly: model {0}, parameter {1}", ModelName, ParameterName);
            }
        }
    }
}
