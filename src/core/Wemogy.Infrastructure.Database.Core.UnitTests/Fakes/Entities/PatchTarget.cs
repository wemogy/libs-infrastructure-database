using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

/// <summary>
///     Covers the shapes a patch path can have without needing a container: a nested object, a
///     counter that is narrower than the increment overloads, and a computed member. It is not
///     stored anywhere, the path resolution is what is under test.
/// </summary>
public class PatchTarget
{
    [Id]
    public string Id { get; set; } = string.Empty;

    [PartitionKey]
    public string PartitionKey { get; set; } = string.Empty;

    [ETag]
    public string? ETag { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Counter { get; set; }

    public decimal Money { get; set; }

    /// <summary>
    ///     A decimal that opts into the fixed-point encoding, which is what makes an exact
    ///     increment of it possible. <see cref="Money"/> is deliberately left without it.
    /// </summary>
    [FixedPoint(Scale = 6)]
    public decimal Balance { get; set; }

    /// <summary>
    ///     A nullable fixed-point member at a second scale.
    /// </summary>
    [FixedPoint(Scale = 2)]
    public decimal? Discount { get; set; }

    public double Rate { get; set; }

    public PatchTargetInner Inner { get; set; } = new PatchTargetInner();

    public int DoubledCounter => Counter * 2;
}
