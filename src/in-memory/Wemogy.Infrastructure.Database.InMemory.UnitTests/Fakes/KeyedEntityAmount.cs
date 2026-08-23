namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;

/// <summary>
///     Nested value-typed member of <see cref="KeyedEntity"/>. Reflection hands out a boxed copy of
///     a struct, so a patch of a member of one is where a write can get lost.
/// </summary>
public struct KeyedEntityAmount
{
    public long Minor { get; set; }
}
