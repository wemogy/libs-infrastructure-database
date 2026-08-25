using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;

/// <summary>
///     Declares its key with both attributes, which leaves it undecided what the key is.
/// </summary>
public class AmbiguouslyKeyedEntity
{
    [Id]
    public string Id { get; set; } = string.Empty;

    [PartitionKey]
    public string Tenant { get; set; } = string.Empty;

    [HierarchicalPartitionKey(0)]
    public string CustomerId { get; set; } = string.Empty;
}
