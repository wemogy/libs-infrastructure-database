using System;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public abstract partial class RepositoryTestBase : IDisposable
{
    protected RepositoryTestBase(Func<IDatabaseRepository<User>> userRepositoryFactory)
    {
        UserRepository = userRepositoryFactory();
        UserRepositoryFactory = userRepositoryFactory;
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = null;
    }

    protected IDatabaseRepository<User> UserRepository { get; set; }
    private Func<IDatabaseRepository<User>> UserRepositoryFactory { get; }

    public void Dispose()
    {
        // Cleanup
        UserRepository.DeleteAsync(x => true).Wait();
    }

    private async Task ResetAsync()
    {
        await UserRepository.DeleteAsync(x => true);
    }
}
