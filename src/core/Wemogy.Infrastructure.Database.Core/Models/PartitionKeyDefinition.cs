using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Wemogy.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Models;

/// <summary>
///     The properties an entity type is partitioned by, in the order they form the key. Resolved
///     once per entity type and shared by the clients and the multi-tenant plugin, so all of them
///     agree on what the partition key of an entity is and reject a broken declaration with the
///     same error.
/// </summary>
internal sealed class PartitionKeyDefinition
{
    private readonly PropertyInfo[] _properties;

    private PartitionKeyDefinition(PropertyInfo[] properties)
    {
        _properties = properties;
    }

    /// <summary>
    ///     The properties forming the key, ordered from the broadest component to the narrowest.
    ///     Always holds at least one entry.
    /// </summary>
    public IReadOnlyList<PropertyInfo> Properties => _properties;

    /// <summary>
    ///     The property holding the broadest component of the key. The multi-tenant plugin
    ///     composes its prefix into this one, and it is the property a partition key filter
    ///     addresses.
    /// </summary>
    public PropertyInfo FirstProperty => _properties[0];

    /// <summary>
    ///     Gets a value indicating whether the key is built from more than one property.
    /// </summary>
    public bool IsHierarchical => _properties.Length > 1;

    /// <summary>
    ///     Resolves the partition key declaration of an entity type.
    /// </summary>
    /// <param name="entityType">The entity type to read the attributes of</param>
    /// <returns>The resolved definition</returns>
    public static PartitionKeyDefinition Resolve(Type entityType)
    {
        var properties = GetDeclaredProperties(entityType);

        var singleProperties = properties
            .Where(x => x.GetCustomAttribute<PartitionKeyAttribute>() != null)
            .ToList();

        var hierarchicalProperties = properties
            .Select(x => (Property: x, Attribute: x.GetCustomAttribute<HierarchicalPartitionKeyAttribute>()))
            .Where(x => x.Attribute != null)
            .ToList();

        if (singleProperties.Count > 0 && hierarchicalProperties.Count > 0)
        {
            throw Error.Unexpected(
                "PartitionKeyDefinitionAmbiguous",
                $"The model {entityType.FullName} declares its partition key with both the PartitionKey and the HierarchicalPartitionKey attribute. Use one of them");
        }

        if (hierarchicalProperties.Count > 0)
        {
            return ResolveHierarchical(
                entityType,
                hierarchicalProperties!);
        }

        if (singleProperties.Count == 0)
        {
            throw Error.Unexpected(
                "PartitionKeyPropertyNotFound",
                $"There is no PartitionKey attribute specified for the model {entityType.FullName}");
        }

        // more than one [PartitionKey] used to resolve to whichever the reflection order handed
        // back first, which silently partitions by a property the caller did not mean
        if (singleProperties.Count > 1)
        {
            throw Error.Unexpected(
                "PartitionKeyDefinitionAmbiguous",
                $"The model {entityType.FullName} carries the PartitionKey attribute on more than one property ({string.Join(", ", singleProperties.Select(x => x.Name))}). Use the HierarchicalPartitionKey attribute to declare a key of several components");
        }

        EnsurePropertyIsAString(
            entityType,
            singleProperties[0]);

        return new PartitionKeyDefinition(new[] { singleProperties[0] });
    }

    /// <summary>
    ///     Reads the partition key of an entity.
    /// </summary>
    /// <param name="entity">The entity to read the key of</param>
    /// <returns>The key, with one component per declared property</returns>
    public PartitionKeyValue GetValue(object entity)
    {
        var components = new string[_properties.Length];
        for (var index = 0; index < _properties.Length; index++)
        {
            components[index] = (string)_properties[index].GetValue(entity)!;
        }

        return new PartitionKeyValue(components);
    }

    /// <summary>
    ///     Rejects a key that is not as deep as the entity type is partitioned by. A string
    ///     converts to a one-component key implicitly, so passing the broadest component alone to
    ///     a repository over a hierarchically partitioned entity compiles cleanly - and would
    ///     otherwise address a partition that no write ever lands in, reported as a plain
    ///     not-found on a read and as a document nobody can read back on a write.
    /// </summary>
    /// <param name="partitionKey">The key a caller passed in</param>
    /// <param name="entityType">The entity type, for the message</param>
    public void EnsureDepth(PartitionKeyValue partitionKey, Type entityType)
    {
        if (partitionKey == null)
        {
            throw Error.Unexpected(
                "PartitionKeyValueNull",
                "The partition key can not be null");
        }

        if (partitionKey.Count != _properties.Length)
        {
            throw Error.Unexpected(
                "PartitionKeyDepthMismatch",
                $"The partition key {partitionKey} carries {partitionKey.Count} component(s), but the model {entityType.FullName} is partitioned by {_properties.Length} ({string.Join(", ", _properties.Select(x => x.Name))})");
        }
    }

    /// <summary>
    ///     Writes a partition key back onto an entity, one component per declared property.
    /// </summary>
    /// <param name="entity">The entity to write to</param>
    /// <param name="partitionKey">The key to write, which has to be as deep as the declaration</param>
    public void SetValue(object entity, PartitionKeyValue partitionKey)
    {
        if (partitionKey.Count != _properties.Length)
        {
            throw Error.Unexpected(
                "PartitionKeyDepthMismatch",
                $"The partition key {partitionKey} carries {partitionKey.Count} component(s), but the model {entity.GetType().FullName} is partitioned by {_properties.Length}");
        }

        for (var index = 0; index < _properties.Length; index++)
        {
            _properties[index].SetValue(
                entity,
                partitionKey[index]);
        }
    }

    private static PartitionKeyDefinition ResolveHierarchical(
        Type entityType,
        List<(PropertyInfo Property, HierarchicalPartitionKeyAttribute Attribute)> hierarchicalProperties)
    {
        if (hierarchicalProperties.Count > PartitionKeyValue.MaxComponentCount)
        {
            throw Error.Unexpected(
                "PartitionKeyValueTooDeep",
                $"The model {entityType.FullName} declares {hierarchicalProperties.Count} hierarchical partition key components, but a partition key is limited to {PartitionKeyValue.MaxComponentCount}");
        }

        var ordered = hierarchicalProperties
            .OrderBy(x => x.Attribute.Order)
            .ToList();

        // the orders map onto the partition key paths the container was created with, so a gap or
        // a duplicate leaves it undecided which path a property belongs to
        for (var index = 0; index < ordered.Count; index++)
        {
            if (ordered[index].Attribute.Order != index)
            {
                throw Error.Unexpected(
                    "PartitionKeyDefinitionAmbiguous",
                    $"The hierarchical partition key orders of the model {entityType.FullName} have to start at 0 and be contiguous, but they are {string.Join(", ", ordered.Select(x => x.Attribute.Order))}");
            }

            EnsurePropertyIsAString(
                entityType,
                ordered[index].Property);
        }

        return new PartitionKeyDefinition(ordered.Select(x => x.Property).ToArray());
    }

    /// <summary>
    ///     The public instance properties of the type, with the base declaration of a property a
    ///     derived type hides with <c>new</c> dropped. Reflection reports both declarations, and
    ///     counting them twice would make an entity that shadows its partition key property look
    ///     like it declares two of them.
    /// </summary>
    private static List<PropertyInfo> GetDeclaredProperties(Type entityType)
    {
        return entityType.GetProperties()
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .Select(group => group.Aggregate((mostDerived, candidate) =>
                candidate.DeclaringType != null &&
                mostDerived.DeclaringType != null &&
                mostDerived.DeclaringType.IsAssignableFrom(candidate.DeclaringType)
                    ? candidate
                    : mostDerived))
            .ToList();
    }

    private static void EnsurePropertyIsAString(Type entityType, PropertyInfo property)
    {
        if (property.PropertyType != typeof(string))
        {
            throw Error.Unexpected(
                "PartitionKeyPropertyNotAString",
                $"The partition key property {entityType.FullName}.{property.Name} is of type {property.PropertyType.Name}, but a partition key property has to be a string");
        }
    }
}
