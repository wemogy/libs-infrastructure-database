using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

/// <summary>
///     The nested object of <see cref="PatchTarget"/>.
/// </summary>
public class PatchTargetInner
{
    public long Value { get; set; }

    /// <summary>
    ///     A fixed-point member behind a nested path, so the tests cover that the attribute is
    ///     found on more than the outermost member.
    /// </summary>
    [FixedPoint(Scale = 4)]
    public decimal Amount { get; set; }
}
