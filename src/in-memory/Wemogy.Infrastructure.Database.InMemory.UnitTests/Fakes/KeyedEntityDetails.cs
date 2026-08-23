namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;

/// <summary>
///     Nested reference-typed member of <see cref="KeyedEntity"/>, so a patch can be pointed at a
///     nested path and at a value the caller keeps holding on to.
/// </summary>
public class KeyedEntityDetails
{
    public string Note { get; set; } = string.Empty;
}
