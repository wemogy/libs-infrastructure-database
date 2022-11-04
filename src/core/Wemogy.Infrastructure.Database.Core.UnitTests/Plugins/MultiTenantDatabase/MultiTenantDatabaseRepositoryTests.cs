using System;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public abstract partial class MultiTenantDatabaseRepositoryTests : IDisposable
{
    private readonly MultiTenantDatabaseRepository<User> _multiTenantRepo;
    private readonly TestDatabaseTenantProvider _testDatabaseTenantProvider;

    protected MultiTenantDatabaseRepositoryTests(Func<IUserRepository> userRepositoryFactory)
    {
        UserRepository = userRepositoryFactory();
        UserRepositoryFactory = userRepositoryFactory;
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = null;
        _testDatabaseTenantProvider = new TestDatabaseTenantProvider();
        _multiTenantRepo = new MultiTenantDatabaseRepository<User>(
            UserRepository,
            _testDatabaseTenantProvider);
    }

    private IUserRepository UserRepository { get; }
    private Func<IUserRepository> UserRepositoryFactory { get; }

    public void Dispose()
    {
        // Cleanup
        UserRepository.DeleteAsync(x => true).Wait();
    }
}
