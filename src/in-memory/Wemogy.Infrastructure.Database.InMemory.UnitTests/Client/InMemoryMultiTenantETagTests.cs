using System;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;
using Wemogy.Infrastructure.Database.InMemory.Factories;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Client;

/// <summary>
///     Optimistic concurrency through the multi-tenant wrapper. The wrapper has to hand the
///     provider's entity back to the caller, otherwise the caller never sees the assigned eTag and
///     optimistic concurrency is silently off for every multi-tenant repository.
/// </summary>
[Collection("Sequential")]
public class InMemoryMultiTenantETagTests
{
    private readonly IDatabaseRepository<User> _userRepository;

    public InMemoryMultiTenantETagTests()
    {
        _userRepository = new MultiTenantDatabaseRepository<User>(
            InMemoryDatabaseRepositoryFactory.CreateInstance<IUserRepository>(),
            new MicrosoftTenantProvider());
        _userRepository.DeleteAsync(_ => true).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnTheAssignedETag()
    {
        // Arrange & Act
        var createdUser = await _userRepository.CreateAsync(NewUser());

        // Assert
        createdUser.ETag.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnTheUnprefixedPartitionKey()
    {
        // Arrange
        var user = NewUser();
        var tenantId = user.TenantId;

        // Act
        var createdUser = await _userRepository.CreateAsync(user);

        // Assert: the tenant prefix is an implementation detail of the wrapper
        createdUser.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task ReplaceAsync_ShouldReturnAnEntityThatCanBeReplacedAgain()
    {
        // Arrange
        var createdUser = await _userRepository.CreateAsync(NewUser());

        // Act: the returned entity has to carry the new eTag, otherwise the second replace
        // fails the precondition with a stale one
        createdUser.Firstname = "First";
        var replacedUser = await _userRepository.ReplaceAsync(createdUser);
        replacedUser.Firstname = "Second";
        var exception = await Record.ExceptionAsync(() => _userRepository.ReplaceAsync(replacedUser));

        // Assert
        exception.ShouldBeNull();
        replacedUser.ETag.ShouldNotBe(createdUser.ETag);
    }

    [Fact]
    public async Task ReplaceAsync_ShouldReturnTheUnprefixedPartitionKey()
    {
        // Arrange
        var createdUser = await _userRepository.CreateAsync(NewUser());
        var tenantId = createdUser.TenantId;

        // Act
        createdUser.Firstname = "Updated";
        var replacedUser = await _userRepository.ReplaceAsync(createdUser);

        // Assert
        replacedUser.TenantId.ShouldBe(tenantId);
    }

    private static User NewUser()
    {
        return new User
        {
            TenantId = Guid.NewGuid().ToString(),
            Firstname = "Initial",
            Lastname = "Initial"
        };
    }
}
