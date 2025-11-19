using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.DynamicProxies;
using Wemogy.Core.DynamicProxies.Enums;
using Wemogy.Core.Errors;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task UpdateAsync_ShouldRetryAutomatically()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        var flakyProxy = new FlakyProxy(
                2,
                FlakyStrategy.ThrowBeforeInvocation,
                () => Error.PreconditionFailed(
                    "EtagMismatch",
                    "Etag mismatch"))
            .OnlyForMethodsWithName(nameof(IDatabaseClient<User>.ReplaceAsync));
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = flakyProxy;
        var flakyUserRepository = UserRepositoryFactory();
        await flakyUserRepository.CreateAsync(user);

        void UpdateAction(User u)
        {
            u.Firstname = "Updated";
        }

        // Act
        var updatedUser = await flakyUserRepository.UpdateAsync(
            user.Id,
            user.TenantId,
            UpdateAction);

        // Assert
        updatedUser.Firstname.ShouldBe("Updated");
        flakyProxy.FailAttempts.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateAsync_ShouldRetryAutomaticallyWithoutPartitionKey()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        var flakyProxy = new FlakyProxy(
                2,
                FlakyStrategy.ThrowBeforeInvocation,
                () => Error.PreconditionFailed(
                    "EtagMismatch",
                    "Etag mismatch"))
            .OnlyForMethodsWithName(nameof(IDatabaseClient<User>.ReplaceAsync));
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = flakyProxy;
        var flakyUserRepository = UserRepositoryFactory();
        await flakyUserRepository.CreateAsync(user);

        void UpdateAction(User u)
        {
            u.Firstname = "Updated";
        }

        // Act
        var updatedUser = await flakyUserRepository.UpdateAsync(
            user.Id,
            UpdateAction);

        // Assert
        updatedUser.Firstname.ShouldBe("Updated");
        flakyProxy.FailAttempts.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateAsync_ShouldRetryOnlyMaxAttempts()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        var flakyProxy = new FlakyProxy(
                100,
                FlakyStrategy.ThrowBeforeInvocation,
                () => Error.PreconditionFailed(
                    "EtagMismatch",
                    "Etag mismatch"))
            .OnlyForMethodsWithName(nameof(IDatabaseClient<User>.ReplaceAsync));
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = flakyProxy;
        var flakyUserRepository = UserRepositoryFactory();
        await flakyUserRepository.CreateAsync(user);

        void UpdateAction(User u)
        {
            u.Firstname = "Updated";
        }

        // Act
        await Should.ThrowAsync<PreconditionFailedErrorException>(
            () => flakyUserRepository.UpdateAsync(
                user.Id,
                user.TenantId,
                UpdateAction));

        // Assert
        flakyProxy.FailAttempts.ShouldBe(4);
    }
}
