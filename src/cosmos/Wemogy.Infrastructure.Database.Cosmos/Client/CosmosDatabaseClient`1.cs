using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Wemogy.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.Cosmos.Extensions;
using Wemogy.Infrastructure.Database.Cosmos.Models;

namespace Wemogy.Infrastructure.Database.Cosmos.Client
{
    public class CosmosDatabaseClient<TEntity> : DatabaseClientBase<TEntity>, IDatabaseClient<TEntity>
        where TEntity : class
    {
        private readonly ILogger? _logger;
        private readonly Container _container;
        private MappingMetadata? _cachedMappingMetadata;

        public CosmosDatabaseClient(CosmosClient cosmosClient, CosmosDatabaseClientOptions options, ILogger? logger)
        {
            var database = cosmosClient.GetDatabase(options.DatabaseName);
            var containerName = options.ContainerName;
            _container = database.GetContainer(containerName);
            _logger = logger;
        }

        public async Task<TEntity> GetAsync(string id, string partitionKey, CancellationToken cancellationToken)
        {
            try
            {
                var itemResponse = await _container.ReadItemAsync<TEntity>(
                    id,
                    new PartitionKey<string>(partitionKey).CosmosPartitionKey,
                    cancellationToken: cancellationToken);

                return itemResponse;
            }
            catch (CosmosException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    throw DatabaseError.EntityNotFound(
                        id,
                        partitionKey);
                }

                throw;
            }
        }

        public async Task IterateAsync(
            QueryParameters queryParameters,
            Expression<Func<TEntity, bool>>? generalFilterPredicate,
            Func<TEntity, Task> callback,
            CancellationToken cancellationToken)
        {
            var feedIterator = GetFeedIterator(
                queryParameters,
                generalFilterPredicate);

            await feedIterator.IterateAsync(
                callback,
                cancellationToken);
        }

        public Task IterateAsync(
            Expression<Func<TEntity, bool>> predicate,
            Func<TEntity, Task> callback,
            CancellationToken cancellationToken = default)
        {
            var feedIterator = _container.GetItemLinqQueryable<TEntity>()
                .Where(predicate)
                .ToFeedIterator();

            return feedIterator.IterateAsync(
                callback,
                cancellationToken);
        }

        public async Task<TEntity> CreateAsync(TEntity entity)
        {
            var partitionKey = ResolvePartitionKey(entity);
            try
            {
                var createResponse = await _container.CreateItemAsync(
                    entity,
                    partitionKey.CosmosPartitionKey,
                    new ItemRequestOptions
                    {
                        EnableContentResponseOnWrite = true
                    });

                return createResponse.Resource;
            }
            catch (CosmosException cosmosException)
            {
                if (cosmosException.StatusCode == HttpStatusCode.Conflict)
                {
                    throw Error.Conflict(
                        "AlreadyExists",
                        $"Entity with id {ResolveIdValue(entity)} already exists");
                }

                throw;
            }
        }

        public async Task<TEntity> ReplaceAsync(TEntity entity)
        {
            try
            {
                var id = ResolveIdValue(entity);
                var partitionKey = ResolvePartitionKey(entity);
                var replaceResponse = await _container.ReplaceItemAsync(
                    entity,
                    id,
                    partitionKey.CosmosPartitionKey);

                return replaceResponse.Resource;
            }
            catch (CosmosException cosmosException)
            {
                if (cosmosException.StatusCode == HttpStatusCode.NotFound)
                {
                    throw DatabaseError.EntityNotFound(
                        ResolveIdValue(entity),
                        ResolvePartitionKeyValue(entity));
                }

                throw;
            }
        }

        public Task DeleteAsync(string id, string partitionKey)
        {
            return DeleteAsync(
                id,
                new PartitionKey<string>(partitionKey));
        }

        public async Task<int> DeleteAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var count = 0;
            await IterateAsync(
                predicate,
                async entity =>
                {
                    var id = ResolveIdValue(entity);
                    var partitionKey = ResolvePartitionKey(entity);
                    await DeleteAsync(
                        id,
                        partitionKey);
                    count++;
                });

            return count;
        }

        private async Task DeleteAsync(string id, PartitionKey<string> partitionKey)
        {
            try
            {
                await _container.DeleteItemAsync<TEntity>(
                    id,
                    partitionKey.CosmosPartitionKey);
            }
            catch (CosmosException e)
            {
                switch (e.StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        throw DatabaseError.EntityNotFound(
                            id,
                            partitionKey.ToString());
                    default:
                        throw;
                }
            }
        }

        private PartitionKey<string> ResolvePartitionKey(TEntity item)
        {
            var partitionKeyValue = ResolvePartitionKeyValue(item);
            return new PartitionKey<string>(partitionKeyValue);
        }

        private FeedIterator<TEntity> GetFeedIterator(
            QueryParameters queryParameters,
            Expression<Func<TEntity, bool>>? generalFilterPredicate)
        {
            IQueryable<TEntity> queryable = _container.GetItemLinqQueryable<TEntity>();
            if (generalFilterPredicate != null)
            {
                queryable = queryable.Where(generalFilterPredicate);
            }

            var mappingMetadata = GetMappingMetadata();
            return _container.GetItemQueryIterator<TEntity, string>(
                queryParameters,
                mappingMetadata,
                queryable,
                _logger);
        }

        private MappingMetadata GetMappingMetadata()
        {
            if (_cachedMappingMetadata != null)
            {
                return _cachedMappingMetadata;
            }

            var mappingMetadata = new MappingMetadata();
            mappingMetadata.InitializeUsingReflection(typeof(TEntity));

            _cachedMappingMetadata = mappingMetadata;

            return mappingMetadata;
        }
    }
}
