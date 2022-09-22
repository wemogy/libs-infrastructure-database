namespace Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Abstractions;

public interface IComposedPrimaryKeyBuilder<TId> : IComposedPrimaryKeyBuilder
{
    string BuildComposedPrimaryKey(TId id);

    TId ExtractIdFromComposedPrimaryKey(string composedPrimaryKey);
}
