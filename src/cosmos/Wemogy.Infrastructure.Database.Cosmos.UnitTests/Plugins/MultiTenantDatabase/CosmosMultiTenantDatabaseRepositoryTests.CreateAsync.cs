using System;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;
using Wemogy.Infrastructure.Database.Cosmos.Factories;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Plugins.MultiTenantDatabase;

public partial class CosmosMultiTenantDatabaseRepositoryTests : MultiTenantDatabaseRepositoryTestsBase
{
    [Fact]
    public async Task CreateAsync_ShouldReturnTwoDifferentCreatedEntities()
    {
        // Arrange
        await ResetAsync();
        var msUser = User.Faker.Generate();
        var appleUser = User.Faker.Generate();

        // Act
        var msEntity = await MicrosoftUserRepository.CreateAsync(msUser);
        var appleEntity = await AppleUserRepository.CreateAsync(appleUser);

        // Act & Assert
        msEntity.Should().BeEquivalentTo(msUser);
        appleEntity.Should().BeEquivalentTo(appleUser);
    }
}
