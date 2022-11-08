using System.Collections.Generic;
using System.Linq;
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
        var queryParameters = new QueryParameters();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user1);
        await MicrosoftUserRepository.CreateAsync(user2);

        // Act
        var msQueriedUsers = await MicrosoftUserRepository.QueryAsync(queryParameters);
        var appleQueriedUsers = await AppleUserRepository.QueryAsync(queryParameters);

        // Assert - This fails for now as the QueryAsync is not correctly implemented
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
            .AllSatisfy(u => u.TenantId.Should().NotContain(new MicrosoftTenantProvider().GetTenantId()));
        users.Should()
            .AllSatisfy(u => u.TenantId.Should().NotContain(new AppleTenantProvider().GetTenantId()));
    }
}
