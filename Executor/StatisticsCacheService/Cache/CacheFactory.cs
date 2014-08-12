using System;
using System.Collections.Generic;
using System.Configuration;
using Common;
using CommonDataTypes;
using CommonDataTypes.RExService.Service.Entity.Info;
using ServiceProxies.SchedulerService;
using StatisticsCacheService.Cache.FileDumping;
using StatisticsCacheService.Cache.GarbageCollecting;
using StatisticsCacheService.Helpers;

namespace StatisticsCacheService.Cache
{
    public class CacheFactory
    {
        public const string CACHE_EXPIRATION_TIME = "CacheExpirationTime";
        public const string ARCHIVE_DUMPING_TIME = "ArchiveExpirationTime";
        public const string CACHE_GARBAGE_COLLECTING_INTERVAL = "CacheGarbageCollectingInterval";
        public const string DEBUG_PARAM = "Debug";

        private static CacheFactory _instance;

        private static string pathToBackUpFolder = "./Saved";

        public string GetPathToBackUpFolder()
        {
            return pathToBackUpFolder;
        }

        public static CacheFactory GetFactory()
        {
            return _instance ?? (_instance = new CacheFactory());
        }

        private static int GetConfigValueOrDefaultByName(string name, int _default)
        {
            var time = ConfigurationManager.AppSettings[name];
            int result = 0;
            return Int32.TryParse(time, out result) ? result : _default; 
        }

        private static string GetConfigValueOrDefaultByName(string name, string _default)
        {
            var value = ConfigurationManager.AppSettings[name];
            
            return value ?? _default;
        }

        private static int GetExpirationTime()
        {
            return GetConfigValueOrDefaultByName(CACHE_EXPIRATION_TIME, 1000*60*30 /*30 min*/);
        }

        private static int GetArchiveDumpingTime()
        {
            return GetConfigValueOrDefaultByName(ARCHIVE_DUMPING_TIME, 1000 * 60 * 30 /*30 min*/);
        }

        private static int GetCacheGarbageCollectingInterval()
        {
            return GetConfigValueOrDefaultByName(CACHE_GARBAGE_COLLECTING_INTERVAL, 1000 * 60 * 5 /*5 min*/);
        }

        private static string GetDebugSettingValue()
        {
            return GetConfigValueOrDefaultByName(DEBUG_PARAM, "false");
        }

        private DataDumper taskDumper = new DataDumper(pathToBackUpFolder, "task");
        private DataDumper resourceDumper = new DataDumper(pathToBackUpFolder, "resource");

        public TaskCache CreateTaskCache()
        {
            var cache = new TaskCache(TimeSpan.FromMilliseconds(CacheFactory.GetExpirationTime()));
            new TreeCacheGarbageCollector(cache, CacheFactory.GetArchiveDumpingTime(), taskDumper);
            return cache;
        }

        public ResourceCache CreateResourceCache()
        {
//            var debug = GetDebugSettingValue();
//            if (debug != "false")
//            {
//                var cache1 = new ResourceCache(TimeSpan.FromMilliseconds(CacheFactory.GetExpirationTime()));
//
//                new TreeCacheGarbageCollector(cache1, CacheFactory.GetArchiveDumpingTime(), resourceDumper);
//
//                var mock = ProfilingSamples.createHighLoadMockData();
//
//                cache1.AddAllInfo(mock);
//
//                return cache1;
//            }

            var cache = new ResourceCache(TimeSpan.FromMilliseconds(CacheFactory.GetExpirationTime()));

            new TreeCacheGarbageCollector(cache, CacheFactory.GetArchiveDumpingTime(), resourceDumper);
            return cache;
        }

        public DataDumper GetTaskDumper()
        {
            return taskDumper;
        }

        public DataDumper GetResourceDumper()
        {
            return resourceDumper;
        }
    }
}
