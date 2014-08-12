using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MITP
{
    public sealed class PackageParametersChecker : IDisposable
    {
        private List<string> _unusedParameters = null;
        private ulong _sequenceId = 0;

        public PackageParametersChecker(ulong sequenceId, Dictionary<string, string> @params)
        {
            _unusedParameters = new List<string>(@params.Keys.ToArray<string>());
            _sequenceId = sequenceId;
        }

        public void MarkParameterAsUsed(string paramName)
        {
            if (_unusedParameters.Contains(paramName))
                _unusedParameters.Remove(paramName);
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (_unusedParameters != null)
            {
                foreach (string paramName in _unusedParameters)
                {
                    Log.Warn(String.Format("Обнаружен неиспользованный параметр <{0}> в цепочке {1}",
                        paramName, _sequenceId
                    ));
                }
            }
        }

        #endregion
    }
}