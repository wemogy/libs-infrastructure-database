using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;
using Wemogy.Infrastructure.Database.InMemory.Factories;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Repositories;

public class InMemoryDatabaseRepositoryTests : RepositoryTestBase
{
    public InMemoryDatabaseRepositoryTests()
        : base(InMemoryDatabaseRepositoryFactory.CreateInstance<IUserRepository>)
    {
    }
}
