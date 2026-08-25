using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;

/// <summary>
///     Skips an order, so it is undecided which container path the second property belongs to.
/// </summary>
public class GappedHierarchicalKeyedEntity
{
    [Id]
    public string Id { get; set; } = string.Empty;

    [HierarchicalPartitionKey(0)]
    public string CustomerId { get; set; } = string.Empty;

    [HierarchicalPartitionKey(2)]
    public string TimeBucket { get; set; } = string.Empty;
}
