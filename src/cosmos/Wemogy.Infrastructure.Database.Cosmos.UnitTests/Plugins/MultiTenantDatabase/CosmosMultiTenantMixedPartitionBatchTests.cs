using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Factories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;
using Wemogy.Infrastructure.Database.Cosmos.Factories;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Plugins.MultiTenantDatabase;

/// <summary>
///     The same behaviour, seen through the multi-tenant plugin: it composes its prefix into the
///     broadest component of the key of every operation, whatever the type, so a batch still writes
///     into one tenant-scoped partition and the hierarchy below the prefix survives unchanged.
/// </summary>
[Collection("Sequential")]
public class CosmosMultiTenantMixedPartitionBatchTests : MixedPartitionBatchTestBase
{
    public CosmosMultiTenantMixedPartitionBatchTests()
        : base(
            () => new MultiTenantDatabaseRepositoryFactory(
                new CosmosDatabaseClientFactory(
                    TestingConstants.ConnectionString,
                    TestingConstants.DatabaseName,
                    true),
                new MicrosoftTenantProvider()).CreateInstance<IUsageEventRepository>(),
            () => new MultiTenantDatabaseRepositoryFactory(
                new CosmosDatabaseClientFactory(
                    TestingConstants.ConnectionString,
                    TestingConstants.DatabaseName,
                    true),
                new MicrosoftTenantProvider()).CreateInstance<IQuotaBalanceRepository>())
    {
    }
}
