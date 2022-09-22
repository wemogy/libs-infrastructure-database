using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.Repositories.AnimalEntity;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.TestingData.Models;
using Wemogy.Infrastructure.Database.InMemory.Setup;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Plugins.ComposedPrimaryKey;

[Collection("Sequential")]
public class AnimalComposedPrimaryKeyRepositoryTests : ComposedPrimaryKeyDatabaseRepositoryTestBase
{
    public AnimalComposedPrimaryKeyRepositoryTests()
        : base(
            serviceCollection => serviceCollection
                .AddInMemoryDatabaseClient()
                .AddRepository<IAnimalRepository, StringPrefixComposedPrimaryKeyBuilder>())
    {
    }
}
