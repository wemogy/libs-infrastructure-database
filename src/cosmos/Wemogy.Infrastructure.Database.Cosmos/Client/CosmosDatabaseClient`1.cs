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
using Wemogy.Infrastructure.Database.Core.Delegates;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.Cosmos.Extensions;
using Wemogy.Infrastructure.Database.Cosmos.Models;
using Wemogy.Infrastructure.Database.Cosmos.Query;

namespace Wemogy.Infrastructure.Database.Cosmos.Client
{
    public partial class CosmosDatabaseClient<TEntity> : DatabaseClientBase<TEntity>, IDatabaseClient<TEntity>
        where TEntity : class
    {
        private readonly ILogger? _logger;
        private readonly Container _container;
        private readonly Container _leaseContainer;
        private readonly CosmosDatabaseClientOptions _options;

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

            // resolved eagerly like the monitored container: neither call reaches the service, so a
            // client whose repository never reads the change feed pays nothing for this
            _leaseContainer = database.GetContainer(options.LeaseContainerName);
            _options = options;
            _serializeMemberName = CosmosPatchTranslator.ResolveMemberNameSerializer(cosmosClient);
            _logger = logger;
        }

        public async Task<TEntity> GetAsync(string id, PartitionKeyValue partitionKey, CancellationToken cancellationToken)
        {
            EnsurePartitionKeyDepth(partitionKey);

            try
            {
                var itemResponse = await _container.ReadItemAsync<TEntity>(
                    id,
                    partitionKey.ToCosmosPartitionKey(),
                    cancellationToken: cancellationToken);

                return itemResponse;
            }
            catch (CosmosException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    throw DatabaseError.EntityNotFound(
                        id,
                        partitionKey.ToString(),
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
                .Where(ToStoredPredicate(predicate));

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
                .Where(ToStoredPredicate(predicate));

            var response = await queryable
                .CountAsync(cancellationToken);

            return response.Resource;
        }

        public async Task<TEntity> CreateAsync(TEntity entity)
        {
            EnsureFixedPointValuesAreValid(entity);
            var partitionKey = ResolvePartitionKey(entity);
            try
            {
                var createResponse = await _container.CreateItemAsync(
                    entity,
                    partitionKey.ToCosmosPartitionKey(),
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
            EnsureFixedPointValuesAreValid(entity);
            var id = ResolveIdValue(entity);
            var partitionKey = ResolvePartitionKey(entity);

            // entities that opt into optimistic concurrency via [ETag] carry the eTag they
            // were read with; passing it as IfMatch makes Cosmos reject stale writes with a 412
            var eTag = ResolveETagValue(entity);

            try
            {
                var replaceResponse = await _container.ReplaceItemAsync(
                    entity,
                    id,
                    partitionKey.ToCosmosPartitionKey(),
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
                        $"The eTag of the entity with id {id} and partition key {partitionKey} does not match the version in the database",
                        cosmosException);
                }

                if (cosmosException.StatusCode == HttpStatusCode.NotFound)
                {
                    throw DatabaseError.EntityNotFound(
                        id,
                        partitionKey.ToString(),
                        hint: typeof(TEntity).Name,
                        innerException: cosmosException);
                }

                throw;
            }
        }

        public async Task<TEntity> UpsertAsync(TEntity entity)
        {
            EnsureFixedPointValuesAreValid(entity);
            var partitionKey = ResolvePartitionKey(entity);
            var upsertResponse = await _container.UpsertItemAsync(
                entity,
                partitionKey.ToCosmosPartitionKey(),
                new ItemRequestOptions
                {
                    EnableContentResponseOnWrite = true
                });

            return upsertResponse.Resource;
        }

        public async Task<TEntity> UpsertAsync(TEntity entity, PartitionKeyValue partitionKey)
        {
            EnsureFixedPointValuesAreValid(entity);
            EnsurePartitionKeyDepth(partitionKey);

            var upsertResponse = await _container.UpsertItemAsync(
                entity,
                partitionKey.ToCosmosPartitionKey(),
                new ItemRequestOptions
                {
                    EnableContentResponseOnWrite = true
                });

            return upsertResponse.Resource;
        }

        public IDatabaseTransactionalBatch<TEntity> CreateTransactionalBatch(PartitionKeyValue partitionKey)
        {
            EnsurePartitionKeyDepth(partitionKey);

            var batch = _container.CreateTransactionalBatch(partitionKey.ToCosmosPartitionKey());

            return new CosmosTransactionalBatch<TEntity>(
                batch,
                _container,
                partitionKey,
                ResolveIdValue,
                ResolvePartitionKey,
                ResolveETagValue,
                _serializeMemberName);
        }

        public IDatabasePartitionBatch CreatePartitionBatch(PartitionKeyValue partitionKey)
        {
            EnsurePartitionKeyDepth(partitionKey);

            var batch = _container.CreateTransactionalBatch(partitionKey.ToCosmosPartitionKey());

            return new CosmosPartitionBatch(
                batch,
                _container,
                partitionKey,
                _serializeMemberName);
        }

        public async Task<TEntity> PatchAsync(
            string id,
            PartitionKeyValue partitionKey,
            Action<IPatchOperations<TEntity>> operations,
            Expression<Func<TEntity, bool>>? condition,
            CancellationToken cancellationToken)
        {
            EnsurePartitionKeyDepth(partitionKey);

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
                    partitionKey.ToCosmosPartitionKey(),
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
                        partitionKey.ToString());
                }

                if (cosmosException.StatusCode == HttpStatusCode.NotFound)
                {
                    throw DatabaseError.EntityNotFound(
                        id,
                        partitionKey.ToString(),
                        hint: typeof(TEntity).Name,
                        innerException: cosmosException);
                }

                if (cosmosException.StatusCode == HttpStatusCode.BadRequest)
                {
                    // a bad request covers two different rejections: the filter predicate, which
                    // is parsed by a stricter parser than a query and refuses e.g. arithmetic on
                    // document fields, and the operations themselves, e.g. a path through an object
                    // the document does not carry. Only the message tells them apart
                    if (condition != null &&
                        CosmosPatchTranslator.IsFilterPredicateFailure(cosmosException.Message))
                    {
                        throw PatchError.ConditionNotSupported(
                            condition.ToString(),
                            "the database refused the filter predicate it was translated into");
                    }

                    // surfaced through the shared error instead of letting the provider exception
                    // out, which is what the in-memory provider does for the same cause
                    throw PatchError.Failed(
                        id,
                        partitionKey.ToString(),
                        "the database refused the patch");
                }

                throw;
            }
        }

        public Task DeleteAsync(string id, PartitionKeyValue partitionKey)
        {
            EnsurePartitionKeyDepth(partitionKey);

            return DeleteItemAsync(
                id,
                partitionKey);
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
                    await DeleteItemAsync(
                        id,
                        ResolvePartitionKey(entity));
                });
        }

        /// <summary>
        ///     Returns the predicate as it has to read against the stored document: a member marked
        ///     with the <see cref="Core.Attributes.FixedPointAttribute"/> is persisted as a scaled
        ///     integer, so every value it is compared against is scaled by the same factor. Without
        ///     it a query would compare <c>0.5</c> against the <c>500000</c> the document carries
        ///     and quietly return the wrong rows.
        /// </summary>
        private static Expression<Func<TEntity, bool>> ToStoredPredicate(Expression<Func<TEntity, bool>> predicate)
        {
            return FixedPointPredicateRewriter.Rewrite(predicate)!;
        }

        private async Task DeleteItemAsync(string id, PartitionKeyValue partitionKey)
        {
            try
            {
                await _container.DeleteItemAsync<TEntity>(
                    id,
                    partitionKey.ToCosmosPartitionKey());
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

        private FeedIterator<TEntity> GetFeedIterator(
            QueryParameters queryParameters,
            Expression<Func<TEntity, bool>>? generalFilterPredicate)
        {
            IQueryable<TEntity> queryable = _container.GetItemLinqQueryable<TEntity>();
            if (generalFilterPredicate != null)
            {
                queryable = queryable.Where(ToStoredPredicate(generalFilterPredicate));
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
            mappingMetadata.InitializeUsingReflection(
                typeof(TEntity),
                _serializeMemberName);

            _cachedMappingMetadata = mappingMetadata;

            return mappingMetadata;
        }
    }
}
