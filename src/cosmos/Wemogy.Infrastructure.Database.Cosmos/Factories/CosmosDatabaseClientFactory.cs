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
        private readonly ILogger? _logger;

        public CosmosDatabaseClientFactory(
            string connectionString,
            string databaseName,
            bool insecureDevelopmentMode = false,
            bool enableLogging = false,
            List<string>? containerNames = null)
            : this(
                AzureCosmosClientFactory.FromConnectionString(
            connectionString,
            insecureDevelopmentMode,
            containerNames?.Select(c => (databaseName, c)).ToList(),
            databaseName),
                databaseName,
                enableLogging)
        {
        }

        public CosmosDatabaseClientFactory(
            CosmosClient cosmosClient,
            string databaseName,
            bool enableLogging = false)
        {
            _cosmosClient = cosmosClient;
            _databaseName = databaseName;

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
                databaseRepositoryOptions.CollectionName);

            return new CosmosDatabaseClient<TEntity>(
                _cosmosClient,
                options,
                _logger);
        }

        public bool IsMultiTenantDatabaseSupported => true;
    }
}
