using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

/// <summary>
///     Wraps a transactional batch of the underlying repository so that every entity added to it
///     carries the tenant prefix on its partition key, the same way the write methods of
///     <see cref="MultiTenantDatabaseRepository{TEntity}"/> do.
/// </summary>
/// <typeparam name="TEntity">The entity type every operation of the batch acts on</typeparam>
internal class MultiTenantTransactionalBatch<TEntity> : IDatabaseTransactionalBatch<TEntity>
    where TEntity : IEntityBase
{
    private readonly IDatabaseTransactionalBatch<TEntity> _transactionalBatch;
    private readonly Func<TEntity, Action> _addPartitionKeyPrefix;
    private readonly Func<Expression<Func<TEntity, bool>>?, Expression<Func<TEntity, bool>>?> _composeConditionPredicate;
    private readonly Action<Exception> _cleanupException;

    public MultiTenantTransactionalBatch(
        IDatabaseTransactionalBatch<TEntity> transactionalBatch,
        Func<TEntity, Action> addPartitionKeyPrefix,
        Func<Expression<Func<TEntity, bool>>?, Expression<Func<TEntity, bool>>?> composeConditionPredicate,
        Action<Exception> cleanupException)
    {
        _transactionalBatch = transactionalBatch;
        _addPartitionKeyPrefix = addPartitionKeyPrefix;
        _composeConditionPredicate = composeConditionPredicate;
        _cleanupException = cleanupException;
    }

    public int OperationCount => _transactionalBatch.OperationCount;

    public IDatabaseTransactionalBatch<TEntity> Create(TEntity entity)
    {
        return AddPrefixed(
            entity,
            _transactionalBatch.Create);
    }

    public IDatabaseTransactionalBatch<TEntity> Replace(TEntity entity)
    {
        return AddPrefixed(
            entity,
            _transactionalBatch.Replace);
    }

    public IDatabaseTransactionalBatch<TEntity> Upsert(TEntity entity)
    {
        return AddPrefixed(
            entity,
            _transactionalBatch.Upsert);
    }

    public IDatabaseTransactionalBatch<TEntity> Delete(string id)
    {
        try
        {
            _transactionalBatch.Delete(id);
            return this;
        }
        catch (Exception e)
        {
            _cleanupException(e);
            throw;
        }
    }

    public IDatabaseTransactionalBatch<TEntity> Patch(
        string id,
        Action<IPatchOperations<TEntity>> operations,
        Expression<Func<TEntity, bool>>? condition = null)
    {
        try
        {
            // a patch addresses an id and does not carry an entity, so only the partition key
            // values its condition compares against need the tenant prefix
            _transactionalBatch.Patch(
                id,
                operations,
                _composeConditionPredicate(condition));
            return this;
        }
        catch (Exception e)
        {
            _cleanupException(e);
            throw;
        }
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _transactionalBatch.ExecuteAsync(cancellationToken);
        }
        catch (Exception e)
        {
            _cleanupException(e);
            throw;
        }
    }

    private IDatabaseTransactionalBatch<TEntity> AddPrefixed(
        TEntity entity,
        Func<TEntity, IDatabaseTransactionalBatch<TEntity>> addOperation)
    {
        // the operation is added with a copy of the entity: a provider reads the entity when the
        // batch executes, not when the operation is added, so the prefix has to survive until then
        // - while the instance of the caller must not be left prefixed behind
        var prefixedEntity = entity.Clone();

        // the returned restore action is dropped on purpose, the copy is not handed back
        _addPartitionKeyPrefix(prefixedEntity);

        try
        {
            addOperation(prefixedEntity);
            return this;
        }
        catch (Exception e)
        {
            _cleanupException(e);
            throw;
        }
    }
}
