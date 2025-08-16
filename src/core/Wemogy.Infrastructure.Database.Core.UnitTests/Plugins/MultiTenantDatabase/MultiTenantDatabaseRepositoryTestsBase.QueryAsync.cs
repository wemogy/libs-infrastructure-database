using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public partial class MultiTenantDatabaseRepositoryTestsBase
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

        msQueriedUsers.Count.ShouldBe(2);
        msQueriedUsers.ShouldContain(u => u.TenantId == user1.TenantId, 1);
        msQueriedUsers.ShouldContain(u => u.TenantId == user2.TenantId, 1);
        AssertPartitionKeyPrefixIsRemoved(msQueriedUsers);

        appleQueriedUsers.ShouldHaveSingleItem();
        appleQueriedUsers.ShouldContain(u => u.TenantId == user1.TenantId, 1);
        AssertPartitionKeyPrefixIsRemoved(appleQueriedUsers);
    }

    [Fact]
    public async Task QueryAsync_ShouldIncludeFiltersInQueryParameters()
    {
        // Arrange
        await ResetAsync();
        var queryParameters = new QueryParameters();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await MicrosoftUserRepository.CreateAsync(user2);
        await AppleUserRepository.CreateAsync(user1);

        queryParameters.Filters.Add(
            new QueryFilter
            {
                Comparator = Comparator.Equals,
                Property = nameof(User.Firstname).ToCamelCase(),
                Value = user1.Firstname.ToJson()
            });

        // Act
        var msQueriedUsers = await MicrosoftUserRepository.QueryAsync(queryParameters);
        var appleQueriedUsers = await AppleUserRepository.QueryAsync(queryParameters);

        // Assert
        msQueriedUsers.ShouldHaveSingleItem();
        msQueriedUsers.ShouldContain(u => u.Firstname == user1.Firstname, 1);
        AssertPartitionKeyPrefixIsRemoved(msQueriedUsers);

        appleQueriedUsers.ShouldHaveSingleItem();
        appleQueriedUsers.ShouldContain(u => u.Firstname == user1.Firstname, 1);
        AssertPartitionKeyPrefixIsRemoved(appleQueriedUsers);
    }

    [Fact]
    public async Task QueryAsync_ShouldIncludeFiltersInQueryParametersAlsoForPartitionKeys()
    {
        // Arrange
        await ResetAsync();
        var queryParameters = new QueryParameters();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await MicrosoftUserRepository.CreateAsync(user2);
        await AppleUserRepository.CreateAsync(user1);

        queryParameters.Filters.Add(
            new QueryFilter
            {
                Comparator = Comparator.Equals,
                Property = nameof(User.Firstname).ToCamelCase(),
                Value = user1.Firstname.ToJson()
            });

        queryParameters.Filters.Add(
            new QueryFilter
            {
                Comparator = Comparator.Equals,
                Property = nameof(User.TenantId).ToCamelCase(),
                Value = user1.TenantId.ToJson()
            });

        // Act
        var msQueriedUsers = await MicrosoftUserRepository.QueryAsync(queryParameters);
        var appleQueriedUsers = await AppleUserRepository.QueryAsync(queryParameters);

        // Assert
        msQueriedUsers.ShouldHaveSingleItem();
        msQueriedUsers.ShouldContain(u => u.Firstname == user1.Firstname, 1);
        AssertPartitionKeyPrefixIsRemoved(msQueriedUsers);

        appleQueriedUsers.ShouldHaveSingleItem();
        appleQueriedUsers.ShouldContain(u => u.Firstname == user1.Firstname, 1);
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
        msQueriedUsers.Count.ShouldBe(2);
        msQueriedUsers.ShouldContain(u => u.TenantId == user1.TenantId, 1);
        msQueriedUsers.ShouldContain(u => u.TenantId == user2.TenantId, 1);
        AssertPartitionKeyPrefixIsRemoved(msQueriedUsers);

        appleQueriedUsers.ShouldHaveSingleItem();
        appleQueriedUsers.ShouldContain(u => u.TenantId == user1.TenantId, 1);
        AssertPartitionKeyPrefixIsRemoved(appleQueriedUsers);
    }

    [Fact]
    public async Task QueryAsync_ShouldWorkWithPredicate()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user1);
        await MicrosoftUserRepository.CreateAsync(user2);

        // Act
        var msQueriedUsers = await MicrosoftUserRepository.QueryAsync(x => !x.IsDeleted);
        var appleQueriedUsers = await AppleUserRepository.QueryAsync(x => !x.IsDeleted);

        // Assert
        msQueriedUsers.Count.ShouldBe(2);
        msQueriedUsers.ShouldContain(u => u.TenantId == user1.TenantId, 1);
        msQueriedUsers.ShouldContain(u => u.TenantId == user2.TenantId, 1);
        AssertPartitionKeyPrefixIsRemoved(msQueriedUsers);

        appleQueriedUsers.ShouldHaveSingleItem();
        appleQueriedUsers.ShouldContain(u => u.TenantId == user1.TenantId, 1);
        AssertPartitionKeyPrefixIsRemoved(appleQueriedUsers);
    }

    [Fact]
    public async Task QueryAsync_ShouldWorkWithPredicateForPartitionKey()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user1);
        await MicrosoftUserRepository.CreateAsync(user2);

        // Act
        var msQueriedUsers = await MicrosoftUserRepository.QueryAsync(x => x.TenantId == user1.TenantId || x.TenantId == user2.TenantId);
        var appleQueriedUsers = await AppleUserRepository.QueryAsync(x => x.TenantId == user1.TenantId);

        // Assert
        msQueriedUsers.Count.ShouldBe(2);
        msQueriedUsers.ShouldContain(u => u.TenantId == user1.TenantId, 1);
        msQueriedUsers.ShouldContain(u => u.TenantId == user2.TenantId, 1);
        AssertPartitionKeyPrefixIsRemoved(msQueriedUsers);

        appleQueriedUsers.ShouldHaveSingleItem();
        appleQueriedUsers.ShouldContain(u => u.TenantId == user1.TenantId, 1);
        AssertPartitionKeyPrefixIsRemoved(appleQueriedUsers);
    }
}
