using System;
using System.Linq;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Cosmos.Client;
using Wemogy.Infrastructure.Database.Cosmos.Factories;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;
using Xunit;
using User = Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities.User;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Client;

/// <summary>
///     How a change of the all-versions-and-deletes feed is mapped onto the provider-independent one.
///     <para>
///         Covered against constructed items rather than a live feed because the Linux vNext emulator
///         does not serve that feed at all, so nothing else here can reach this code.
///     </para>
/// </summary>
public class CosmosChangeFeedMappingTests
{
    private readonly CosmosDatabaseClient<User> _client;

    public CosmosChangeFeedMappingTests()
    {
        // no request is issued by building this, so the mapping is covered without a database
        var cosmosClient = AzureCosmosClientFactory.FromConnectionString(
            TestingConstants.ConnectionString,
            true,
            applicationName: TestingConstants.DatabaseName);

        _client = new CosmosDatabaseClient<User>(
            cosmosClient,
            new CosmosDatabaseClientOptions(
                TestingConstants.DatabaseName,
                "users"),
            null);
    }

    [Fact]
    public void ToDatabaseChange_ShouldReportNoCurrentVersionForADelete()
    {
        // Arrange: Cosmos sends "current": {} for a delete, and ChangeFeedItem<T>.Current is a
        // non-nullable T - so it deserializes into a default-constructed entity, not into null
        var removedUser = NewUser();
        var item = new ChangeFeedItem<User>
        {
            Current = new User(),
            Previous = removedUser,
            Metadata = NewMetadata("delete")
        };

        // Act
        var change = _client.ToDatabaseChange(item);

        // Assert
        change.Operation.ShouldBe(DatabaseChangeOperation.Delete);

        // forwarding the empty object would break the promise that a delete carries no current
        // version, and would have the multi-tenant wrapper read the partition key off it
        change.Current.ShouldBeNull();
        change.Previous.ShouldNotBeNull();
        change.Previous!.Id.ShouldBe(removedUser.Id);
    }

    [Fact]
    public void ToDatabaseChange_ShouldReportNoPreviousVersionForACreate()
    {
        // Arrange
        var createdUser = NewUser();
        var item = new ChangeFeedItem<User>
        {
            Current = createdUser,
            Previous = new User(),
            Metadata = NewMetadata("create")
        };

        // Act
        var change = _client.ToDatabaseChange(item);

        // Assert
        change.Operation.ShouldBe(DatabaseChangeOperation.Create);
        change.Current!.Id.ShouldBe(createdUser.Id);
        change.Previous.ShouldBeNull();
    }

    [Fact]
    public void ToDatabaseChange_ShouldKeepBothVersionsOfAReplace()
    {
        // Arrange
        var previousUser = NewUser();
        var currentUser = NewUser();
        var item = new ChangeFeedItem<User>
        {
            Current = currentUser,
            Previous = previousUser,
            Metadata = NewMetadata("replace")
        };

        // Act
        var change = _client.ToDatabaseChange(item);

        // Assert
        change.Operation.ShouldBe(DatabaseChangeOperation.Replace);
        change.Current!.Id.ShouldBe(currentUser.Id);
        change.Previous!.Id.ShouldBe(previousUser.Id);
    }

    [Fact]
    public void ToDatabaseChange_ShouldReportTheDeletedDocumentEvenWhenItCarriesAnId()
    {
        // Arrange: the empty object Cosmos sends for the current version of a delete deserializes
        // into a default-constructed entity, and an entity filling in its own id - as EntityBase
        // does - is indistinguishable from a real document. So the operation type has to decide,
        // not the contents
        var item = new ChangeFeedItem<User>
        {
            Current = new User(),
            Previous = NewUser(),
            Metadata = NewMetadata("delete")
        };
        item.Current.Id.ShouldNotBeNullOrEmpty();

        // Act
        var change = _client.ToDatabaseChange(item);

        // Assert
        change.Current.ShouldBeNull();
    }

    [Theory]
    [InlineData(3, 2, new[] { 2, 1 })]
    [InlineData(4, 2, new[] { 2, 2 })]
    [InlineData(2, 5, new[] { 2 })]
    [InlineData(0, 2, new int[0])]
    public void Batch_ShouldSplitWhatTheProviderReadIntoTheBoundTheHandlerWasPromised(
        int changeCount,
        int maxItemsPerBatch,
        int[] expectedBatchSizes)
    {
        // Arrange: the Cosmos SDK treats its batch size as a hint and may hand over more, so the
        // provider splits what it read rather than passing the bound on and hoping
        var changes = Enumerable.Range(0, changeCount).ToList();

        // Act
        var batches = CosmosDatabaseClient<User>.Batch(changes, maxItemsPerBatch).ToList();

        // Assert
        batches.Select(batch => batch.Count).ShouldBe(expectedBatchSizes);
        batches.SelectMany(batch => batch).ShouldBe(changes);
    }

    [Fact]
    public void Batch_ShouldHandOverOneBatchWhenNoBoundIsConfigured()
    {
        // Arrange
        var changes = Enumerable.Range(0, 5).ToList();

        // Act
        var batches = CosmosDatabaseClient<User>.Batch(changes, null).ToList();

        // Assert
        batches.Count.ShouldBe(1);
        batches.Single().ShouldBe(changes);
    }

    private static User NewUser()
    {
        var user = User.Faker.Generate();
        user.TenantId = Guid.NewGuid().ToString();
        return user;
    }

    /// <summary>
    ///     Deserialized rather than constructed: the setters of <see cref="ChangeFeedMetadata"/> are
    ///     not public, and the conflict resolution timestamp is required.
    /// </summary>
    private static ChangeFeedMetadata NewMetadata(string operationType)
    {
        var json =
            $"{{\"operationType\":\"{operationType}\",\"lsn\":42,\"timeToLiveExpired\":false,\"crts\":1700000000}}";

        return JsonConvert.DeserializeObject<ChangeFeedMetadata>(json)!;
    }
}
