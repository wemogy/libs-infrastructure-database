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

    public double Rate { get; set; }

    public PatchTargetInner Inner { get; set; } = new PatchTargetInner();

    public int DoubledCounter => Counter * 2;
}
