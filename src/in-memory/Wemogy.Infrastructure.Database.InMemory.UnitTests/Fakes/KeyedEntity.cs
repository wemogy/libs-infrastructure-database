using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;

/// <summary>
///     Entity that deliberately does not derive from <c>EntityBase</c>: its id property is not
///     called "Id" and it does not opt into optimistic concurrency. Used to verify that the client
///     resolves the id through the <see cref="IdAttribute"/> instead of assuming a property name,
///     and that entities without an <see cref="ETagAttribute"/> stay untouched.
/// </summary>
public class KeyedEntity
{
    [Id]
    public string Key { get; set; } = string.Empty;

    [PartitionKey]
    public string Tenant { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Rank { get; set; }
}
