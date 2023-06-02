using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;
using Wemogy.Infrastructure.Database.Mongo.Factories;
using Wemogy.Infrastructure.Database.Mongo.UnitTests.Constants;
using Xunit;

namespace Wemogy.Infrastructure.Database.Mongo.UnitTests.Repositories;

[Collection("Sequential")]
public class MongoDatabaseRepositoryTests : RepositoryTestBase
{
    public MongoDatabaseRepositoryTests()
        : base(
            () => MongoDatabaseRepositoryFactory.CreateInstance<IUserRepository>(
                TestingConstants.ConnectionString,
                TestingConstants.DatabaseName,
                true),
            () => MongoDatabaseRepositoryFactory.CreateInstance<IDataCenterRepository>(
                TestingConstants.ConnectionString,
                TestingConstants.DatabaseName,
                true))
    {
    }
}
