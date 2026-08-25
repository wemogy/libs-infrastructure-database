using System;

namespace Wemogy.Infrastructure.Database.Core.Attributes;

/// <summary>
///     Marks a property as one component of a hierarchical partition key. Apply it to every
///     property that forms the key, numbering them from the broadest component to the narrowest.
///     <para>
///         The orders have to start at 0 and be contiguous, and the resulting key must not be
///         deeper than <see cref="ValueObjects.PartitionKeyValue.MaxComponentCount"/>. The order
///         has to match the order of the partition key paths the container was created with.
///     </para>
///     <para>
///         An entity declares its partition key either with this attribute or with
///         <see cref="PartitionKeyAttribute"/>, never with both.
///     </para>
/// </summary>
/// <example>
///     <code>
///     public class UsageEvent : EntityBase
///     {
///         [HierarchicalPartitionKey(0)]
///         public string CustomerId { get; set; } = string.Empty;
///
///         [HierarchicalPartitionKey(1)]
///         public string MeterSlug { get; set; } = string.Empty;
///
///         [HierarchicalPartitionKey(2)]
///         public string TimeBucket { get; set; } = string.Empty;
///     }
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Property)]
public class HierarchicalPartitionKeyAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="HierarchicalPartitionKeyAttribute"/> class.
    /// </summary>
    /// <param name="order">The position of the property in the key, counted from the broadest component</param>
    public HierarchicalPartitionKeyAttribute(int order)
    {
        Order = order;
    }

    /// <summary>
    ///     The position of the property in the key, counted from the broadest component.
    /// </summary>
    public int Order { get; }
}
