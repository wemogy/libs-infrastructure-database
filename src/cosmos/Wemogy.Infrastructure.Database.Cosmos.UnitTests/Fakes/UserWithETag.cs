using Newtonsoft.Json;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Fakes;

/// <summary>
///     Cosmos fake that opts into optimistic concurrency via the <see cref="ETagAttribute"/>.
/// </summary>
public class UserWithETag : EntityBase
{
    [PartitionKey]
    public string TenantId { get; set; } = string.Empty;

    public string Firstname { get; set; } = string.Empty;

    public string Lastname { get; set; } = string.Empty;

    /// <summary>
    ///     Serialized under a name carrying a slash, which a JSON pointer has to escape as ~1 -
    ///     without that, a patch of it would address a nested path instead of this field.
    /// </summary>
    [JsonProperty("limits/daily")]
    public long DailyLimit { get; set; }
}
