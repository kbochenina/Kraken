using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StatisticsCacheService.Cache
{
    //todo finish it later
    //this component isn't used now
    //it is assumed The component will be entered in an explutation later.
    class FarmCacheContainer
    {
        private readonly object _lock = new object();

        private Dictionary<string,CacheCollection> storage = new Dictionary<string, CacheCollection>(); 

        public ResourceCache GetResourceCacheByFarmName(string farmName)
        {
            lock (_lock)
            {
                if (!storage.ContainsKey(farmName))
                {
                    storage.Add(farmName, createCacheCollection());
                }

                return storage[farmName].ResourceCache;
            }
        }

        public TaskCache GetTaskCacheByFarmName(string farmName)
        {
            lock (_lock)
            {
                if (!storage.ContainsKey(farmName))
                {
                    storage.Add(farmName, createCacheCollection());
                }

                return storage[farmName].TaskCache;
            }
        }

        private CacheCollection createCacheCollection()
        {
            return new CacheCollection()
            {
                ResourceCache = CacheFactory.GetFactory().CreateResourceCache(),
                TaskCache = CacheFactory.GetFactory().CreateTaskCache(),
            };
        }

        private class CacheCollection
        {
            public ResourceCache ResourceCache{ get; set; }

            public TaskCache TaskCache{get; set;}
        }

    }
}
