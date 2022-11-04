using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;
using Wemogy.Infrastructure.Database.Cosmos.Factories;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Plugins.MultiTenantDatabase;

public class CosmosMultiTenantDatabaseRepositoryTests : MultiTenantDatabaseRepositoryTests
{
    public CosmosMultiTenantDatabaseRepositoryTests()
        : base(
            () => CosmosDatabaseRepositoryFactory.CreateInstance<IUserRepository>(
                TestingConstants.ConnectionString,
                TestingConstants.DatabaseName,
                true))
    {
    }
}
