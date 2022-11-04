using System;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public abstract partial class MultiTenantDatabaseRepositoryTests : IDisposable
{
    protected MultiTenantDatabaseRepositoryTests(Func<IUserRepository> userRepositoryFactory)
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
}
