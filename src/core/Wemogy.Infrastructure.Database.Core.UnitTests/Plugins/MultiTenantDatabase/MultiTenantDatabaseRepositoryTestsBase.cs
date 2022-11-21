using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public abstract partial class MultiTenantDatabaseRepositoryTestsBase : RepositoryTestBase
{
    protected MultiTenantDatabaseRepositoryTestsBase(
        Func<IDatabaseRepository<User>> multiTenantUserRepositoryFactory1,
        Func<IDatabaseRepository<User>> multiTenantUserRepositoryFactory2)
        : base(multiTenantUserRepositoryFactory1)
    {
        AppleUserRepository = multiTenantUserRepositoryFactory2();
        MultiTenantUserRepository2Factory = multiTenantUserRepositoryFactory2;
    }

    protected IDatabaseRepository<User> AppleUserRepository { get; }
    private Func<IDatabaseRepository<User>> MultiTenantUserRepository2Factory { get; }

    protected override async Task ResetAsync()
    {
        await AppleUserRepository.DeleteAsync(x => true);
        await base.ResetAsync();
    }

    private void AssertPartitionKeyPrefixIsRemoved(User user)
    {
        user.TenantId.Should().NotStartWith(new MicrosoftTenantProvider().GetTenantId());
        user.TenantId.Should().NotStartWith(new AppleTenantProvider().GetTenantId());
    }

    private void AssertExceptionMessageDoesNotContainPrefix(Exception? exception)
    {
        exception?.Message.Should().NotContain(new MicrosoftTenantProvider().GetTenantId());
        exception?.Message.Should().NotContain(new AppleTenantProvider().GetTenantId());
    }

    private void AssertPartitionKeyPrefixIsRemoved(IEnumerable<User> actualUsers)
    {
        var users = actualUsers.ToList();
        users.Should()
            .AllSatisfy(u => u.TenantId.Should().NotStartWith(new MicrosoftTenantProvider().GetTenantId()));
        users.Should()
            .AllSatisfy(u => u.TenantId.Should().NotStartWith(new AppleTenantProvider().GetTenantId()));
    }
}
