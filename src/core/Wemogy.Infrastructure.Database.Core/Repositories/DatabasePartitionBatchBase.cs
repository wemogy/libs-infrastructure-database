using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

/// <summary>
///     Base class of the provider implementations of <see cref="IDatabasePartitionBatch"/>. It owns
///     the validation every provider has to apply, so the providers cannot drift apart: a provider
///     only contributes how an operation is recorded and how the batch is executed.
/// </summary>
public abstract class DatabasePartitionBatchBase : IDatabasePartitionBatch
{
    /// <inheritdoc cref="TransactionalBatchLimits.MaxOperationCount"/>
    public const int MaxOperationCount = TransactionalBatchLimits.MaxOperationCount;

    private bool _executed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DatabasePartitionBatchBase"/> class.
    /// </summary>
    /// <param name="partitionKey">The logical partition every operation of the batch acts on</param>
    protected DatabasePartitionBatchBase(PartitionKeyValue partitionKey)
    {
        PartitionKey = partitionKey;
    }

    /// <summary>
    ///     The logical partition every operation of the batch acts on.
    /// </summary>
    protected PartitionKeyValue PartitionKey { get; }

    /// <inheritdoc />
    public int OperationCount { get; private set; }

    /// <inheritdoc />
    public IDatabasePartitionBatch Create<T>(T entity)
        where T : class
    {
        EnsureNotExecuted();
        EnsureCapacity();
        EnsureSamePartition(entity);
        FixedPointMetadata.EnsureValuesAreValid(entity);
        ApplyCreate(entity);
        OperationCount++;
        return this;
    }

    /// <inheritdoc />
    public IDatabasePartitionBatch Replace<T>(T entity)
        where T : class
    {
        EnsureNotExecuted();
        EnsureCapacity();
        EnsureSamePartition(entity);
        FixedPointMetadata.EnsureValuesAreValid(entity);
        ApplyReplace(entity);
        OperationCount++;
        return this;
    }

    /// <inheritdoc />
    public IDatabasePartitionBatch Upsert<T>(T entity)
        where T : class
    {
        EnsureNotExecuted();
        EnsureCapacity();
        EnsureSamePartition(entity);
        FixedPointMetadata.EnsureValuesAreValid(entity);
        ApplyUpsert(entity);
        OperationCount++;
        return this;
    }

    /// <inheritdoc />
    public IDatabasePartitionBatch Delete<T>(string id)
        where T : class
    {
        EnsureNotExecuted();
        EnsureCapacity();

        // a delete addresses an id, so there is no entity whose partition could be compared - but
        // the type still has to be partitioned as deeply as the batch's key, or the operation would
        // address a partition the type cannot express
        EntityMetadata<T>.EnsurePartitionKeyDepth(PartitionKey);
        ApplyDelete<T>(id);
        OperationCount++;
        return this;
    }

    /// <inheritdoc />
    public IDatabasePartitionBatch Patch<T>(
        string id,
        Action<IPatchOperations<T>> operations,
        Expression<Func<T, bool>>? condition = null)
        where T : class
    {
        EnsureNotExecuted();
        EnsureCapacity();

        // a patch addresses an id, like a delete, so there is no entity to check the partition of -
        // only that the type is partitioned as deeply as the batch's key
        EntityMetadata<T>.EnsurePartitionKeyDepth(PartitionKey);
        ApplyPatch(
            id,
            PatchOperationsBuilder<T>.Build(operations),
            condition);
        OperationCount++;
        return this;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // a batch is single-use: the providers consume their recorded operations, so a second
        // execution would either replay every write or silently do nothing, depending on the
        // provider. Failing instead keeps both providers on the same, predictable semantics
        EnsureNotExecuted();
        _executed = true;

        // an empty batch is a no-op instead of an error: a caller that collects operations in a
        // loop should not have to guard the execution with a count check
        if (OperationCount == 0)
        {
            return;
        }

        // awaited instead of returned, so a provider that applies its operations synchronously
        // faults the returned task like the asynchronous ones instead of throwing before it
        await ExecuteCoreAsync(cancellationToken);
    }

    /// <summary>
    ///     Records a create operation. Called after the operation has been validated.
    /// </summary>
    /// <param name="entity">The entity to create</param>
    /// <typeparam name="T">The type of the entity</typeparam>
    protected abstract void ApplyCreate<T>(T entity)
        where T : class;

    /// <summary>
    ///     Records a replace operation. Called after the operation has been validated.
    /// </summary>
    /// <param name="entity">The updated entity which will replace the existing one</param>
    /// <typeparam name="T">The type of the entity</typeparam>
    protected abstract void ApplyReplace<T>(T entity)
        where T : class;

    /// <summary>
    ///     Records an upsert operation. Called after the operation has been validated.
    /// </summary>
    /// <param name="entity">The entity to insert or update</param>
    /// <typeparam name="T">The type of the entity</typeparam>
    protected abstract void ApplyUpsert<T>(T entity)
        where T : class;

    /// <summary>
    ///     Records a delete operation. Called after the operation has been validated.
    /// </summary>
    /// <param name="id">The id of the entity to delete</param>
    /// <typeparam name="T">The type of the entity to delete</typeparam>
    protected abstract void ApplyDelete<T>(string id)
        where T : class;

    /// <summary>
    ///     Records a patch operation. Called after the operations have been validated.
    /// </summary>
    /// <param name="id">The id of the document to patch</param>
    /// <param name="operations">The validated operations to apply</param>
    /// <param name="condition">An optional condition that has to hold for the patch to be applied</param>
    /// <typeparam name="T">The type of the document to patch</typeparam>
    protected abstract void ApplyPatch<T>(
        string id,
        IReadOnlyList<DatabasePatchOperation> operations,
        Expression<Func<T, bool>>? condition)
        where T : class;

    /// <summary>
    ///     Executes the recorded operations atomically. Only called when the batch holds at least
    ///     one operation.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the execution</param>
    /// <returns>A task that completes when every operation has been applied</returns>
    protected abstract Task ExecuteCoreAsync(CancellationToken cancellationToken);

    private void EnsureNotExecuted()
    {
        if (_executed)
        {
            throw TransactionalBatchError.AlreadyExecuted();
        }
    }

    private void EnsureCapacity()
    {
        if (OperationCount >= MaxOperationCount)
        {
            throw TransactionalBatchError.OperationLimitExceeded(MaxOperationCount);
        }
    }

    private void EnsureSamePartition<T>(T entity)
        where T : class
    {
        // checked before the values are compared, so a type that is not partitioned as deeply as
        // the batch's key is named as such instead of reported as a mismatch of values
        EntityMetadata<T>.EnsurePartitionKeyDepth(PartitionKey);

        var entityPartitionKey = EntityMetadata<T>.ResolvePartitionKey(entity);

        // compared by value across every component: a batch is limited to one logical partition,
        // and for a hierarchical key that means the whole hierarchy has to match, not just its head
        if (entityPartitionKey != PartitionKey)
        {
            throw TransactionalBatchError.PartitionKeyMismatch(
                entityPartitionKey.ToString(),
                PartitionKey.ToString(),
                typeof(T).Name);
        }
    }
}
