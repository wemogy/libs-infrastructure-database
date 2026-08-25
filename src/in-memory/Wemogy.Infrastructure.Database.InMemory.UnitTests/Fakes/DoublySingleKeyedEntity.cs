using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;

/// <summary>
///     Carries the single-value attribute twice, so which property the documents are partitioned
///     by would come down to the order reflection happens to report the properties in.
/// </summary>
public class DoublySingleKeyedEntity
{
    [Id]
    public string Id { get; set; } = string.Empty;

    [PartitionKey]
    public string Tenant { get; set; } = string.Empty;

    [PartitionKey]
    public string Region { get; set; } = string.Empty;
}
