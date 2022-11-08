using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Plugins.MultiTenantDatabase;

public partial class CosmosMultiTenantDatabaseRepositoryTests
{
    [Fact]
    public async Task QueryAsync_ShouldReturnAllItemsForeachDatabaseIfEmptyQueryParameters()
    {
        // Arrange
        await ResetAsync();
        var queryParameters = new QueryParameters();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await MicrosoftUserRepository.CreateAsync(user2);
        await AppleUserRepository.CreateAsync(user1);

        // Act
        var msQueriedUsers = await MicrosoftUserRepository.QueryAsync(queryParameters);
        var appleQueriedUsers = await AppleUserRepository.QueryAsync(queryParameters);

        msQueriedUsers.Should().HaveCount(2);
        msQueriedUsers.Should().ContainSingle(u => u.TenantId == user1.TenantId);
        msQueriedUsers.Should().ContainSingle(u => u.TenantId == user2.TenantId);
        AssertPartitionKeyPrefixIsRemoved(msQueriedUsers);

        appleQueriedUsers.Should().HaveCount(1);
        appleQueriedUsers.Should().ContainSingle(u => u.TenantId == user1.TenantId);
        AssertPartitionKeyPrefixIsRemoved(appleQueriedUsers);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnAllItemsForeachDatabaseIfPredicateIsSet()
    {
        // Arrange
        await ResetAsync();
        Expression<Func<User, bool>> predicate = _ => true;
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user1);
        await MicrosoftUserRepository.CreateAsync(user2);

        // Act
        var msQueriedUsers = await MicrosoftUserRepository.QueryAsync(predicate);
        var appleQueriedUsers = await AppleUserRepository.QueryAsync(predicate);

        // Assert
        msQueriedUsers.Should().HaveCount(2);
        msQueriedUsers.Should().ContainSingle(u => u.TenantId == user1.TenantId);
        msQueriedUsers.Should().ContainSingle(u => u.TenantId == user2.TenantId);
        AssertPartitionKeyPrefixIsRemoved(msQueriedUsers);

        appleQueriedUsers.Should().HaveCount(1);
        appleQueriedUsers.Should().ContainSingle(u => u.TenantId == user1.TenantId);
        AssertPartitionKeyPrefixIsRemoved(appleQueriedUsers);
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
