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
///     Base class of the provider implementations of <see cref="IDatabaseTransactionalBatch{TEntity}"/>.
///     It owns the validation every provider has to apply, so the providers cannot drift apart:
///     a provider only contributes how an operation is recorded and how the batch is executed.
/// </summary>
/// <typeparam name="TEntity">The entity type every operation of the batch acts on</typeparam>
public abstract class DatabaseTransactionalBatchBase<TEntity> : IDatabaseTransactionalBatch<TEntity>
    where TEntity : class
{
    /// <summary>
    ///     Cosmos DB caps a transactional batch at 100 operations. The cap is enforced for every
    ///     provider, so a batch that runs against the in-memory provider in a test cannot be
    ///     larger than one that runs against Cosmos DB in production.
    /// </summary>
    public const int MaxOperationCount = 100;

    private readonly Func<TEntity, PartitionKeyValue> _resolvePartitionKey;

    private bool _executed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DatabaseTransactionalBatchBase{TEntity}"/> class.
    /// </summary>
    /// <param name="partitionKey">The logical partition every operation of the batch acts on</param>
    /// <param name="resolvePartitionKey">Reads the partition key of an entity</param>
    protected DatabaseTransactionalBatchBase(
        PartitionKeyValue partitionKey,
        Func<TEntity, PartitionKeyValue> resolvePartitionKey)
    {
        PartitionKey = partitionKey;
        _resolvePartitionKey = resolvePartitionKey;
    }

    /// <summary>
    ///     The logical partition every operation of the batch acts on.
    /// </summary>
    protected PartitionKeyValue PartitionKey { get; }

    /// <inheritdoc />
    public int OperationCount { get; private set; }

    /// <inheritdoc />
    public IDatabaseTransactionalBatch<TEntity> Create(TEntity entity)
    {
        EnsureNotExecuted();
        EnsureCapacity();
        EnsureSamePartition(entity);
        ApplyCreate(entity);
        OperationCount++;
        return this;
    }

    /// <inheritdoc />
    public IDatabaseTransactionalBatch<TEntity> Replace(TEntity entity)
    {
        EnsureNotExecuted();
        EnsureCapacity();
        EnsureSamePartition(entity);
        ApplyReplace(entity);
        OperationCount++;
        return this;
    }

    /// <inheritdoc />
    public IDatabaseTransactionalBatch<TEntity> Upsert(TEntity entity)
    {
        EnsureNotExecuted();
        EnsureCapacity();
        EnsureSamePartition(entity);
        ApplyUpsert(entity);
        OperationCount++;
        return this;
    }

    /// <inheritdoc />
    public IDatabaseTransactionalBatch<TEntity> Delete(string id)
    {
        EnsureNotExecuted();
        EnsureCapacity();
        ApplyDelete(id);
        OperationCount++;
        return this;
    }

    /// <inheritdoc />
    public IDatabaseTransactionalBatch<TEntity> Patch(
        string id,
        Action<IPatchOperations<TEntity>> operations,
        Expression<Func<TEntity, bool>>? condition = null)
    {
        EnsureNotExecuted();
        EnsureCapacity();

        // a patch addresses an id, like a delete, so there is no entity to check the partition of
        ApplyPatch(
            id,
            PatchOperationsBuilder<TEntity>.Build(operations),
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
    protected abstract void ApplyCreate(TEntity entity);

    /// <summary>
    ///     Records a replace operation. Called after the operation has been validated.
    /// </summary>
    /// <param name="entity">The updated entity which will replace the existing one</param>
    protected abstract void ApplyReplace(TEntity entity);

    /// <summary>
    ///     Records an upsert operation. Called after the operation has been validated.
    /// </summary>
    /// <param name="entity">The entity to insert or update</param>
    protected abstract void ApplyUpsert(TEntity entity);

    /// <summary>
    ///     Records a delete operation. Called after the operation has been validated.
    /// </summary>
    /// <param name="id">The id of the entity to delete</param>
    protected abstract void ApplyDelete(string id);

    /// <summary>
    ///     Records a patch operation. Called after the operations have been validated.
    /// </summary>
    /// <param name="id">The id of the document to patch</param>
    /// <param name="operations">The validated operations to apply</param>
    /// <param name="condition">An optional condition that has to hold for the patch to be applied</param>
    protected abstract void ApplyPatch(
        string id,
        IReadOnlyList<DatabasePatchOperation> operations,
        Expression<Func<TEntity, bool>>? condition);

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

    private void EnsureSamePartition(TEntity entity)
    {
        var entityPartitionKey = _resolvePartitionKey(entity);

        // compared by value across every component: a batch is limited to one logical partition,
        // and for a hierarchical key that means the whole hierarchy has to match, not just its head
        if (entityPartitionKey != PartitionKey)
        {
            throw TransactionalBatchError.PartitionKeyMismatch(
                entityPartitionKey.ToString(),
                PartitionKey.ToString(),
                typeof(TEntity).Name);
        }
    }
}
