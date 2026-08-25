using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Azure.Cosmos;
using Wemogy.Infrastructure.Database.Cosmos.Factories;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;

/// <summary>
///     Creates the containers the integration tests need but the emulator's init script cannot:
///     its <c>mkcon</c> takes a single partition key path, so a container with a hierarchical key
///     has to be created through the SDK.
/// </summary>
public static class TestingContainers
{
    /// <summary>
    ///     The container behind <see cref="Core.UnitTests.Fakes.Entities.UsageEvent"/>, whose
    ///     paths have to be listed in the order the entity numbers its key components.
    ///     <para>
    ///         <see cref="LazyThreadSafetyMode.PublicationOnly"/> so a failure is not cached: the
    ///         creation talks to the emulator, and caching the first exception would fail every
    ///         remaining test of the run instead of just retrying.
    ///     </para>
    /// </summary>
    private static readonly Lazy<bool> HierarchicalContainer = new Lazy<bool>(
        () => CreateContainer(
            "usageevents",
            new List<string> { "/customerId", "/meterSlug", "/timeBucket" }),
        LazyThreadSafetyMode.PublicationOnly);

    /// <summary>
    ///     Ensures the container of the hierarchical test entity exists. Runs once per test
    ///     process, no matter how many test classes ask for it.
    /// </summary>
    public static void EnsureHierarchicalContainerExists()
    {
        _ = HierarchicalContainer.Value;
    }

    private static bool CreateContainer(string containerName, IReadOnlyList<string> partitionKeyPaths)
    {
        // the application name is passed the way the client factory of the library does: the
        // factory assigns it unconditionally and the SDK refuses a null one
        using var cosmosClient = AzureCosmosClientFactory.FromConnectionString(
            TestingConstants.ConnectionString,
            insecureDevelopmentMode: true,
            applicationName: TestingConstants.DatabaseName);

        var database = cosmosClient
            .CreateDatabaseIfNotExistsAsync(TestingConstants.DatabaseName)
            .GetAwaiter()
            .GetResult()
            .Database;

        database
            .CreateContainerIfNotExistsAsync(new ContainerProperties(containerName, partitionKeyPaths))
            .GetAwaiter()
            .GetResult();

        return true;
    }
}
