using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Wemogy.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Repositories;
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

        /// <summary>
        ///     How the client of this container names a member in the document. Resolved once, so a
        ///     patch path is built with the very same rules the serializer applied when it wrote
        ///     the document.
        /// </summary>
        private readonly Func<MemberInfo, string> _serializeMemberName;

        private MappingMetadata? _cachedMappingMetadata;

        public CosmosDatabaseClient(CosmosClient cosmosClient, CosmosDatabaseClientOptions options, ILogger? logger)
        {
            var database = cosmosClient.GetDatabase(options.DatabaseName);
            var containerName = options.ContainerName;
            _container = database.GetContainer(containerName);
            _serializeMemberName = CosmosPatchTranslator.ResolveMemberNameSerializer(cosmosClient);
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
                        partitionKey,
                        hint: typeof(TEntity).Name,
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
                        $"Entity with id {ResolveIdValue(entity)} already exists",
                        cosmosException);
                }

                throw;
            }
        }

        public async Task<TEntity> ReplaceAsync(TEntity entity)
        {
            var id = ResolveIdValue(entity);
            var partitionKeyValue = ResolvePartitionKeyValue(entity);
            var partitionKey = new PartitionKey<string>(partitionKeyValue);

            // entities that opt into optimistic concurrency via [ETag] carry the eTag they
            // were read with; passing it as IfMatch makes Cosmos reject stale writes with a 412
            var eTag = ResolveETagValue(entity);

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

                return replaceResponse.Resource;
            }
            catch (CosmosException cosmosException)
            {
                if (cosmosException.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    throw Error.PreconditionFailed(
                        "EtagMismatch",
                        $"The eTag of the entity with id {id} and partition key {partitionKeyValue} does not match the version in the database",
                        cosmosException);
                }

                if (cosmosException.StatusCode == HttpStatusCode.NotFound)
                {
                    throw DatabaseError.EntityNotFound(
                        id,
                        partitionKeyValue,
                        hint: typeof(TEntity).Name,
                        innerException: cosmosException);
                }

                throw;
            }
        }

        public async Task<TEntity> UpsertAsync(TEntity entity)
        {
            var partitionKey = ResolvePartitionKey(entity);
            var upsertResponse = await _container.UpsertItemAsync(
                entity,
                partitionKey.CosmosPartitionKey,
                new ItemRequestOptions
                {
                    EnableContentResponseOnWrite = true
                });

            return upsertResponse.Resource;
        }

        public async Task<TEntity> UpsertAsync(TEntity entity, string partitionKey)
        {
            var upsertResponse = await _container.UpsertItemAsync(
                entity,
                new PartitionKey<string>(partitionKey).CosmosPartitionKey,
                new ItemRequestOptions
                {
                    EnableContentResponseOnWrite = true
                });

            return upsertResponse.Resource;
        }

        public IDatabaseTransactionalBatch<TEntity> CreateTransactionalBatch(string partitionKey)
        {
            var batch = _container.CreateTransactionalBatch(new PartitionKey<string>(partitionKey).CosmosPartitionKey);

            return new CosmosTransactionalBatch<TEntity>(
                batch,
                _container,
                partitionKey,
                ResolveIdValue,
                ResolvePartitionKeyValue,
                ResolveETagValue,
                _serializeMemberName);
        }

        public async Task<TEntity> PatchAsync(
            string id,
            string partitionKey,
            Action<IPatchOperations<TEntity>> operations,
            Expression<Func<TEntity, bool>>? condition,
            CancellationToken cancellationToken)
        {
            var patchOperations = CosmosPatchTranslator.ToPatchOperations(
                PatchOperationsBuilder<TEntity>.Build(operations),
                _serializeMemberName);
            var filterPredicate = CosmosPatchTranslator.ToFilterPredicate(
                _container,
                condition);

            try
            {
                var patchResponse = await _container.PatchItemAsync<TEntity>(
                    id,
                    new PartitionKey<string>(partitionKey).CosmosPartitionKey,
                    patchOperations,
                    new PatchItemRequestOptions
                    {
                        // the patched document is the return value of this path, e.g. the balance
                        // after an increment, so the write response is worth its request charge
                        EnableContentResponseOnWrite = true,
                        FilterPredicate = filterPredicate
                    },
                    cancellationToken);

                return patchResponse.Resource;
            }
            catch (CosmosException cosmosException)
            {
                // a filter predicate that does not hold is answered with a 412, the same status a
                // stale eTag produces - but a failed condition is deterministic, so it must not be
                // mapped to the exception type the retry proxy retries
                if (cosmosException.StatusCode == HttpStatusCode.PreconditionFailed && condition != null)
                {
                    throw PatchError.ConditionNotMet(
                        id,
                        partitionKey);
                }

                if (cosmosException.StatusCode == HttpStatusCode.NotFound)
                {
                    throw DatabaseError.EntityNotFound(
                        id,
                        partitionKey,
                        hint: typeof(TEntity).Name,
                        innerException: cosmosException);
                }

                // the filter predicate is parsed by a stricter parser than a query and answers a
                // construct it cannot evaluate - arithmetic on document fields, for example - with
                // a bad request. That is a condition the database refuses, not a failed patch
                if (cosmosException.StatusCode == HttpStatusCode.BadRequest && condition != null)
                {
                    throw PatchError.ConditionNotSupported(
                        condition.ToString(),
                        "the database refused the filter predicate it was translated into");
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
                            hint: typeof(TEntity).Name,
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
