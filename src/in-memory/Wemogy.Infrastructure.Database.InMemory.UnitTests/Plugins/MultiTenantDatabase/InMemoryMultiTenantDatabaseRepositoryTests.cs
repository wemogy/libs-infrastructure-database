using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;
using Wemogy.Infrastructure.Database.InMemory.Factories;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Plugins.MultiTenantDatabase;

[Collection("Sequential")]
public class InMemoryMultiTenantDatabaseRepositoryTests : MultiTenantDatabaseRepositoryTestsBase
{
    public InMemoryMultiTenantDatabaseRepositoryTests()
        : base(
            GetFactoryUser(new MicrosoftTenantProvider()),
            GetFactoryFilteredUser(new MicrosoftTenantProvider()),
            GetFactoryUser(new AppleTenantProvider()),
            GetFactoryDataCenter(new DataCenterTenantProvider()))
    {
    }

    /// <remarks>
    ///     In-memory only: the Cosmos DB emulator does not serve the all-versions-and-deletes feed,
    ///     so the shared suite cannot assert on what it carries.
    /// </remarks>
    [Fact]
    public async Task AllVersionsAndDeletesChangeFeed_ShouldStripTheTenantPrefixOffBothVersions()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = Guid.NewGuid().ToString();
        var changes = new List<DatabaseChange<User>>();
        var gate = new object();

        await using var processor = MicrosoftUserRepository.CreateAllVersionsAndDeletesChangeFeedProcessor(
            $"tenant-prefix-{Guid.NewGuid():N}",
            (batch, context, cancellationToken) =>
            {
                lock (gate)
                {
                    changes.AddRange(batch);
                }

                return Task.CompletedTask;
            });
        await processor.StartAsync();

        // Act: a replace, so the change carries a previous version as well as a current one
        var user = User.Faker.Generate();
        user.TenantId = partitionKey;
        await MicrosoftUserRepository.CreateAsync(user);
        user.Firstname = "Renamed";
        await MicrosoftUserRepository.ReplaceAsync(user);

        // Assert
        await WaitUntilAsync(() =>
        {
            lock (gate)
            {
                return changes.Any(x => x.Previous != null);
            }
        });

        List<DatabaseChange<User>> observedChanges;
        lock (gate)
        {
            observedChanges = changes.ToList();
        }

        var replace = observedChanges.Last(x => x.Previous != null);
        replace.Current!.TenantId.ShouldBe(partitionKey);

        // the previous version used to keep the "microsoft__" prefix, which made a handler comparing
        // the two versions see two different partition keys for one document
        replace.Previous!.TenantId.ShouldBe(partitionKey);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!condition())
        {
            if (stopwatch.Elapsed > TimeSpan.FromSeconds(10))
            {
                throw new TimeoutException("The change feed did not deliver the expected changes in time");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }
    }

    private static Func<IDatabaseRepository<User>> GetFactoryUser(IDatabaseTenantProvider provider)
    {
        return () =>
        {
            var databaseRepository = InMemoryDatabaseRepositoryFactory.CreateInstance<IUserRepository>();

            var multiTenantRepository = new MultiTenantDatabaseRepository<User>(
                databaseRepository,
                provider);

            return multiTenantRepository;
        };
    }

    private static Func<IDatabaseRepository<User>> GetFactoryFilteredUser(IDatabaseTenantProvider provider)
    {
        return () =>
        {
            var databaseRepository = InMemoryDatabaseRepositoryFactory.CreateInstance<IFilteredUserRepository>();

            var multiTenantRepository = new MultiTenantDatabaseRepository<User>(
                databaseRepository,
                provider);

            return multiTenantRepository;
        };
    }

    private static Func<IDatabaseRepository<DataCenter>> GetFactoryDataCenter(IDatabaseTenantProvider provider)
    {
        return () =>
        {
            var databaseRepository = InMemoryDatabaseRepositoryFactory.CreateInstance<IDataCenterRepository>();

            var multiTenantRepository = new MultiTenantDatabaseRepository<DataCenter>(
                databaseRepository,
                provider);

            return multiTenantRepository;
        };
    }
}
