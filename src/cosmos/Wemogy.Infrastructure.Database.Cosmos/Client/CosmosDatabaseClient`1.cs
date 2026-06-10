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

                var entity = itemResponse.Resource;
                SetETagValue(entity, itemResponse.ETag);
                return entity;
            }
            catch (CosmosException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    throw DatabaseError.EntityNotFound(
                        id,
                        partitionKey,
                        innerException: e);
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
            Sorting<TEntity>? sorting,
            Pagination? pagination,
            Func<TEntity, Task> callback,
            CancellationToken cancellationToken = default)
        {
            var queryable = _container.GetItemLinqQueryable<TEntity>()
                .Where(predicate);

            if (sorting != null)
            {
                queryable = sorting.ApplyTo(queryable);
            }

            if (pagination != null)
            {
                queryable = queryable
                    .Skip(pagination.Skip)
                    .Take(pagination.Take);
            }

            var feedIterator = queryable.ToFeedIterator();

            return feedIterator.IterateAsync(
                callback,
                cancellationToken);
        }

        public async Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken)
        {
            var queryable = _container.GetItemLinqQueryable<TEntity>()
                .Where(predicate);

            var response = await queryable
                .CountAsync(cancellationToken);

            return response.Resource;
        }

        public async Task<TEntity> CreateAsync(TEntity entity)
        {
            var partitionKey = ResolvePartitionKey(entity);

            // never persist an eTag into the document body — queries would
            // deserialize a stale value and cause false 412s on later replaces
            var eTag = ResolveETagValue(entity);
            SetETagValue(entity, null);

            try
            {
                var createResponse = await _container.CreateItemAsync(
                    entity,
                    partitionKey.CosmosPartitionKey,
                    new ItemRequestOptions
                    {
                        EnableContentResponseOnWrite = true
                    });

                var createdEntity = createResponse.Resource;
                SetETagValue(createdEntity, createResponse.ETag);
                SetETagValue(entity, createResponse.ETag);
                return createdEntity;
            }
            catch (CosmosException cosmosException)
            {
                SetETagValue(entity, eTag);

                if (cosmosException.StatusCode == HttpStatusCode.Conflict)
                {
                    throw Error.Conflict(
                        "AlreadyExists",
                        $"Entity with id {ResolveIdValue(entity)} already exists",
                        cosmosException);
                }

                throw;
            }
        }

        public async Task<TEntity> ReplaceAsync(TEntity entity)
        {
            var id = ResolveIdValue(entity);
            var partitionKey = ResolvePartitionKey(entity);

            // never persist an eTag into the document body — queries would
            // deserialize a stale value and cause false 412s on later replaces
            var eTag = ResolveETagValue(entity);
            SetETagValue(entity, null);

            try
            {
                var replaceResponse = await _container.ReplaceItemAsync(
                    entity,
                    id,
                    partitionKey.CosmosPartitionKey,
                    new ItemRequestOptions
                    {
                        IfMatchEtag = eTag
                    });

                var replacedEntity = replaceResponse.Resource;
                SetETagValue(replacedEntity, replaceResponse.ETag);
                SetETagValue(entity, replaceResponse.ETag);
                return replacedEntity;
            }
            catch (CosmosException cosmosException)
            {
                // restore the guard, otherwise a caller-level retry with this
                // instance would silently fall back to an unconditional replace
                SetETagValue(entity, eTag);

                if (cosmosException.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    throw Error.PreconditionFailed(
                        "EtagMismatch",
                        $"The eTag of the entity with id {id} does not match the version in the database",
                        cosmosException);
                }

                if (cosmosException.StatusCode == HttpStatusCode.NotFound)
                {
                    throw DatabaseError.EntityNotFound(
                        id,
                        ResolvePartitionKeyValue(entity),
                        innerException: cosmosException);
                }

                throw;
            }
        }

        public async Task<TEntity> UpsertAsync(TEntity entity)
        {
            var partitionKey = ResolvePartitionKey(entity);

            // upserts stay unconditional by design, but the eTag must still
            // not be persisted into the document body
            var eTag = ResolveETagValue(entity);
            SetETagValue(entity, null);

            try
            {
                var upsertResponse = await _container.UpsertItemAsync(
                    entity,
                    partitionKey.CosmosPartitionKey,
                    new ItemRequestOptions
                    {
                        EnableContentResponseOnWrite = true
                    });

                var upsertedEntity = upsertResponse.Resource;
                SetETagValue(upsertedEntity, upsertResponse.ETag);
                SetETagValue(entity, upsertResponse.ETag);
                return upsertedEntity;
            }
            catch
            {
                SetETagValue(entity, eTag);
                throw;
            }
        }

        public async Task<TEntity> UpsertAsync(TEntity entity, string partitionKey)
        {
            // upserts stay unconditional by design, but the eTag must still
            // not be persisted into the document body
            var eTag = ResolveETagValue(entity);
            SetETagValue(entity, null);

            try
            {
                var upsertResponse = await _container.UpsertItemAsync(
                    entity,
                    new PartitionKey<string>(partitionKey).CosmosPartitionKey,
                    new ItemRequestOptions
                    {
                        EnableContentResponseOnWrite = true
                    });

                var upsertedEntity = upsertResponse.Resource;
                SetETagValue(upsertedEntity, upsertResponse.ETag);
                SetETagValue(entity, upsertResponse.ETag);
                return upsertedEntity;
            }
            catch
            {
                SetETagValue(entity, eTag);
                throw;
            }
        }

        public Task DeleteAsync(string id, string partitionKey)
        {
            return DeleteAsync(
                id,
                new PartitionKey<string>(partitionKey));
        }

        public Task DeleteAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return IterateAsync(
                predicate,
                null,
                null,
                async entity =>
                {
                    var id = ResolveIdValue(entity);
                    var partitionKey = ResolvePartitionKey(entity);
                    await DeleteAsync(
                        id,
                        partitionKey);
                });
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
                            partitionKey.ToString(),
                            innerException: e);
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
