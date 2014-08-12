using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scheduler
{
    public static class DictUtils
    {
        public static TValue GetOrCreateValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> valueProvider)
        {
            TValue ret;
            if (!dictionary.TryGetValue(key, out ret))
            {
                ret = valueProvider();
                dictionary[key] = ret;
            }
            return ret;
        }
    }
}
