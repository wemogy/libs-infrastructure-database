using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
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
                Property = nameof(User.Firstname),
                Value = user1.Firstname
            });

        // Act
        var msQueriedUsers = await MicrosoftUserRepository.QueryAsync(queryParameters);
        var appleQueriedUsers = await AppleUserRepository.QueryAsync(queryParameters);

        // Assert
        msQueriedUsers.Should().HaveCount(1);
        msQueriedUsers.Should().ContainSingle(u => u.Firstname == user1.Firstname);
        AssertPartitionKeyPrefixIsRemoved(msQueriedUsers);

        appleQueriedUsers.Should().HaveCount(1);
        appleQueriedUsers.Should().ContainSingle(u => u.Firstname == user1.Firstname);
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
                Property = nameof(User.Firstname),
                Value = user1.Firstname
            });

        // TODO: This does not work with partitionKey filters yet
        queryParameters.Filters.Add(
            new QueryFilter
            {
                Comparator = Comparator.Equals,
                Property = nameof(User.TenantId),
                Value = user1.TenantId
            });

        // Act
        var msQueriedUsers = await MicrosoftUserRepository.QueryAsync(queryParameters);
        var appleQueriedUsers = await AppleUserRepository.QueryAsync(queryParameters);

        // Assert
        msQueriedUsers.Should().HaveCount(1);
        msQueriedUsers.Should().ContainSingle(u => u.Firstname == user1.Firstname);
        AssertPartitionKeyPrefixIsRemoved(msQueriedUsers);

        appleQueriedUsers.Should().HaveCount(1);
        appleQueriedUsers.Should().ContainSingle(u => u.Firstname == user1.Firstname);
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
        msQueriedUsers.Should().HaveCount(2);
        msQueriedUsers.Should().ContainSingle(u => u.TenantId == user1.TenantId);
        msQueriedUsers.Should().ContainSingle(u => u.TenantId == user2.TenantId);
        AssertPartitionKeyPrefixIsRemoved(msQueriedUsers);

        appleQueriedUsers.Should().HaveCount(1);
        appleQueriedUsers.Should().ContainSingle(u => u.TenantId == user1.TenantId);
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

        // Act - TODO: does not support partition key in the predicate
        var msQueriedUsers = await MicrosoftUserRepository.QueryAsync(x => x.TenantId == user1.TenantId);
        var appleQueriedUsers = await AppleUserRepository.QueryAsync(x => x.TenantId == user2.TenantId);

        // Assert
        msQueriedUsers.Should().HaveCount(2);
        msQueriedUsers.Should().ContainSingle(u => u.TenantId == user1.TenantId);
        msQueriedUsers.Should().ContainSingle(u => u.TenantId == user2.TenantId);
        AssertPartitionKeyPrefixIsRemoved(msQueriedUsers);

        appleQueriedUsers.Should().HaveCount(1);
        appleQueriedUsers.Should().ContainSingle(u => u.TenantId == user1.TenantId);
        AssertPartitionKeyPrefixIsRemoved(appleQueriedUsers);
    }
}
