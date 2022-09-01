using System.Collections.Generic;

namespace Wemogy.Infrastructure.Database.Core.Registries;

public abstract class RegistryBase<TKey, TValue>
{
    private static readonly Dictionary<TKey, TValue> EntriesCache = new Dictionary<TKey, TValue>();

    protected TValue GetRegistryEntry(TKey key)
    {
        if (!EntriesCache.TryGetValue(key, out TValue value))
        {
            value = InitializeEntry(key);
            EntriesCache.Add(key, value);
        }

        return value;
    }

    protected abstract TValue InitializeEntry(TKey key);
}
