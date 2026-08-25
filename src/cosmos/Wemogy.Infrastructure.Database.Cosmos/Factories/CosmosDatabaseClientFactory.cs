using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Cosmos.Client;
using Wemogy.Infrastructure.Database.Cosmos.Extensions;

namespace Wemogy.Infrastructure.Database.Cosmos.Factories
{
    public class CosmosDatabaseClientFactory : IDatabaseClientFactory
    {
        private readonly CosmosClient _cosmosClient;
        private readonly string _databaseName;
        private readonly string _leaseContainerName;
        private readonly ILogger? _logger;

        /// <param name="connectionString">The connection string of the Cosmos DB account</param>
        /// <param name="databaseName">The database every repository of this factory reads and writes</param>
        /// <param name="insecureDevelopmentMode">
        ///     Skips certificate checks and uses gateway mode, to talk to the local emulator
        /// </param>
        /// <param name="enableLogging">Logs the queries the repositories issue to the console</param>
        /// <param name="containerNames">
        ///     The containers this application commonly uses, to speed up the initialization
        /// </param>
        /// <param name="leaseContainerName">
        ///     The container the change feed processors keep their leases in. One container serves
        ///     every processor of the database; it has to exist with the partition key path
        ///     <c>/id</c> before a processor is started. Only relevant for repositories whose change
        ///     feed is read.
        /// </param>
        public CosmosDatabaseClientFactory(
            string connectionString,
            string databaseName,
            bool insecureDevelopmentMode = false,
            bool enableLogging = false,
            List<string>? containerNames = null,
            string leaseContainerName = CosmosDatabaseClientOptions.DefaultLeaseContainerName)
            : this(
                AzureCosmosClientFactory.FromConnectionString(
            connectionString,
            insecureDevelopmentMode,
            containerNames?.Select(c => (databaseName, c)).ToList(),
            databaseName),
                databaseName,
                enableLogging,
                leaseContainerName)
        {
        }

        public CosmosDatabaseClientFactory(
            CosmosClient cosmosClient,
            string databaseName,
            bool enableLogging = false,
            string leaseContainerName = CosmosDatabaseClientOptions.DefaultLeaseContainerName)
        {
            _cosmosClient = cosmosClient;
            _databaseName = databaseName;
            _leaseContainerName = leaseContainerName;

            if (enableLogging)
            {
                var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                });
                _logger = loggerFactory.CreateLogger(nameof(QueryParametersExtensions));
            }
        }

        public IDatabaseClient<TEntity> CreateClient<TEntity>(DatabaseRepositoryOptions databaseRepositoryOptions)
            where TEntity : class
        {
            var options = new CosmosDatabaseClientOptions(
                _databaseName,
                databaseRepositoryOptions.CollectionName,
                _leaseContainerName);

            return new CosmosDatabaseClient<TEntity>(
                _cosmosClient,
                options,
                _logger);
        }

        public bool IsMultiTenantDatabaseSupported => true;
    }
}
