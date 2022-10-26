using System;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public abstract partial class RepositoryTestBase : IDisposable
{
    protected RepositoryTestBase(Func<IUserRepository> userRepositoryFactory)
    {
        UserRepository = userRepositoryFactory();
        UserRepositoryFactory = userRepositoryFactory;
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = null;
    }

    private IUserRepository UserRepository { get; }
    private Func<IUserRepository> UserRepositoryFactory { get; }

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
