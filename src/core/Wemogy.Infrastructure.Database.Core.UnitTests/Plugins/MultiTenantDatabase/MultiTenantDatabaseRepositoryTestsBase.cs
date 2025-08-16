using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public abstract partial class MultiTenantDatabaseRepositoryTestsBase : RepositoryTestBase
{
    protected MultiTenantDatabaseRepositoryTestsBase(
        Func<IDatabaseRepository<User>> multiTenantUserRepositoryFactory1,
        Func<IDatabaseRepository<User>> multiTenantUserRepositoryFactory2,
        Func<IDatabaseRepository<DataCenter>> dataCenterRepositoryFactory)
        : base(multiTenantUserRepositoryFactory1, dataCenterRepositoryFactory)
    {
        AppleUserRepository = multiTenantUserRepositoryFactory2();
    }

    protected IDatabaseRepository<User> AppleUserRepository { get; }

    protected override async Task ResetAsync()
    {
        await AppleUserRepository.DeleteAsync(x => true);
        await base.ResetAsync();
    }

    private void AssertPartitionKeyPrefixIsRemoved(User user)
    {
        user.TenantId.ShouldNotStartWith(new MicrosoftTenantProvider().GetTenantId());
        user.TenantId.ShouldNotStartWith(new AppleTenantProvider().GetTenantId());
    }

    private void AssertExceptionMessageDoesNotContainPrefix(Exception? exception)
    {
        exception?.Message.ShouldNotContain(new MicrosoftTenantProvider().GetTenantId());
        exception?.Message.ShouldNotContain(new AppleTenantProvider().GetTenantId());
    }

    private void AssertPartitionKeyPrefixIsRemoved(IEnumerable<User> actualUsers)
    {
        var users = actualUsers.ToList();
        users.ShouldAllBe(u => !u.TenantId.StartsWith(new MicrosoftTenantProvider().GetTenantId()));
        users.ShouldAllBe(u => !u.TenantId.StartsWith(new AppleTenantProvider().GetTenantId()));
    }
}
