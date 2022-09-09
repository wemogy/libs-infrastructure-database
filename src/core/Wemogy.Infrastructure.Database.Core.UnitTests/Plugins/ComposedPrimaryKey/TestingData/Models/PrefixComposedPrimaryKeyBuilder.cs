using System;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.TestingData.Models;

public class PrefixComposedPrimaryKeyBuilder : IComposedPrimaryKeyBuilder<Guid>
{
    private readonly PrefixContext _prefixContext;

    public PrefixComposedPrimaryKeyBuilder(PrefixContext prefixContext)
    {
        _prefixContext = prefixContext;
    }

    public string BuildComposedPrimaryKey(Guid id)
    {
        return $"{_prefixContext.Prefix}_{id}";
    }

    public Guid ExtractIdFromComposedPrimaryKey(string composedPrimaryKey)
    {
        return Guid.Parse(composedPrimaryKey.SplitOnFirstOccurrence("_")[1]);
    }
}
