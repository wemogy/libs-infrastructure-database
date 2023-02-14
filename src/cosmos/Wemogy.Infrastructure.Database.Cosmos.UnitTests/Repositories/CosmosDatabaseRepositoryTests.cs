using System.Collections.Generic;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;
using Wemogy.Infrastructure.Database.Cosmos.Factories;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Repositories;

[Collection("Sequential")]
public class CosmosDatabaseRepositoryTests : RepositoryTestBase
{
    public CosmosDatabaseRepositoryTests()
        : base(() => CosmosDatabaseRepositoryFactory.CreateInstance<IUserRepository>(
            TestingConstants.ConnectionString,
            TestingConstants.DatabaseName,
            new List<string> { "animals", "files", "users" },
            true,
            true))
    {
    }
}
