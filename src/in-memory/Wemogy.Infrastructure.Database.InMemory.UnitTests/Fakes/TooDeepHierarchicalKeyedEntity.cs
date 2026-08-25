using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;

/// <summary>
///     Declares more components than Cosmos DB supports.
/// </summary>
public class TooDeepHierarchicalKeyedEntity
{
    [Id]
    public string Id { get; set; } = string.Empty;

    [HierarchicalPartitionKey(0)]
    public string First { get; set; } = string.Empty;

    [HierarchicalPartitionKey(1)]
    public string Second { get; set; } = string.Empty;

    [HierarchicalPartitionKey(2)]
    public string Third { get; set; } = string.Empty;

    [HierarchicalPartitionKey(3)]
    public string Fourth { get; set; } = string.Empty;
}
