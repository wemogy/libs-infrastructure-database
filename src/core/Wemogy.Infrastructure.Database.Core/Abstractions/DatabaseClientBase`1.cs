using System.Reflection;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public abstract class DatabaseClientBase<TEntity>
    where TEntity : class
{
    private readonly PartitionKeyDefinition _partitionKeyDefinition;
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

        // resolves either the single [PartitionKey] property or the ordered
        // [HierarchicalPartitionKey] ones, and throws if the declaration is broken
        _partitionKeyDefinition = PartitionKeyDefinition.Resolve(typeof(TEntity));

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

    /// <summary>
    ///     Returns the partition key of the entity, which carries one component per property the
    ///     entity type declares its key with.
    /// </summary>
    protected PartitionKeyValue ResolvePartitionKey(TEntity entity)
    {
        return _partitionKeyDefinition.GetValue(entity);
    }

    /// <summary>
    ///     Rejects a partition key a caller passed in that is not as deep as the entity type is
    ///     partitioned by, so a key of the wrong shape is named as such instead of quietly
    ///     addressing a partition nothing lives in.
    /// </summary>
    protected void EnsurePartitionKeyDepth(PartitionKeyValue partitionKey)
    {
        _partitionKeyDefinition.EnsureDepth(
            partitionKey,
            typeof(TEntity));
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
