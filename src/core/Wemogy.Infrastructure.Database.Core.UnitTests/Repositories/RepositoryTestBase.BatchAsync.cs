using System;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task BatchAsync_CreateBatch_ShouldCreateMultipleEntitiesAtomically()
    {
        // Arrange
        await ResetAsync();
        var userA = User.Faker.Generate();
        var userB = User.Faker.Generate();
        userB.TenantId = userA.TenantId; // Cosmos: all batch items must share the same partition key

        // Act
        Exception? exception = null;
        try
        {
            var batch = MicrosoftUserRepository.CreateBatch(userA.TenantId);
            batch.Add(MicrosoftUserRepository.ForBatchCreate(userA));
            batch.Add(MicrosoftUserRepository.ForBatchCreate(userB));
            await batch.ExecuteAsync();

            var fetchedA = await MicrosoftUserRepository.GetAsync(userA.Id, userA.TenantId);
            var fetchedB = await MicrosoftUserRepository.GetAsync(userB.Id, userB.TenantId);
            fetchedA.Id.ShouldBe(userA.Id);
            fetchedB.Id.ShouldBe(userB.Id);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        if (exception is NotSupportedException)
        {
            return; // expected for MongoDB
        }

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task BatchAsync_ForBatchReplace_ShouldUpdateExistingEntity()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        // Act
        Exception? exception = null;
        try
        {
            await MicrosoftUserRepository.CreateAsync(user);
            user.Firstname = "BatchUpdated";

            var batch = MicrosoftUserRepository.CreateBatch(user.TenantId);
            batch.Add(MicrosoftUserRepository.ForBatchReplace(user));
            await batch.ExecuteAsync();

            var fetched = await MicrosoftUserRepository.GetAsync(user.Id, user.TenantId);
            fetched.Firstname.ShouldBe("BatchUpdated");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        if (exception is NotSupportedException)
        {
            return;
        }

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task BatchAsync_ForBatchDelete_ShouldRemoveEntity()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        // Act
        Exception? exception = null;
        try
        {
            await MicrosoftUserRepository.CreateAsync(user);

            var batch = MicrosoftUserRepository.CreateBatch(user.TenantId);
            batch.Add(MicrosoftUserRepository.ForBatchDelete(user.Id, user.TenantId));
            await batch.ExecuteAsync();

            var exists = await MicrosoftUserRepository.ExistsAsync(user.Id, user.TenantId);
            exists.ShouldBeFalse();
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        if (exception is NotSupportedException)
        {
            return;
        }

        exception.ShouldBeNull();
    }
}
