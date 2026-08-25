using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;

/// <summary>
///     Uses the same order twice, so the two properties cannot be told apart.
/// </summary>
public class DuplicateOrderHierarchicalKeyedEntity
{
    [Id]
    public string Id { get; set; } = string.Empty;

    [HierarchicalPartitionKey(0)]
    public string CustomerId { get; set; } = string.Empty;

    [HierarchicalPartitionKey(0)]
    public string MeterSlug { get; set; } = string.Empty;
}
