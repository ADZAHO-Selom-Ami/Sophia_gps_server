using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;

namespace Proxy
{
    public class GenericProxyCache<T>
    {
        private ObjectCache _cache = MemoryCache.Default;
        private CacheItemPolicy _defaultCachePolicy = new CacheItemPolicy { AbsoluteExpiration = ObjectCache.InfiniteAbsoluteExpiration };

        public T Get(string cacheItemName)
        {
            return Get(cacheItemName, _defaultCachePolicy);
        }
        public T Get(string cacheItemName, double dt_seconds)
        {
            var expiration = DateTimeOffset.Now.AddSeconds(dt_seconds);
            var cachePolicy = new CacheItemPolicy { AbsoluteExpiration = expiration };
            return Get(cacheItemName, cachePolicy);
        }
        public T Get(string cacheItemName, DateTimeOffset dt)
        {
            var cachePolicy = new CacheItemPolicy { AbsoluteExpiration = dt };
            return Get(cacheItemName, cachePolicy);
        }
        private T Get(string cacheItemName, CacheItemPolicy cachePolicy)
        {
            var cacheItem = _cache.Get(cacheItemName);
            if (cacheItem == null)
            {
                var newItem = Activator.CreateInstance<T>();
                _cache.Add(cacheItemName, newItem, cachePolicy);
                return newItem;
            }
            return (T)cacheItem;
        }
    }
}
