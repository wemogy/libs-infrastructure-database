using System;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Core.DynamicProxies;
using Wemogy.Core.DynamicProxies.Enums;
using Wemogy.Core.DynamicProxies.Extensions;
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
        await SeedAsync();
        var user = User.Faker.Generate();
        await UserRepository.CreateAsync(user);

        var flakyProxy = new FlakyProxy(
                2,
                FlakyStrategy.ThrowBeforeInvocation,
                () => Error.PreconditionFailed("EtagMismatch", "Etag mismatch"))
            .OnlyForMethodsWithName(nameof(IDatabaseClient<User, Guid, Guid>.ReplaceAsync));
        RepositoryFactoryFactory.DatabaseClientProxy = flakyProxy;
        var flakyUserRepository = UserRepositoryFactory();

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
        updatedUser.Firstname.Should().Be("Updated");
        flakyProxy.FailAttempts.Should().Be(2);
    }

    [Fact]
    public async Task UpdateAsync_ShouldRetryOnlyMaxAttempts()
    {
        // Arrange
        await SeedAsync();
        var user = User.Faker.Generate();
        await UserRepository.CreateAsync(user);

        var flakyProxy = new FlakyProxy(
                100,
                FlakyStrategy.ThrowBeforeInvocation,
                () => Error.PreconditionFailed("EtagMismatch", "Etag mismatch"))
            .OnlyForMethodsWithName(nameof(IDatabaseClient<User, Guid, Guid>.ReplaceAsync));
        RepositoryFactoryFactory.DatabaseClientProxy = flakyProxy;
        var flakyUserRepository = UserRepositoryFactory();

        void UpdateAction(User u)
        {
            u.Firstname = "Updated";
        }

        // Act
        var updatedUserException = await Record.ExceptionAsync(() => flakyUserRepository.UpdateAsync(
            user.Id,
            user.TenantId,
            UpdateAction));

        // Assert
        updatedUserException.Should().BeOfType<PreconditionFailedErrorException>();
        flakyProxy.FailAttempts.Should().Be(4);
    }
}
