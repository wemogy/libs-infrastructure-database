using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Azure.Cosmos;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Cosmos.Client
{
    /// <summary>
    ///     Turns the response of a failed Cosmos <see cref="TransactionalBatch"/> into the error the
    ///     library raises for it, and keeps the per-operation bookkeeping that mapping needs.
    ///     <para>
    ///         Shared by the typed <see cref="CosmosTransactionalBatch{TEntity}"/> and the mixed-type
    ///         <see cref="CosmosPartitionBatch"/>. It lives on its own because the mapping is the
    ///         subtlest part of both - a 412 means two different things and a 400 means two more,
    ///         and only the bookkeeping tells them apart - so the two batches must not each carry
    ///         their own copy of it and drift.
    ///     </para>
    /// </summary>
    internal sealed class CosmosBatchFailureTranslator
    {
        /// <summary>
        ///     The write response of a batch operation is not read, so it is not requested either:
        ///     the entities are not returned to the caller and the payload would only add to the
        ///     request charge.
        /// </summary>
        public static readonly TransactionalBatchItemRequestOptions DefaultItemRequestOptions =
            new TransactionalBatchItemRequestOptions
            {
                EnableContentResponseOnWrite = false
            };

        private readonly PartitionKeyValue _partitionKey;

        /// <summary>
        ///     The id each operation addresses, by operation index. Cosmos reports a failure by
        ///     index, so the id has to be kept to name the entity in the error message.
        /// </summary>
        private readonly List<string> _operationIds = new List<string>();

        /// <summary>
        ///     The type name each operation addresses, by operation index, so a failure can name the
        ///     shape of the document even when the batch mixes types.
        /// </summary>
        private readonly List<string> _operationTypeNames = new List<string>();

        /// <summary>
        ///     The condition of each patch operation, by operation index, null when the patch is
        ///     unconditional. A 412 means "the condition did not hold" for a patch and "the eTag is
        ///     stale" for a replace, and the caller has to be able to tell those apart even when one
        ///     batch carries both.
        /// </summary>
        private readonly Dictionary<int, string?> _patchOperationConditions = new Dictionary<int, string?>();

        public CosmosBatchFailureTranslator(PartitionKeyValue partitionKey)
        {
            _partitionKey = partitionKey;
        }

        /// <summary>
        ///     Records that an operation addressing the given id was added to the batch. Has to be
        ///     called once per operation, in the order they are added, so the bookkeeping indexes
        ///     keep matching the operations of the Cosmos batch.
        /// </summary>
        /// <param name="id">The id the operation addresses</param>
        /// <param name="typeName">The name of the type the operation addresses</param>
        public void RecordOperation(string id, string typeName)
        {
            _operationIds.Add(id);
            _operationTypeNames.Add(typeName);
        }

        /// <summary>
        ///     Records that a patch operation was added to the batch, remembering the condition it
        ///     carried so a 412 or a 400 on it can be told apart from the same status on a replace.
        /// </summary>
        /// <param name="id">The id the operation addresses</param>
        /// <param name="typeName">The name of the type the operation addresses</param>
        /// <param name="condition">The condition the patch carried, null when it was unconditional</param>
        public void RecordPatchOperation(string id, string typeName, string? condition)
        {
            _patchOperationConditions.Add(
                _operationIds.Count,
                condition);
            RecordOperation(
                id,
                typeName);
        }

        /// <summary>
        ///     Returns the error to raise for a batch response that did not succeed.
        /// </summary>
        /// <param name="response">The response of the failed batch</param>
        /// <returns>The error to raise</returns>
        public Exception Translate(TransactionalBatchResponse response)
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

                return Translate(
                    index,
                    result.StatusCode,
                    response.ErrorMessage);
            }

            return TransactionalBatchError.Failed((int)response.StatusCode);
        }

        private Exception Translate(int operationIndex, HttpStatusCode statusCode, string? errorMessage)
        {
            var id = _operationIds[operationIndex];
            var typeName = _operationTypeNames[operationIndex];
            var isPatch = _patchOperationConditions.ContainsKey(operationIndex);
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
                        _partitionKey.ToString(),
                        typeName);

                // the same status covers two different answers: a patch condition that did not
                // hold, and a replace whose eTag is stale. Only an operation that carried a
                // condition can be the former
                case HttpStatusCode.PreconditionFailed when patchCondition != null:
                    return PatchError.ConditionNotMet(
                        operationIndex,
                        id,
                        _partitionKey.ToString());
                case HttpStatusCode.PreconditionFailed:
                    return TransactionalBatchError.ETagMismatch(
                        operationIndex,
                        id,
                        _partitionKey.ToString());

                // a bad request on a patch covers two rejections, the filter predicate and the
                // operations themselves, and only the message of the response tells them apart
                case HttpStatusCode.BadRequest
                    when patchCondition != null && CosmosPatchTranslator.IsFilterPredicateFailure(errorMessage):
                    return PatchError.ConditionNotSupported(
                        patchCondition,
                        "the database refused the filter predicate it was translated into");

                // reported as a patch failure whether or not the patch carried a condition, so it
                // stays the same error the in-memory provider raises for the same cause
                case HttpStatusCode.BadRequest when isPatch:
                    return PatchError.Failed(
                        operationIndex,
                        id,
                        _partitionKey.ToString(),
                        "the database refused the patch");
                default:
                    return TransactionalBatchError.Failed(
                        operationIndex,
                        (int)statusCode);
            }
        }

        /// <summary>
        ///     Returns the condition the patch operation at the given index carried, or null when the
        ///     operation is not a patch or was unconditional. Whether the operation is a patch at all
        ///     is a separate question, answered by the presence of the key.
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
