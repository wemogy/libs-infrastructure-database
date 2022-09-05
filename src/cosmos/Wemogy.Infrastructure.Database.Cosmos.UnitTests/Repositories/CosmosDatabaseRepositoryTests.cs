using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;
using Wemogy.Infrastructure.Database.Cosmos.Factories;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Repositories;

public class CosmosDatabaseRepositoryTests : RepositoryTestBase
{
    public CosmosDatabaseRepositoryTests()
        : base(() => CosmosDatabaseRepositoryFactory.CreateInstance<IUserRepository>(
            TestingConstants.ConnectionString,
            TestingConstants.DatabaseName,
            true))
    {
    }
}