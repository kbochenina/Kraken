using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace Common
{
    public static class Utility
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static List<string> ReverseStringFormat(string template, string str)
        {
            string pattern = "^" + Regex.Replace(template, @"\{[0-9]+\}", "(.*?)") + "$"; 
            Regex r = new Regex(pattern); Match m = r.Match(str); 
            List<string> ret = new List<string>(); 
            for (int i = 1; i < m.Groups.Count; i++)
            {
                ret.Add(m.Groups[i].Value);
            } 
            return ret;
        }


        public static void ExceptionablePlaceWrapper(Action code, string messageIfExceptionOccured="",
                                                                  string messageForSuccess="", 
                                                                  bool rethrown = true)
        {
            ExceptionablePlaceWrapper(code,messageIfExceptionOccured,
                                           messageForSuccess,
                                           messageIfExceptionOccured != null,
                                           true,
                                           rethrown);
        }

        public static void ExceptionablePlaceWrapper(Action code, string messageIfExceptionOccured,
                                                                  string messageForSuccess, 
                                                                  bool logMessageIfExceptionOccured, 
                                                                  bool logMessageForSuccess, 
                                                                  bool rethrown = true)
        {
            bool isOccured = false;
            try
            {
                code();
            }
            catch (Exception ex)
            {
                isOccured = true;
                if (logMessageIfExceptionOccured)
                {
                    Log.Error(messageIfExceptionOccured, ex);    
                }
                if (rethrown)
                {
                    throw;
                }
            }

            if (logMessageForSuccess && !isOccured && !string.IsNullOrEmpty(messageForSuccess))
            {
                Log.Info(messageForSuccess);
            }
        }

        public static string FormatParams(params Tuple<string,string>[] data)
        {
            var format = "{0}={1}\r\n";
            var result = new StringBuilder();
            foreach (var tuple in data)
            {
                result.AppendFormat(format, tuple.Item1, tuple.Item2);
            }
            return result.ToString();
        }

        public static string Message(string message, params Tuple<string, string>[] data)
        {
            return string.Format("{0}\r\n Params:\r\n {1}",message,FormatParams(data));
        }

        public static Tuple<string,string> Arg<T>(string name, T val)
        {
            return new Tuple<string, string>(name, "" + val);
        }

        //todo it will be better to redesign it later
        public static void LogError(string message)
        {
            Log.Error(message);
        }

        public static void LogError(string message, Exception ex)
        {
            Log.ErrorException(message,ex);
        }

        public static void LogInfo(string message)
        {
            Log.Info(message);
        }

        public static void LogDebug(string message)
        {
            Log.Debug(message);
        }

        public static void LogWarn(string message)
        {
            Log.Warn(message);
        }
    }
}
