using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Fakes;

/// <summary>
///     Cosmos fake that relies on the <see cref="ETagAttribute"/> carried by
///     <see cref="EntityBase.ETag"/> to opt into optimistic concurrency.
/// </summary>
public class UserWithETag : EntityBase
{
    [PartitionKey]
    public string TenantId { get; set; } = string.Empty;

    public string Firstname { get; set; } = string.Empty;

    public string Lastname { get; set; } = string.Empty;
}
