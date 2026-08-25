using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

/// <summary>
///     The hierarchical counterpart of <see cref="PatchTarget"/>: only there to check that a patch
///     path is rejected for every component of the key, not just the broadest one. It is not
///     stored anywhere, the path resolution is what is under test.
/// </summary>
public class HierarchicallyPartitionedPatchTarget
{
    [Id]
    public string Id { get; set; } = string.Empty;

    [HierarchicalPartitionKey(0)]
    public string CustomerId { get; set; } = string.Empty;

    [HierarchicalPartitionKey(1)]
    public string MeterSlug { get; set; } = string.Empty;

    [HierarchicalPartitionKey(2)]
    public string TimeBucket { get; set; } = string.Empty;

    public long Quantity { get; set; }
}
