using System;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public abstract partial class RepositoryTestBase : IDisposable
{
    protected IUserRepository UserRepository { get; }
    protected Func<IUserRepository> UserRepositoryFactory { get; }

    protected RepositoryTestBase(Func<IUserRepository> userRepositoryFactory)
    {
        UserRepository = userRepositoryFactory();
        UserRepositoryFactory = userRepositoryFactory;
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = null;
    }

    protected async Task ResetAsync()
    {
        await UserRepository.DeleteAsync(x => true);
    }

    public void Dispose()
    {
        // Cleanup
        UserRepository.DeleteAsync(x => true).Wait();
    }
}
