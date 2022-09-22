using System;
using Microsoft.Azure.Cosmos;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Cosmos.Client;

namespace Wemogy.Infrastructure.Database.Cosmos.Factories
{
    public class CosmosDatabaseClientFactory : IDatabaseClientFactory
    {
        private readonly CosmosClient _cosmosClient;
        private readonly string _databaseName;
        public CosmosDatabaseClientFactory(
            string connectionString,
            string databaseName,
            bool insecureDevelopmentMode = false)
        {
            _cosmosClient = CosmosClientFactory.FromConnectionString(connectionString, insecureDevelopmentMode);
            _databaseName = databaseName;
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
                options);
        }
    }
}
