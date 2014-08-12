using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MITP
{
    public static class TimeSpanExt
    {
        public static TimeSpan Measure(Action action)
        {
            var actionStarted = DateTime.Now;
            action();
            var actionFinished = DateTime.Now;

            var timeTaken = actionFinished - actionStarted;
            return timeTaken;
        }
    }
}