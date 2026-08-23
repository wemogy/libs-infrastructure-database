using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Repositories;

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
        /// <summary>
        ///     The write response of a batch operation is not read, so it is not requested either:
        ///     the entities are not returned to the caller and the payload would only add to the
        ///     request charge.
        /// </summary>
        private static readonly TransactionalBatchItemRequestOptions DefaultItemRequestOptions =
            new TransactionalBatchItemRequestOptions
            {
                EnableContentResponseOnWrite = false
            };

        private readonly TransactionalBatch _batch;
        private readonly Container _container;
        private readonly Func<TEntity, string> _resolveIdValue;
        private readonly Func<TEntity, string?> _resolveETagValue;
        private readonly Func<MemberInfo, string> _serializeMemberName;

        /// <summary>
        ///     The id each operation addresses, by operation index. Cosmos reports a failure by
        ///     index, so the id has to be kept to name the entity in the error message.
        /// </summary>
        private readonly List<string> _operationIds = new List<string>();

        /// <summary>
        ///     The condition of each patch operation, by operation index, null when the patch is
        ///     unconditional. A 412 means "the condition did not hold" for a patch and "the eTag is
        ///     stale" for a replace, and the caller has to be able to tell those apart even when
        ///     one batch carries both.
        /// </summary>
        private readonly Dictionary<int, string?> _patchOperationConditions = new Dictionary<int, string?>();

        /// <summary>
        ///     Initializes a new instance of the <see cref="CosmosTransactionalBatch{TEntity}"/> class.
        /// </summary>
        /// <param name="batch">The Cosmos batch to record the operations in</param>
        /// <param name="container">The container the batch runs against, used to translate a patch condition</param>
        /// <param name="partitionKey">The logical partition every operation of the batch acts on</param>
        /// <param name="resolveIdValue">Reads the id value of an entity</param>
        /// <param name="resolvePartitionKeyValue">Reads the partition key value of an entity</param>
        /// <param name="resolveETagValue">Reads the eTag value of an entity, null if it does not opt into optimistic concurrency</param>
        /// <param name="serializeMemberName">Returns how a member is named in the document</param>
        public CosmosTransactionalBatch(
            TransactionalBatch batch,
            Container container,
            string partitionKey,
            Func<TEntity, string> resolveIdValue,
            Func<TEntity, string> resolvePartitionKeyValue,
            Func<TEntity, string?> resolveETagValue,
            Func<MemberInfo, string> serializeMemberName)
            : base(partitionKey, resolvePartitionKeyValue)
        {
            _batch = batch;
            _container = container;
            _resolveIdValue = resolveIdValue;
            _resolveETagValue = resolveETagValue;
            _serializeMemberName = serializeMemberName;
        }

        /// <inheritdoc />
        protected override void ApplyCreate(TEntity entity)
        {
            _operationIds.Add(_resolveIdValue(entity));
            _batch.CreateItem(
                entity,
                DefaultItemRequestOptions);
        }

        /// <inheritdoc />
        protected override void ApplyReplace(TEntity entity)
        {
            var id = _resolveIdValue(entity);
            _operationIds.Add(id);

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
            _operationIds.Add(_resolveIdValue(entity));
            _batch.UpsertItem(
                entity,
                DefaultItemRequestOptions);
        }

        /// <inheritdoc />
        protected override void ApplyDelete(string id)
        {
            _operationIds.Add(id);
            _batch.DeleteItem(
                id,
                DefaultItemRequestOptions);
        }

        /// <inheritdoc />
        protected override void ApplyPatch(
            string id,
            IReadOnlyList<DatabasePatchOperation> operations,
            Expression<Func<TEntity, bool>>? condition)
        {
            _patchOperationConditions.Add(
                _operationIds.Count,
                condition?.ToString());
            _operationIds.Add(id);

            _batch.PatchItem(
                id,
                CosmosPatchTranslator.ToPatchOperations(
                    operations,
                    _serializeMemberName),
                new TransactionalBatchPatchItemRequestOptions
                {
                    EnableContentResponseOnWrite = false,
                    FilterPredicate = CosmosPatchTranslator.ToFilterPredicate(
                        _container,
                        condition)
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

            throw TranslateFailure(response);
        }

        private Exception TranslateFailure(TransactionalBatchResponse response)
        {
            // when one operation fails, Cosmos rejects every other operation of the batch with a
            // 424 FailedDependency, so the first result that is neither a success nor a 424 is the
            // one that actually failed
            for (var index = 0; index < response.Count; index++)
            {
                var result = response[index];
                if (result.IsSuccessStatusCode || result.StatusCode == HttpStatusCode.FailedDependency)
                {
                    continue;
                }

                return TranslateFailure(
                    index,
                    result.StatusCode);
            }

            return TransactionalBatchError.Failed((int)response.StatusCode);
        }

        private Exception TranslateFailure(int operationIndex, HttpStatusCode statusCode)
        {
            var id = _operationIds[operationIndex];
            var patchCondition = ResolvePatchCondition(operationIndex);

            switch (statusCode)
            {
                case HttpStatusCode.Conflict:
                    return TransactionalBatchError.AlreadyExists(
                        operationIndex,
                        id);
                case HttpStatusCode.NotFound:
                    return TransactionalBatchError.EntityNotFound(
                        operationIndex,
                        id,
                        PartitionKey,
                        typeof(TEntity).Name);
                // the same status covers two different answers: a patch condition that did not
                // hold, and a replace whose eTag is stale. Only an operation that carried a
                // condition can be the former
                case HttpStatusCode.PreconditionFailed when patchCondition != null:
                    return PatchError.ConditionNotMet(
                        operationIndex,
                        id,
                        PartitionKey);
                case HttpStatusCode.PreconditionFailed:
                    return TransactionalBatchError.ETagMismatch(
                        operationIndex,
                        id,
                        PartitionKey);

                // a filter predicate the database cannot evaluate, e.g. one doing arithmetic on
                // document fields
                case HttpStatusCode.BadRequest when patchCondition != null:
                    return PatchError.ConditionNotSupported(
                        patchCondition,
                        "the database refused the filter predicate it was translated into");
                default:
                    return TransactionalBatchError.Failed(
                        operationIndex,
                        (int)statusCode);
            }
        }

        /// <summary>
        ///     Returns the condition the patch operation at the given index carried, or null when
        ///     the operation is not a patch or was unconditional.
        /// </summary>
        private string? ResolvePatchCondition(int operationIndex)
        {
            return _patchOperationConditions.TryGetValue(
                operationIndex,
                out var condition)
                ? condition
                : null;
        }
    }
}
