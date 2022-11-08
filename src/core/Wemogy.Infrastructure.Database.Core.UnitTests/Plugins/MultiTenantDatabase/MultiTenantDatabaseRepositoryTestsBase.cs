using System;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public abstract class MultiTenantDatabaseRepositoryTestsBase : RepositoryTestBase
{
    protected MultiTenantDatabaseRepositoryTestsBase(Func<IDatabaseRepository<User>> multiTenantUserRepositoryFactory1,
        Func<IDatabaseRepository<User>> multiTenantUserRepositoryFactory2)
        : base(multiTenantUserRepositoryFactory1)
    {
        AppleUserRepository = multiTenantUserRepositoryFactory2();
        MultiTenantUserRepository2Factory = multiTenantUserRepositoryFactory2;
    }

    protected IDatabaseRepository<User> AppleUserRepository { get; }
    private Func<IDatabaseRepository<User>> MultiTenantUserRepository2Factory { get; }

    public override void Dispose()
    {
        // Cleanup
        AppleUserRepository.DeleteAsync(x => true).Wait();
        base.Dispose();
    }

    protected override async Task ResetAsync()
    {
        await AppleUserRepository.DeleteAsync(x => true);
        await base.ResetAsync();
    }

}
