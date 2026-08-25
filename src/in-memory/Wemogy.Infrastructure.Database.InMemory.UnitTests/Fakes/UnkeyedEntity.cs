using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;

/// <summary>
///     Declares no partition key at all.
/// </summary>
public class UnkeyedEntity
{
    [Id]
    public string Id { get; set; } = string.Empty;
}
