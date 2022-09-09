using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.Repositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.TestingData.Models;
using Wemogy.Infrastructure.Database.InMemory.Setup;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Plugins.ComposedPrimaryKey;

public class ComposedPrimaryKeyRepositoryTests : ComposedPrimaryKeyDatabaseRepositoryTestBase
{
    public ComposedPrimaryKeyRepositoryTests()
        : base(
            serviceCollection => serviceCollection
                .AddInMemoryDatabaseClient()
                .AddRepository<IUserRepository, PrefixComposedPrimaryKeyBuilder>())
    {
    }
}
