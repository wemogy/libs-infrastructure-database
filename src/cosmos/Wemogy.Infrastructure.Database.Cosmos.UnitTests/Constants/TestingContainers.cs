using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Wemogy.Infrastructure.Database.Cosmos.Client;
using Wemogy.Infrastructure.Database.Cosmos.Factories;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;

/// <summary>
///     Makes sure the containers the change feed tests need exist before a processor is started.
///     <para>
///         The emulator seeds them from <c>env/cosmos/init-scripts</c>, but the library deliberately
///         does not create the lease container itself, so a suite run against an emulator that was
///         started before that script gained the container - or against a deployed database - would
///         fail on the setup rather than on the behaviour it is testing.
///     </para>
/// </summary>
public static class TestingContainers
{
    private static readonly Lazy<Task> EnsureLeaseContainer = new Lazy<Task>(
        CreateLeaseContainerAsync,
        true);

    /// <summary>
    ///     Creates the lease container if it is missing. Runs once per test process, and every
    ///     caller awaits the same attempt.
    /// </summary>
    public static Task EnsureLeaseContainerAsync()
    {
        return EnsureLeaseContainer.Value;
    }

    private static async Task CreateLeaseContainerAsync()
    {
        // the application name is passed on to CosmosClientOptions, which rejects a null one
        using var cosmosClient = AzureCosmosClientFactory.FromConnectionString(
            TestingConstants.ConnectionString,
            true,
            applicationName: TestingConstants.DatabaseName);

        var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(TestingConstants.DatabaseName);

        // the partition key path a lease container has to have, see the change feed documentation
        await database.Database.CreateContainerIfNotExistsAsync(
            CosmosDatabaseClientOptions.DefaultLeaseContainerName,
            "/id");
    }
}
