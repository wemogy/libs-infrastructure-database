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
    ///     Cosmos DB implementation of a transactional batch, backed by the native
    ///     <see cref="TransactionalBatch"/> of the Cosmos SDK.
    /// </summary>
    /// <typeparam name="TEntity">The entity type every operation of the batch acts on</typeparam>
    public class CosmosTransactionalBatch<TEntity> : DatabaseTransactionalBatchBase<TEntity>
        where TEntity : class
    {
        private readonly TransactionalBatch _batch;
        private readonly Container _container;
        private readonly Func<TEntity, string> _resolveIdValue;
        private readonly Func<TEntity, string?> _resolveETagValue;
        private readonly Func<MemberInfo, string> _serializeMemberName;
        private readonly CosmosBatchFailureTranslator _failureTranslator;

        /// <summary>
        ///     Initializes a new instance of the <see cref="CosmosTransactionalBatch{TEntity}"/> class.
        /// </summary>
        /// <param name="batch">The Cosmos batch to record the operations in</param>
        /// <param name="container">The container the batch runs against, used to translate a patch condition</param>
        /// <param name="partitionKey">The logical partition every operation of the batch acts on</param>
        /// <param name="resolveIdValue">Reads the id value of an entity</param>
        /// <param name="resolvePartitionKey">Reads the partition key of an entity</param>
        /// <param name="resolveETagValue">Reads the eTag value of an entity, null if it does not opt into optimistic concurrency</param>
        /// <param name="serializeMemberName">Returns how a member is named in the document</param>
        public CosmosTransactionalBatch(
            TransactionalBatch batch,
            Container container,
            PartitionKeyValue partitionKey,
            Func<TEntity, string> resolveIdValue,
            Func<TEntity, PartitionKeyValue> resolvePartitionKey,
            Func<TEntity, string?> resolveETagValue,
            Func<MemberInfo, string> serializeMemberName)
            : base(partitionKey, resolvePartitionKey)
        {
            _batch = batch;
            _container = container;
            _resolveIdValue = resolveIdValue;
            _resolveETagValue = resolveETagValue;
            _serializeMemberName = serializeMemberName;
            _failureTranslator = new CosmosBatchFailureTranslator(partitionKey);
        }

        /// <inheritdoc />
        protected override void ApplyCreate(TEntity entity)
        {
            RecordOperation(_resolveIdValue(entity));
            _batch.CreateItem(
                entity,
                CosmosBatchFailureTranslator.DefaultItemRequestOptions);
        }

        /// <inheritdoc />
        protected override void ApplyReplace(TEntity entity)
        {
            var id = _resolveIdValue(entity);
            RecordOperation(id);

            // entities that opt into optimistic concurrency via [ETag] carry the eTag they were
            // read with; passing it as IfMatch makes Cosmos reject a stale write with a 412, which
            // fails the whole batch
            var eTag = _resolveETagValue(entity);

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
        protected override void ApplyUpsert(TEntity entity)
        {
            RecordOperation(_resolveIdValue(entity));
            _batch.UpsertItem(
                entity,
                CosmosBatchFailureTranslator.DefaultItemRequestOptions);
        }

        /// <inheritdoc />
        protected override void ApplyDelete(string id)
        {
            RecordOperation(id);
            _batch.DeleteItem(
                id,
                CosmosBatchFailureTranslator.DefaultItemRequestOptions);
        }

        /// <inheritdoc />
        protected override void ApplyPatch(
            string id,
            IReadOnlyList<DatabasePatchOperation> operations,
            Expression<Func<TEntity, bool>>? condition)
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
                typeof(TEntity).Name,
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

        private void RecordOperation(string id)
        {
            _failureTranslator.RecordOperation(
                id,
                typeof(TEntity).Name);
        }
    }
}
