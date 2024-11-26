using System;
using System.Runtime.Caching;

public class GenericProxyCache<T> where T : new()
{
    private MemoryCache _cache;
    public DateTimeOffset dt_default { get; set; }

    public GenericProxyCache()
    {
        _cache = new MemoryCache("GenericProxyCache");
        dt_default = ObjectCache.InfiniteAbsoluteExpiration;
    }

    public T Get(string CacheItemName)
    {
        return Get(CacheItemName, dt_default);
    }

    public T Get(string CacheItemName, double dt_seconds)
    {
        return Get(CacheItemName, DateTimeOffset.Now.AddSeconds(dt_seconds));
    }

    public T Get(string CacheItemName, DateTimeOffset dt)
    {
        if (_cache.Contains(CacheItemName))
        {
            return (T)_cache.Get(CacheItemName);
        }
        else
        {
            T newItem = new T();
            _cache.Set(CacheItemName, newItem, new CacheItemPolicy { AbsoluteExpiration = dt });
            return newItem;
        }
    }
}
