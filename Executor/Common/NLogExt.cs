using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;

namespace MITP
{
    public static class NLogExt
    {
        private static string AddAffix<T>(Logger logger, string message, T affix)
        {
            string updatedMessage;
            object affixAsObj = (object) affix;

            if (affix is long)
                updatedMessage = "Task " + ((long) affixAsObj).ToString() + ": " + message;
            else
            if (affix is TaskDescription)
                updatedMessage = "Task " + ((TaskDescription) affixAsObj).TaskId + ": " + message;
            else
            if (affix is IEnumerable<TaskDescription>)
                updatedMessage = 
                    message + "Tasks: [" + 
                        String.Join(", ", ((IEnumerable<TaskDescription>) affix).Select(a => a.TaskId.ToString())) + 
                    "]";
            else
            {
                //logger.Error();
                updatedMessage = message;
            }

            return updatedMessage;
        }

        private static void Log<T>(this Logger logger, LogLevel logLevel, T affix, string message, object[] args, Exception ex = null)
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            var nlogEvent = (ex == null)?
                new NLog.LogEventInfo(logLevel, logger.Name, culture, AddAffix(logger, message, affix), args):
                new NLog.LogEventInfo(logLevel, logger.Name, culture, AddAffix(logger, message, affix), args, ex);

            logger.Log(typeof(NLogExt), nlogEvent);
        }


        #region Affixed Logging

        public static void Trace<TAffix>(this Logger logger, TAffix affix, string message, params object[] args)
        {
            logger.Log(LogLevel.Trace, affix, message, args);
        }

        public static void Debug<TAffix>(this Logger logger, TAffix affix, string message, params object[] args)
        {
            logger.Log(LogLevel.Debug, affix, message, args);
        }

        public static void Info<TAffix>(this Logger logger, TAffix affix, string message, params object[] args)
        {
            logger.Log(LogLevel.Info, affix, message, args);
        }

        public static void Warn<TAffix>(this Logger logger, TAffix affix, string message, params object[] args)
        {
            logger.Log(LogLevel.Warn, affix, message, args);
        }

        public static void Error<TAffix>(this Logger logger, TAffix affix, string message, params object[] args)
        {
            logger.Log(LogLevel.Error, affix, message, args);
        }

        public static void Fatal<TAffix>(this Logger logger, TAffix affix, string message, params object[] args)
        {
            logger.Log(LogLevel.Fatal, affix, message, args);
        }

        #endregion



        #region Affixed Exceptions Logging

        public static void WarnException<TAffix>(this Logger logger, Exception ex, TAffix affix, string message, params object[] args)
        {
            logger.Log(LogLevel.Warn, affix, message, args, ex);
        }

        public static void ErrorException<TAffix>(this Logger logger, Exception ex, TAffix affix, string message, params object[] args)
        {
            logger.Log(LogLevel.Error, affix, message, args, ex);
        }

        public static void FatalException<TAffix>(this Logger logger, Exception ex, TAffix affix, string message, params object[] args)
        {
            logger.Log(LogLevel.Fatal, affix, message, args, ex);
        }

        #endregion
    }
}