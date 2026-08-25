using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;

/// <summary>
///     A well-formed hierarchical key: three components, numbered from the broadest.
/// </summary>
public class HierarchicalKeyedEntity
{
    [Id]
    public string Id { get; set; } = string.Empty;

    [HierarchicalPartitionKey(0)]
    public string CustomerId { get; set; } = string.Empty;

    [HierarchicalPartitionKey(1)]
    public string MeterSlug { get; set; } = string.Empty;

    [HierarchicalPartitionKey(2)]
    public string TimeBucket { get; set; } = string.Empty;

    public int Amount { get; set; }
}
