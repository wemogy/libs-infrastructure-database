using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Fakes;

/// <summary>
/// Cosmos-only fake that opts into optimistic concurrency via <see cref="ETagAttribute"/>.
/// Kept out of the shared Core.UnitTests fakes on purpose: the eTag value is
/// path-dependent (point operations populate it, queries do not), which breaks
/// the cross-path BeEquivalentTo assertions of the shared repository test suite.
/// </summary>
public class UserWithETag : EntityBase
{
    [PartitionKey]
    public string TenantId { get; set; } = string.Empty;

    public string Firstname { get; set; } = string.Empty;

    public string Lastname { get; set; } = string.Empty;

    [ETag]
    public string? ETag { get; set; }
}
