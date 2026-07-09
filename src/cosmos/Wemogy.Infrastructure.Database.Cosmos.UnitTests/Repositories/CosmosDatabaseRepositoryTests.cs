using System;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;
using Wemogy.Infrastructure.Database.Cosmos.Factories;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Fakes;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Repositories;

[Collection("Sequential")]
public class CosmosDatabaseRepositoryTests : RepositoryTestBase
{
    private readonly IDatabaseRepository<UserWithETag> _userWithETagRepository;

    public CosmosDatabaseRepositoryTests()
        : base(
            () => CosmosDatabaseRepositoryFactory.CreateInstance<IUserRepository>(
            TestingConstants.ConnectionString,
            TestingConstants.DatabaseName,
            true,
            true),
            () => CosmosDatabaseRepositoryFactory.CreateInstance<IFilteredUserRepository>(
            TestingConstants.ConnectionString,
            TestingConstants.DatabaseName,
            true,
            true),
            () => CosmosDatabaseRepositoryFactory.CreateInstance<IDataCenterRepository>(
            TestingConstants.ConnectionString,
            TestingConstants.DatabaseName,
            true,
            true))
    {
        _userWithETagRepository = CosmosDatabaseRepositoryFactory.CreateInstance<IUserWithETagRepository>(
            TestingConstants.ConnectionString,
            TestingConstants.DatabaseName,
            true,
            true);
    }

    [Fact]
    public async Task GetAsync_ShouldPopulateETagFromCosmos()
    {
        // Arrange
        var user = NewUserWithETag();
        await _userWithETagRepository.CreateAsync(user);

        // Act
        var fetchedUser = await _userWithETagRepository.GetAsync(
            user.Id,
            user.TenantId);

        // Assert: the eTag is read back from Cosmos' system "_etag" field
        fetchedUser.ETag.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ShouldRecoverFromConcurrentETagConflict()
    {
        // Arrange
        var user = NewUserWithETag();
        await _userWithETagRepository.CreateAsync(user);
        var concurrentWriteDone = false;

        // Act: a concurrent writer bumps the eTag between this update's Get and Replace.
        // 1st attempt: Get (eTag A) -> concurrent update writes (eTag becomes B)
        //              -> Replace with IfMatch A -> real 412 -> PreconditionFailedErrorException
        // RetryProxy re-runs UpdateAsync: Get (eTag B) -> Replace with IfMatch B -> success
        var updatedUser = await _userWithETagRepository.UpdateAsync(
            user.Id,
            user.TenantId,
            async u =>
            {
                if (!concurrentWriteDone)
                {
                    concurrentWriteDone = true;
                    await _userWithETagRepository.UpdateAsync(
                        user.Id,
                        user.TenantId,
                        concurrentUser => concurrentUser.Lastname = "Concurrent");
                }

                u.Firstname = "Updated";
            });

        // Assert: both writes survived - proves the 412 fired and the retry re-read.
        // Without the eTag guard the outer replace would overwrite Lastname.
        updatedUser.Firstname.ShouldBe("Updated");
        updatedUser.Lastname.ShouldBe("Concurrent");
    }

    [Fact]
    public async Task ReplaceAsync_ShouldThrowPreconditionFailedForStaleETag()
    {
        // Arrange
        var user = NewUserWithETag();
        await _userWithETagRepository.CreateAsync(user);
        var staleUser = await _userWithETagRepository.GetAsync(
            user.Id,
            user.TenantId);
        var freshUser = await _userWithETagRepository.GetAsync(
            user.Id,
            user.TenantId);
        freshUser.Firstname = "Fresh";
        await _userWithETagRepository.ReplaceAsync(freshUser);

        // Act: replace with the instance that still carries the old eTag.
        // A direct ReplaceAsync has no re-read, so all RetryProxy attempts hit 412.
        staleUser.Firstname = "Stale";
        var exception = await Record.ExceptionAsync(
            () => _userWithETagRepository.ReplaceAsync(staleUser));

        // Assert
        exception.ShouldBeOfType<PreconditionFailedErrorException>();

        // the stale write must NOT have won
        var persistedUser = await _userWithETagRepository.GetAsync(
            user.Id,
            user.TenantId);
        persistedUser.Firstname.ShouldBe("Fresh");
    }

    private static UserWithETag NewUserWithETag()
    {
        return new UserWithETag
        {
            TenantId = Guid.NewGuid().ToString(),
            Firstname = "Initial",
            Lastname = "Initial"
        };
    }
}
