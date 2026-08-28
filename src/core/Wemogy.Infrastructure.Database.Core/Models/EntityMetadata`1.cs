using System;
using System.Reflection;
using System.Threading;
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
    /// <summary>
    ///     Constructed here, but resolved on first use. Resolving in the field initializer would run
    ///     the reflection inside the type initializer, so an entity type with a broken declaration
    ///     would reach the caller as a <see cref="TypeInitializationException"/> instead of the
    ///     named error this library raises everywhere else - and the CLR caches a failed type
    ///     initializer, so every later touch of the type would keep throwing that wrapper.
    ///     <para>
    ///         <see cref="LazyThreadSafetyMode.PublicationOnly"/> because it does not cache the
    ///         failure either: the named error is raised again on the next call rather than once.
    ///         Resolving twice under a race is harmless, it only reads attributes.
    ///     </para>
    /// </summary>
    private static readonly Lazy<EntityMembers> Members = new Lazy<EntityMembers>(
        Resolve,
        LazyThreadSafetyMode.PublicationOnly);

    /// <summary>
    ///     The property holding the broadest component of the partition key. The multi-tenant
    ///     plugin composes its prefix into this component alone.
    /// </summary>
    public static PropertyInfo PartitionKeyProperty => Members.Value.Definition.FirstProperty;

    internal static PartitionKeyDefinition Definition => Members.Value.Definition;

    /// <summary>
    ///     Returns the id value of the entity.
    /// </summary>
    /// <param name="entity">The entity to read the id of</param>
    /// <returns>The id value of the entity</returns>
    public static string ResolveId(T entity)
    {
        return (string)Members.Value.IdProperty.GetValue(entity)!;
    }

    /// <summary>
    ///     Returns the partition key of the entity, which carries one component per property the
    ///     entity type declares its key with.
    /// </summary>
    /// <param name="entity">The entity to read the partition key of</param>
    /// <returns>The partition key of the entity</returns>
    public static PartitionKeyValue ResolvePartitionKey(T entity)
    {
        return Members.Value.Definition.GetValue(entity);
    }

    /// <summary>
    ///     Returns the eTag value of the entity, or null if the entity does not opt into optimistic
    ///     concurrency via the <see cref="ETagAttribute"/>.
    /// </summary>
    /// <param name="entity">The entity to read the eTag of</param>
    /// <returns>The eTag value, or null when the entity does not carry one</returns>
    public static string? ResolveETag(T entity)
    {
        return (string?)Members.Value.ETagProperty?.GetValue(entity);
    }

    /// <summary>
    ///     Throws when the partition key the caller passed in is not as deep as the entity type is
    ///     partitioned by.
    /// </summary>
    /// <param name="partitionKey">The partition key to validate</param>
    public static void EnsurePartitionKeyDepth(PartitionKeyValue partitionKey)
    {
        Members.Value.Definition.EnsureDepth(
            partitionKey,
            typeof(T));
    }

    private static EntityMembers Resolve()
    {
        var idPropertyInfo = typeof(T).GetPropertyByCustomAttribute<IdAttribute>();
        if (idPropertyInfo == null)
        {
            throw Error.Unexpected(
                "IdPropertyNotFound",
                $"There is no ID attribute specified for the model {typeof(T).FullName}");
        }

        // resolves either the single [PartitionKey] property or the ordered
        // [HierarchicalPartitionKey] ones, and throws if the declaration is broken
        var definition = PartitionKeyDefinition.Resolve(typeof(T));

        // optional: entities opt into optimistic concurrency via the [ETag] attribute
        var eTagPropertyInfo = typeof(T).GetPropertyByCustomAttribute<ETagAttribute>();

        return new EntityMembers(
            idPropertyInfo,
            definition,
            eTagPropertyInfo);
    }

    private sealed class EntityMembers
    {
        public EntityMembers(
            PropertyInfo idProperty,
            PartitionKeyDefinition definition,
            PropertyInfo? eTagProperty)
        {
            IdProperty = idProperty;
            Definition = definition;
            ETagProperty = eTagProperty;
        }

        public PropertyInfo IdProperty { get; }

        public PartitionKeyDefinition Definition { get; }

        public PropertyInfo? ETagProperty { get; }
    }
}
