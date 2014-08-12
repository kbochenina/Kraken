using System;
using CommonDataTypes.Utility;
using StatisticsCacheService.Cache.FileDumping;

namespace StatisticsCacheService.Cache.GarbageCollecting
{
    public class TreeCacheGarbageCollector
    {
        private readonly ICacheTrasher trasher;
        private readonly int interval;
        private readonly DataDumper dumper;

        public TreeCacheGarbageCollector(ICacheTrasher trasher, int interval, DataDumper dumper)
        {
            this.trasher = trasher;
            this.interval = interval;
            this.dumper = dumper;

            RunGarbageProcess();
        }

        public void RunGarbageProcess()
        {
            Utility.CreateAndRunRepeatedProcess(interval, false, Run);
        }

        public void Run()
        {
            Common.Utility.ExceptionablePlaceWrapper(() =>
            {
               var data = trasher.CollectGarbage();

               this.dumper.FireDump(data.Item1,data.Item2,data.Item3); 

               trasher.ApplySurvived();

            }, "Garbage collecting with a collector of type " + this.GetType() + " failed",
               "Garbage collecting with a collector of type " + this.GetType() + " successed");
        }
    }

    public interface ICacheTrasher
    {
        Tuple<object,DateTime,DateTime> CollectGarbage();
        void ApplySurvived();
    }

}
