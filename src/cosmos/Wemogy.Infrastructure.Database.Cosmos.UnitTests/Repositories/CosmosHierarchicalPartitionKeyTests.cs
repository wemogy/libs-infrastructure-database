using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;
using Wemogy.Infrastructure.Database.Cosmos.Factories;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Repositories;

[Collection("Sequential")]
public class CosmosHierarchicalPartitionKeyTests : HierarchicalPartitionKeyTestBase
{
    public CosmosHierarchicalPartitionKeyTests()
        : base(() => CosmosDatabaseRepositoryFactory.CreateInstance<IUsageEventRepository>(
            TestingConstants.ConnectionString,
            TestingConstants.DatabaseName,
            true,
            true))
    {
    }
}
