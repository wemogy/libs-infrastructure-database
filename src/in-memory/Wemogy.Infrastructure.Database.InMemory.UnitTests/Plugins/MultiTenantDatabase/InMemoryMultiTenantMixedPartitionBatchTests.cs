using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;
using Wemogy.Infrastructure.Database.InMemory.Factories;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Plugins.MultiTenantDatabase;

/// <summary>
///     The same behaviour, seen through the multi-tenant plugin: it composes its prefix into the
///     broadest component of the key of every operation, whatever the type, so a batch still writes
///     into one tenant-scoped partition and the hierarchy below the prefix survives unchanged.
/// </summary>
[Collection("Sequential")]
public class InMemoryMultiTenantMixedPartitionBatchTests : MixedPartitionBatchTestBase
{
    public InMemoryMultiTenantMixedPartitionBatchTests()
        : base(
            () => new MultiTenantDatabaseRepository<UsageEvent>(
                InMemoryDatabaseRepositoryFactory.CreateInstance<IUsageEventRepository>(),
                new MicrosoftTenantProvider()),
            () => new MultiTenantDatabaseRepository<QuotaBalance>(
                InMemoryDatabaseRepositoryFactory.CreateInstance<IQuotaBalanceRepository>(),
                new MicrosoftTenantProvider()))
    {
    }
}
