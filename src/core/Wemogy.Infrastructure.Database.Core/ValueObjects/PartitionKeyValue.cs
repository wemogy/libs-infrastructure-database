using System;
using System.Collections.Generic;
using System.Linq;
using Wemogy.Core.Errors;

namespace Wemogy.Infrastructure.Database.Core.ValueObjects;

/// <summary>
///     The value a document is partitioned by: either a single value, or up to three values that
///     form a hierarchy, ordered from the broadest to the narrowest.
///     <para>
///         A hierarchical key lets a store spread one logical partition over several physical ones
///         while an operation that names the whole key - a point read, a patch, a transactional
///         batch - still addresses exactly one of them. That is what lifts the per-partition size
///         and throughput ceilings a single-value key runs into.
///     </para>
///     <para>
///         A single value converts implicitly, so <c>GetAsync(id, "acme")</c> keeps meaning what
///         it always did.
///     </para>
/// </summary>
public sealed class PartitionKeyValue : IEquatable<PartitionKeyValue>
{
    /// <summary>
    ///     Cosmos DB caps a hierarchical partition key at three components. The cap is enforced
    ///     for every provider, so a key that works against the in-memory provider in a test is
    ///     not too deep for Cosmos DB in production.
    /// </summary>
    public const int MaxComponentCount = 3;

    /// <summary>
    ///     How the components are joined when the key is written into a message. For display
    ///     only - no provider addresses a partition by this string.
    /// </summary>
    private const string ComponentSeparator = "/";

    private readonly string[] _components;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PartitionKeyValue"/> class with a single
    ///     value.
    /// </summary>
    /// <param name="partitionKey">The value the document is partitioned by</param>
    public PartitionKeyValue(string partitionKey)
        : this(new[] { partitionKey })
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PartitionKeyValue"/> class with a
    ///     hierarchy of two values, ordered from the broadest to the narrowest.
    /// </summary>
    /// <param name="first">The first, broadest component of the key</param>
    /// <param name="second">The second component of the key</param>
    public PartitionKeyValue(string first, string second)
        : this(new[] { first, second })
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PartitionKeyValue"/> class with a
    ///     hierarchy of three values, ordered from the broadest to the narrowest.
    /// </summary>
    /// <param name="first">The first, broadest component of the key</param>
    /// <param name="second">The second component of the key</param>
    /// <param name="third">The third, narrowest component of the key</param>
    public PartitionKeyValue(string first, string second, string third)
        : this(new[] { first, second, third })
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PartitionKeyValue"/> class from a list of
    ///     components, ordered from the broadest to the narrowest. For callers that build a key
    ///     whose depth is only known at runtime.
    /// </summary>
    /// <param name="components">Between one and <see cref="MaxComponentCount"/> components</param>
    public PartitionKeyValue(IReadOnlyList<string> components)
    {
        if (components == null)
        {
            throw Error.Unexpected(
                "PartitionKeyValueNull",
                "The partition key can not be null");
        }

        if (components.Count == 0)
        {
            throw Error.Unexpected(
                "PartitionKeyValueEmpty",
                "A partition key has to carry at least one component");
        }

        if (components.Count > MaxComponentCount)
        {
            throw Error.Unexpected(
                "PartitionKeyValueTooDeep",
                $"A partition key is limited to {MaxComponentCount} components, but {components.Count} were given");
        }

        // a null component is rejected here rather than where it is written, so the call site that
        // built the key is still on the stack
        if (components.Any(component => component == null))
        {
            throw Error.Unexpected(
                "PartitionKeyValueNull",
                "The partition key can not be null");
        }

        _components = components.ToArray();
    }

    /// <summary>
    ///     The components of the key, ordered from the broadest to the narrowest. Always holds at
    ///     least one entry.
    /// </summary>
    public IReadOnlyList<string> Components => _components;

    /// <summary>
    ///     The number of components the key is built from.
    /// </summary>
    public int Count => _components.Length;

    /// <summary>
    ///     Gets a value indicating whether the key is built from more than one component.
    /// </summary>
    public bool IsHierarchical => _components.Length > 1;

    /// <summary>
    ///     The component at the given position, counted from the broadest.
    /// </summary>
    /// <param name="index">The position of the component</param>
    public string this[int index] => _components[index];

    public static implicit operator PartitionKeyValue(string partitionKey)
    {
        return new PartitionKeyValue(partitionKey);
    }

    public static bool operator ==(PartitionKeyValue? left, PartitionKeyValue? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(PartitionKeyValue? left, PartitionKeyValue? right)
    {
        return !Equals(left, right);
    }

    /// <summary>
    ///     Returns a copy of this key with the component at the given position replaced. Used by
    ///     the multi-tenant plugin, which composes its prefix into the broadest component only.
    /// </summary>
    /// <param name="index">The position of the component to replace</param>
    /// <param name="component">The value to put in its place</param>
    /// <returns>A new key, this one is left as it is</returns>
    public PartitionKeyValue WithComponent(int index, string component)
    {
        var components = _components.ToArray();
        components[index] = component;
        return new PartitionKeyValue(components);
    }

    public bool Equals(PartitionKeyValue? other)
    {
        if (other is null)
        {
            return false;
        }

        return ReferenceEquals(this, other) || _components.SequenceEqual(other._components, StringComparer.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as PartitionKeyValue);
    }

    public override int GetHashCode()
    {
        var hashCode = default(HashCode);
        foreach (var component in _components)
        {
            hashCode.Add(component, StringComparer.Ordinal);
        }

        return hashCode.ToHashCode();
    }

    /// <summary>
    ///     The key as it appears in a message. A single-value key is its value, so the wording of
    ///     an error does not change for the entities that have always had one.
    /// </summary>
    public override string ToString()
    {
        return string.Join(ComponentSeparator, _components);
    }
}
