using System.Reflection;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public abstract class DatabaseClientBase<TEntity>
    where TEntity : class
{
    private readonly PropertyInfo _partitionKeyPropertyInfo;
    private readonly PropertyInfo _idPropertyInfo;
    private readonly PropertyInfo? _eTagPropertyInfo;

    protected DatabaseClientBase()
    {
        var idPropertyInfo = typeof(TEntity).GetPropertyByCustomAttribute<IdAttribute>();
        if (idPropertyInfo == null)
        {
            throw Error.Unexpected(
                "IdPropertyNotFound",
                $"There is no ID attribute specified for the model {typeof(TEntity).FullName}");
        }

        _idPropertyInfo = idPropertyInfo;

        var partitionKeyPropertyInfo = typeof(TEntity).GetPropertyByCustomAttribute<PartitionKeyAttribute>();
        if (partitionKeyPropertyInfo == null)
        {
            throw Error.Unexpected(
                "PartitionKeyPropertyNotFound",
                $"There is no PartitionKey attribute specified for the model {typeof(TEntity).FullName}");
        }

        _partitionKeyPropertyInfo = partitionKeyPropertyInfo;

        // optional: entities opt into optimistic concurrency via the [ETag] attribute
        _eTagPropertyInfo = typeof(TEntity).GetPropertyByCustomAttribute<ETagAttribute>();
    }

    protected string ResolveIdValue(TEntity entity)
    {
        var idValue = (string)_idPropertyInfo.GetValue(entity)!;
        return idValue;
    }

    protected string ResolvePartitionKeyValue(TEntity entity)
    {
        var partitionKeyValue = (string)_partitionKeyPropertyInfo.GetValue(entity)!;
        return partitionKeyValue;
    }

    /// <summary>
    ///     Returns the eTag value of the entity, or null if the entity does not opt into
    ///     optimistic concurrency via the <see cref="ETagAttribute"/>.
    /// </summary>
    protected string? ResolveETagValue(TEntity entity)
    {
        return (string?)_eTagPropertyInfo?.GetValue(entity);
    }
}
