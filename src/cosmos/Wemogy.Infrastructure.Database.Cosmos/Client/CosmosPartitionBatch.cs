using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Cosmos.Client
{
    /// <summary>
    ///     Cosmos DB implementation of a mixed-type partition batch, backed by the native
    ///     <see cref="TransactionalBatch"/> of the Cosmos SDK. The SDK's batch is untyped already, so
    ///     every operation records its own type; the only per-type work is resolving the id and eTag
    ///     of the document, which <see cref="EntityMetadata{T}"/> caches.
    /// </summary>
    public class CosmosPartitionBatch : DatabasePartitionBatchBase
    {
        private readonly TransactionalBatch _batch;
        private readonly Container _container;
        private readonly Func<MemberInfo, string> _serializeMemberName;
        private readonly CosmosBatchFailureTranslator _failureTranslator;

        /// <summary>
        ///     Initializes a new instance of the <see cref="CosmosPartitionBatch"/> class.
        /// </summary>
        /// <param name="batch">The Cosmos batch to record the operations in</param>
        /// <param name="container">The container the batch runs against, used to translate a patch condition</param>
        /// <param name="partitionKey">The logical partition every operation of the batch acts on</param>
        /// <param name="serializeMemberName">Returns how a member is named in the document</param>
        public CosmosPartitionBatch(
            TransactionalBatch batch,
            Container container,
            PartitionKeyValue partitionKey,
            Func<MemberInfo, string> serializeMemberName)
            : base(partitionKey)
        {
            _batch = batch;
            _container = container;
            _serializeMemberName = serializeMemberName;
            _failureTranslator = new CosmosBatchFailureTranslator(partitionKey);
        }

        /// <inheritdoc />
        protected override void ApplyCreate<T>(T entity)
        {
            RecordOperation<T>(EntityMetadata<T>.ResolveId(entity));
            _batch.CreateItem(
                entity,
                CosmosBatchFailureTranslator.DefaultItemRequestOptions);
        }

        /// <inheritdoc />
        protected override void ApplyReplace<T>(T entity)
        {
            var id = EntityMetadata<T>.ResolveId(entity);
            RecordOperation<T>(id);

            // entities that opt into optimistic concurrency via [ETag] carry the eTag they were
            // read with; passing it as IfMatch makes Cosmos reject a stale write with a 412, which
            // fails the whole batch
            var eTag = EntityMetadata<T>.ResolveETag(entity);

            _batch.ReplaceItem(
                id,
                entity,
                new TransactionalBatchItemRequestOptions
                {
                    EnableContentResponseOnWrite = false,
                    IfMatchEtag = eTag
                });
        }

        /// <inheritdoc />
        protected override void ApplyUpsert<T>(T entity)
        {
            RecordOperation<T>(EntityMetadata<T>.ResolveId(entity));
            _batch.UpsertItem(
                entity,
                CosmosBatchFailureTranslator.DefaultItemRequestOptions);
        }

        /// <inheritdoc />
        protected override void ApplyDelete<T>(string id)
        {
            RecordOperation<T>(id);
            _batch.DeleteItem(
                id,
                CosmosBatchFailureTranslator.DefaultItemRequestOptions);
        }

        /// <inheritdoc />
        protected override void ApplyPatch<T>(
            string id,
            IReadOnlyList<DatabasePatchOperation> operations,
            Expression<Func<T, bool>>? condition)
        {
            // translated first: a refused path or a condition the provider cannot express throws
            // here, and an operation that was never recorded must not leave an id behind - the
            // indexes of the bookkeeping have to keep matching the operations of the batch
            var patchOperations = CosmosPatchTranslator.ToPatchOperations(
                operations,
                _serializeMemberName);
            var filterPredicate = CosmosPatchTranslator.ToFilterPredicate(
                _container,
                condition);

            _failureTranslator.RecordPatchOperation(
                id,
                typeof(T).Name,
                condition?.ToString());

            _batch.PatchItem(
                id,
                patchOperations,
                new TransactionalBatchPatchItemRequestOptions
                {
                    EnableContentResponseOnWrite = false,
                    FilterPredicate = filterPredicate
                });
        }

        /// <inheritdoc />
        protected override async Task ExecuteCoreAsync(CancellationToken cancellationToken)
        {
            using var response = await _batch.ExecuteAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return;
            }

            throw _failureTranslator.Translate(response);
        }

        private void RecordOperation<T>(string id)
        {
            _failureTranslator.RecordOperation(
                id,
                typeof(T).Name);
        }
    }
}
