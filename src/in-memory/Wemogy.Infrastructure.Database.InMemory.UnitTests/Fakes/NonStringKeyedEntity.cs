using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;

/// <summary>
///     Partitions by a property that is not a string, which no provider can address a partition by.
/// </summary>
public class NonStringKeyedEntity
{
    [Id]
    public string Id { get; set; } = string.Empty;

    [PartitionKey]
    public int Tenant { get; set; }
}
