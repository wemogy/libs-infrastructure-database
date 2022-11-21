using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;
using Wemogy.Infrastructure.Database.InMemory.Factories;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Repositories;

[Collection("Sequential")]
public class InMemoryDatabaseRepositoryTests : RepositoryTestBase
{
    public InMemoryDatabaseRepositoryTests()
        : base(InMemoryDatabaseRepositoryFactory.CreateInstance<IUserRepository>)
    {
    }
}
