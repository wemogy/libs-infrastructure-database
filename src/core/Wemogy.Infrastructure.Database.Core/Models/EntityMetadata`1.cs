using System.Reflection;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Models;

/// <summary>
///     The reflection an entity type needs before it can be written, resolved once and cached per
///     closed type. It is the type-erased counterpart of what
///     <see cref="Abstractions.DatabaseClientBase{TEntity}"/> keeps for its single entity type, and
///     it exists so a mixed-type partition batch can resolve the id, partition key and eTag of an
///     operation whose type it only learns when the operation is added.
/// </summary>
/// <typeparam name="T">The entity type the metadata describes</typeparam>
public static class EntityMetadata<T>
    where T : class
{
    private static readonly PropertyInfo IdProperty;
    private static readonly PropertyInfo? ETagProperty;

    static EntityMetadata()
    {
        var idPropertyInfo = typeof(T).GetPropertyByCustomAttribute<IdAttribute>();
        if (idPropertyInfo == null)
        {
            throw Error.Unexpected(
                "IdPropertyNotFound",
                $"There is no ID attribute specified for the model {typeof(T).FullName}");
        }

        IdProperty = idPropertyInfo;

        // resolves either the single [PartitionKey] property or the ordered
        // [HierarchicalPartitionKey] ones, and throws if the declaration is broken
        Definition = PartitionKeyDefinition.Resolve(typeof(T));

        // optional: entities opt into optimistic concurrency via the [ETag] attribute
        ETagProperty = typeof(T).GetPropertyByCustomAttribute<ETagAttribute>();
    }

    /// <summary>
    ///     The property holding the broadest component of the partition key. The multi-tenant
    ///     plugin composes its prefix into this component alone.
    /// </summary>
    public static PropertyInfo PartitionKeyProperty => Definition.FirstProperty;

    /// <summary>
    ///     Whether the entity type opts into optimistic concurrency via the
    ///     <see cref="ETagAttribute"/> and the eTag can be assigned.
    /// </summary>
    public static bool SupportsETag => ETagProperty is { CanWrite: true };

    internal static PartitionKeyDefinition Definition { get; }

    /// <summary>
    ///     Returns the id value of the entity.
    /// </summary>
    /// <param name="entity">The entity to read the id of</param>
    /// <returns>The id value of the entity</returns>
    public static string ResolveId(T entity)
    {
        return (string)IdProperty.GetValue(entity)!;
    }

    /// <summary>
    ///     Returns the partition key of the entity, which carries one component per property the
    ///     entity type declares its key with.
    /// </summary>
    /// <param name="entity">The entity to read the partition key of</param>
    /// <returns>The partition key of the entity</returns>
    public static PartitionKeyValue ResolvePartitionKey(T entity)
    {
        return Definition.GetValue(entity);
    }

    /// <summary>
    ///     Returns the eTag value of the entity, or null if the entity does not opt into optimistic
    ///     concurrency via the <see cref="ETagAttribute"/>.
    /// </summary>
    /// <param name="entity">The entity to read the eTag of</param>
    /// <returns>The eTag value, or null when the entity does not carry one</returns>
    public static string? ResolveETag(T entity)
    {
        return (string?)ETagProperty?.GetValue(entity);
    }

    /// <summary>
    ///     Assigns the eTag value of the entity. Does nothing if the entity does not opt into
    ///     optimistic concurrency via the <see cref="ETagAttribute"/>.
    /// </summary>
    /// <param name="entity">The entity to stamp</param>
    /// <param name="eTag">The eTag value to assign</param>
    public static void SetETag(T entity, string? eTag)
    {
        if (!SupportsETag)
        {
            return;
        }

        ETagProperty!.SetValue(entity, eTag);
    }

    /// <summary>
    ///     Throws when the partition key the caller passed in is not as deep as the entity type is
    ///     partitioned by.
    /// </summary>
    /// <param name="partitionKey">The partition key to validate</param>
    public static void EnsurePartitionKeyDepth(PartitionKeyValue partitionKey)
    {
        Definition.EnsureDepth(partitionKey, typeof(T));
    }
}
