using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scheduler
{
    static class State
    {
        public static string DefaultUHName = System.Configuration.ConfigurationManager.AppSettings[TaskScheduler.DefaultUrgentHeuristicsKey];
        public static string DefaultHName = System.Configuration.ConfigurationManager.AppSettings[TaskScheduler.DefaultHeuristicsKey];
    }
}
