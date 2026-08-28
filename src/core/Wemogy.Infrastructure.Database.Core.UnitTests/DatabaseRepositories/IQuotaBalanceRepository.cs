using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;

/// <summary>
///     Mapped to the same container as <see cref="IUsageEventRepository"/>, so a Cosmos partition
///     batch created from either repository can write both entity types into one partition of one
///     container. The in-memory provider keeps a store per type and ignores the container, so the
///     mapping only matters for Cosmos.
/// </summary>
[RepositoryOptions(collectionName: "usageevents")]
public interface IQuotaBalanceRepository : IDatabaseRepository<QuotaBalance>
{
}
