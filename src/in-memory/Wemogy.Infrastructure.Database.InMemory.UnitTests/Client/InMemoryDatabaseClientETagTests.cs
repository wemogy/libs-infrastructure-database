using System;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.InMemory.Factories;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Client;

/// <summary>
///     Optimistic concurrency for the in-memory provider. <c>EntityBase</c> carries the
///     <c>[ETag]</c> attribute, so every entity derived from it takes part. These tests mirror the
///     Cosmos ones, so a test written against the in-memory provider behaves the same way when it
///     is later pointed at Cosmos.
/// </summary>
[Collection("Sequential")]
public class InMemoryDatabaseClientETagTests
{
    private readonly IDatabaseRepository<User> _userRepository;

    public InMemoryDatabaseClientETagTests()
    {
        _userRepository = InMemoryDatabaseRepositoryFactory.CreateInstance<IUserRepository>();
        _userRepository.DeleteAsync(_ => true).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task CreateAsync_ShouldAssignAnETag()
    {
        // Arrange
        var user = NewUser();

        // Act
        var createdUser = await _userRepository.CreateAsync(user);

        // Assert
        createdUser.ETag.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAsync_ShouldReturnTheStoredETag()
    {
        // Arrange
        var user = NewUser();
        var createdUser = await _userRepository.CreateAsync(user);

        // Act
        var fetchedUser = await _userRepository.GetAsync(
            user.Id,
            user.TenantId);

        // Assert
        fetchedUser.ETag.ShouldBe(createdUser.ETag);
    }

    [Fact]
    public async Task ReplaceAsync_ShouldAssignANewETag()
    {
        // Arrange
        var user = NewUser();
        var createdUser = await _userRepository.CreateAsync(user);

        // Act
        createdUser.Firstname = "Updated";
        var replacedUser = await _userRepository.ReplaceAsync(createdUser);

        // Assert: every write has to move the version forward, otherwise a stale write would be
        // accepted the second time around
        replacedUser.ETag.ShouldNotBeNullOrEmpty();
        replacedUser.ETag.ShouldNotBe(createdUser.ETag);
    }

    [Fact]
    public async Task ReplaceAsync_ShouldThrowPreconditionFailedForStaleETag()
    {
        // Arrange
        var user = NewUser();
        await _userRepository.CreateAsync(user);
        var staleUser = await _userRepository.GetAsync(
            user.Id,
            user.TenantId);
        var freshUser = await _userRepository.GetAsync(
            user.Id,
            user.TenantId);
        freshUser.Firstname = "Fresh";
        await _userRepository.ReplaceAsync(freshUser);

        // Act
        staleUser.Firstname = "Stale";
        var exception = await Record.ExceptionAsync(() => _userRepository.ReplaceAsync(staleUser));

        // Assert
        exception.ShouldBeOfType<PreconditionFailedErrorException>();

        // the stale write must not have won
        var persistedUser = await _userRepository.GetAsync(
            user.Id,
            user.TenantId);
        persistedUser.Firstname.ShouldBe("Fresh");
    }

    [Fact]
    public async Task ReplaceAsync_ShouldAcceptAnEntityWithoutAnETag()
    {
        // Arrange
        var user = NewUser();
        await _userRepository.CreateAsync(user);

        // Act: a caller that never read the entity asks for no precondition, which mirrors a
        // Cosmos replace with a null IfMatchEtag
        user.Firstname = "Blind";
        var exception = await Record.ExceptionAsync(() => _userRepository.ReplaceAsync(user));

        // Assert
        exception.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldRecoverFromConcurrentETagConflict()
    {
        // Arrange
        var user = NewUser();
        await _userRepository.CreateAsync(user);
        var concurrentWriteDone = false;

        // Act: a concurrent writer bumps the eTag between this update's Get and Replace, so the
        // first attempt fails the precondition and the retry proxy re-reads
        var updatedUser = await _userRepository.UpdateAsync(
            user.Id,
            user.TenantId,
            async u =>
            {
                if (!concurrentWriteDone)
                {
                    concurrentWriteDone = true;
                    await _userRepository.UpdateAsync(
                        user.Id,
                        user.TenantId,
                        concurrentUser => concurrentUser.Lastname = "Concurrent");
                }

                u.Firstname = "Updated";
            });

        // Assert: both writes survived, which proves the precondition fired and the retry re-read
        updatedUser.Firstname.ShouldBe("Updated");
        updatedUser.Lastname.ShouldBe("Concurrent");
    }

    [Fact]
    public async Task UpsertAsync_ShouldNotEnforceThePrecondition()
    {
        // Arrange
        var user = NewUser();
        await _userRepository.CreateAsync(user);
        var staleUser = await _userRepository.GetAsync(
            user.Id,
            user.TenantId);
        staleUser.Firstname = "First";
        await _userRepository.ReplaceAsync(staleUser);

        // Act: an upsert carries no IfMatch, so even a stale instance overwrites
        staleUser.Firstname = "Stale upsert";
        var exception = await Record.ExceptionAsync(() => _userRepository.UpsertAsync(staleUser));

        // Assert
        exception.ShouldBeNull();
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
