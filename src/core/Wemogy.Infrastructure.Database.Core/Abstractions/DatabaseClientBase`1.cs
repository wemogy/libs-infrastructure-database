using System.Reflection;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Models;

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

    /// <summary>
    ///     Throws when the entity carries a decimal that the scale declared by its
    ///     <see cref="FixedPointAttribute"/> cannot store exactly. Called by every provider before
    ///     it writes, so the in-memory provider refuses the values Cosmos DB would refuse - and it
    ///     is the reason a stored value is always exactly the scaled integer divided by its factor,
    ///     which is what lets the two providers agree on what is stored.
    /// </summary>
    /// <param name="entity">The entity that is about to be written</param>
    protected static void EnsureFixedPointValuesAreValid(TEntity entity)
    {
        FixedPointMetadata.EnsureValuesAreValid(entity);
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

    /// <summary>
    ///     Gets a value indicating whether the entity type opts into optimistic concurrency via the
    ///     <see cref="ETagAttribute"/> <em>and</em> the eTag can be assigned.
    ///     <para>
    ///         A getter-only eTag property cannot be maintained, so optimistic concurrency stays
    ///         off for it rather than failing every write. <see cref="IEntityBase"/> only declares
    ///         the getter, so an entity implementing it directly can end up in that shape.
    ///     </para>
    /// </summary>
    protected bool SupportsETag => _eTagPropertyInfo is { CanWrite: true };

    /// <summary>
    ///     Assigns the eTag value of the entity. Does nothing if the entity does not opt into
    ///     optimistic concurrency via the <see cref="ETagAttribute"/>.
    ///     <para>
    ///         Needed by clients that own the stored value themselves instead of receiving it from
    ///         the database, e.g. the in-memory client. Providers like Cosmos get the eTag assigned
    ///         by their serializer.
    ///     </para>
    /// </summary>
    protected void SetETagValue(TEntity entity, string? eTag)
    {
        if (!SupportsETag)
        {
            return;
        }

        // works for init-only properties too, the init accessor is a regular setter for reflection
        _eTagPropertyInfo!.SetValue(entity, eTag);
    }
}
