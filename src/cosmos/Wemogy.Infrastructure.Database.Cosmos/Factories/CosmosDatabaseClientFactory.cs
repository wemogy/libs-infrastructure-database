using System;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Cosmos.Client;

namespace Wemogy.Infrastructure.Database.Cosmos.Factories
{
    public class CosmosDatabaseClientFactory : IDatabaseClientFactory
    {
        private readonly CosmosClient _cosmosClient;
        private readonly string _databaseName;
        private readonly ILogger _logger;

        public CosmosDatabaseClientFactory(
            string connectionString,
            string databaseName,
            ILogger logger,
            bool insecureDevelopmentMode = false)
        {
            _cosmosClient = CosmosClientFactory.FromConnectionString(connectionString, insecureDevelopmentMode);
            _databaseName = databaseName;
            _logger = logger;
        }

        public IDatabaseClient<TEntity, TPartitionKey, TId> CreateClient<TEntity, TPartitionKey, TId>(DatabaseRepositoryOptions databaseRepositoryOptions)
            where TEntity : class, IEntityBase<TId>
            where TPartitionKey : IEquatable<TPartitionKey>
            where TId : IEquatable<TId>
        {
            var options = new CosmosDatabaseClientOptions(
                _databaseName,
                databaseRepositoryOptions.CollectionName);

            return new CosmosDatabaseClient<TEntity, TPartitionKey, TId>(
                _cosmosClient,
                options,
                _logger);
        }
    }
}
