using System;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.TestingData.Models;

public class StringPrefixComposedPrimaryKeyBuilder : IComposedPrimaryKeyBuilder<string>
{
    private readonly PrefixContext _prefixContext;

    public StringPrefixComposedPrimaryKeyBuilder(PrefixContext prefixContext)
    {
        _prefixContext = prefixContext;
    }

    public string GetComposedPrimaryKeyPrefix()
    {
        return $"{_prefixContext.Prefix}_";
    }

    public string BuildComposedPrimaryKey(string id)
    {
        return $"{_prefixContext.Prefix}_{id}";
    }

    public string ExtractIdFromComposedPrimaryKey(string composedPrimaryKey)
    {
        return composedPrimaryKey.SplitOnFirstOccurrence("_")[1];
    }
}
