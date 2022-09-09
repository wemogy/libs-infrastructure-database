using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Wemogy.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.Cosmos.Extensions;
using Wemogy.Infrastructure.Database.Cosmos.Models;

namespace Wemogy.Infrastructure.Database.Cosmos.Client
{
    public class CosmosDatabaseClient<TEntity, TPartitionKey, TId> : DatabaseClientBase<TEntity, TPartitionKey, TId>,
        IDatabaseClient<TEntity, TPartitionKey, TId>
        where TEntity : class, IEntityBase<TId>
        where TPartitionKey : IEquatable<TPartitionKey>
        where TId : IEquatable<TId>
    {
        private readonly Container _container;

        public CosmosDatabaseClient(CosmosClient cosmosClient, CosmosDatabaseClientOptions options)
        {
            var database = cosmosClient.GetDatabase(options.DatabaseName);
            var containerName = options.ContainerName;
            _container = database.GetContainer(containerName);
        }

        public async Task<TEntity> GetAsync(TId id, TPartitionKey partitionKey, CancellationToken cancellationToken)
        {
            try
            {
                var itemResponse = await _container.ReadItemAsync<TEntity>(
                    id.ToString(),
                    new PartitionKey<TPartitionKey>(partitionKey).CosmosPartitionKey,
                    cancellationToken: cancellationToken);

                return itemResponse;
            }
            catch (CosmosException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    throw DatabaseError.EntityNotFound(id.ToString(), partitionKey.ToString());
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
            var feedIterator = GetFeedIterator(queryParameters, generalFilterPredicate);

            await feedIterator.IterateAsync(callback, cancellationToken);
        }

        public Task IterateAsync(
            Expression<Func<TEntity, bool>> predicate,
            Func<TEntity, Task> callback,
            CancellationToken cancellationToken = default)
        {
            var feedIterator = _container.GetItemLinqQueryable<TEntity>()
                .Where(predicate)
                .ToFeedIterator();

            return feedIterator.IterateAsync(callback, cancellationToken);
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
                        EnableContentResponseOnWrite = false
                    });

                return createResponse.Resource;
            }
            catch (CosmosException cosmosException)
            {
                if (cosmosException.StatusCode == HttpStatusCode.Conflict)
                {
                    throw Error.Conflict(
                        "AlreadyExists",
                        $"Entity with id {entity.Id} already exists");
                }

                throw;
            }
        }

        public async Task<TEntity> ReplaceAsync(TEntity entity)
        {
            try
            {
                var partitionKey = ResolvePartitionKey(entity);
                var replaceResponse = await _container.ReplaceItemAsync(
                    entity,
                    entity.Id.ToString(),
                    partitionKey.CosmosPartitionKey);



                return replaceResponse.Resource;
            }
            catch (CosmosException cosmosException)
            {
                if (cosmosException.StatusCode == HttpStatusCode.NotFound)
                {
                    throw DatabaseError.EntityNotFound(entity.Id.ToString(), ResolvePartitionKeyValue(entity).ToString());
                }

                throw;
            }
        }

        public Task DeleteAsync(TId id, TPartitionKey partitionKey)
        {
            return DeleteAsync(
                id,
                new PartitionKey<TPartitionKey>(partitionKey));
        }

        public Task DeleteAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return IterateAsync(predicate, async entity =>
            {
                var partitionKey = ResolvePartitionKey(entity);
                await DeleteAsync(entity.Id, partitionKey);
            });
        }

        private async Task DeleteAsync(TId id, PartitionKey<TPartitionKey> partitionKey)
        {
            try
            {
                await _container.DeleteItemAsync<TEntity>(
                    id.ToString(),
                    partitionKey.CosmosPartitionKey);
            }
            catch (CosmosException e)
            {
                switch (e.StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        throw DatabaseError.EntityNotFound(id.ToString(), partitionKey.ToString());
                    default:
                        throw;
                }
            }
        }

        private PartitionKey<TPartitionKey> ResolvePartitionKey(TEntity item)
        {
            var partitionKeyValue = ResolvePartitionKeyValue(item);
            return new PartitionKey<TPartitionKey>(partitionKeyValue);
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
            return _container.GetItemQueryIterator<TEntity, TId>(queryParameters, mappingMetadata, queryable);
        }

        private static MappingMetadata? cachedMappingMetadata = null;

        private static MappingMetadata GetMappingMetadata()
        {
            if (cachedMappingMetadata != null)
            {
                return cachedMappingMetadata;
            }

            var mappingMetadata = new MappingMetadata();
            mappingMetadata.InitializeUsingReflection(typeof(TEntity));

            cachedMappingMetadata = mappingMetadata;

            return mappingMetadata;
        }
    }
}
