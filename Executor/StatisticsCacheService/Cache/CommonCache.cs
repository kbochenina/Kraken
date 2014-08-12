using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using Common;
using StatisticsCacheService.Cache.GarbageCollecting;

namespace StatisticsCacheService.Cache
{
    public abstract class CommonCache<T> : ICacheTrasher
    {

        //in milliseconds
        public const int DEFAULT_GARBAGE_COLLECTING_INTERVAL = 60*5*1000;

        private static readonly TimeSpan DEFAULT_TIME_OF_LIFE = new TimeSpan(2,0,0);

        public TimeSpan TimeOfLife {get; private set;}

        public CommonCache() : this(DEFAULT_TIME_OF_LIFE) { }

        public CommonCache(TimeSpan lifeTime)
        {
            TimeOfLife = lifeTime;
        }

        public abstract void AddAllInfo(T data);

        public T GetAllInfoStartedWith(DateTime date)
        {
            T result = default(T);
            
            Utility.ExceptionablePlaceWrapper(() =>
            {
                result = GetStartedWith(date, false);
            }, "GetAllInfoStartedWith in a cache of type " + this.GetType() + " failed",
               "GetAllInfoStartedWith in a cache of type " + this.GetType() + " successed");

            return result;
        }

        public T GetAndRemoveAllInfoStartedWith(DateTime date)
        {
            return GetStartedWith(date, false);
        }

        protected abstract T GetStartedWith(DateTime date,
             bool withRemoving);


        public class RemovedElementsEventArgs : EventArgs
        {
            public List<object> RemovedElements { get; set; } 
        }

        public event EventHandler<RemovedElementsEventArgs> ElementsExpired;

        public void OnElementsExpired(RemovedElementsEventArgs arg)
        {
            if (ElementsExpired != null)
            {
                ElementsExpired.Invoke(this,arg);
            }
        }

        public abstract Tuple<object,DateTime,DateTime> CollectGarbage();
        public abstract void ApplySurvived();
    }
}