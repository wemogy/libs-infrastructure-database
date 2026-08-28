using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Expressions;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Repositories;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

/// <summary>
///     Wraps a mixed-type partition batch of the underlying repository so that every entity added
///     to it carries the tenant prefix on the broadest component of its partition key, the same way
///     <see cref="MultiTenantTransactionalBatch{TEntity}"/> does for the typed batch. Because a
///     partition batch mixes types, the prefix is resolved per operation from the type the caller
///     names, not once from the repository's entity type.
/// </summary>
internal class MultiTenantPartitionBatch : IDatabasePartitionBatch
{
    private const string PrefixSeparator = "__";
    private readonly IDatabasePartitionBatch _partitionBatch;
    private readonly string _tenantId;
    private readonly Action<Exception> _cleanupException;

    public MultiTenantPartitionBatch(
        IDatabasePartitionBatch partitionBatch,
        string tenantId,
        Action<Exception> cleanupException)
    {
        _partitionBatch = partitionBatch;
        _tenantId = tenantId;
        _cleanupException = cleanupException;
    }

    public int OperationCount => _partitionBatch.OperationCount;

    public IDatabasePartitionBatch Create<T>(T entity)
        where T : class
    {
        return Guarded(() => _partitionBatch.Create(Prefixed(entity)));
    }

    public IDatabasePartitionBatch Replace<T>(T entity)
        where T : class
    {
        return Guarded(() => _partitionBatch.Replace(Prefixed(entity)));
    }

    public IDatabasePartitionBatch Upsert<T>(T entity)
        where T : class
    {
        return Guarded(() => _partitionBatch.Upsert(Prefixed(entity)));
    }

    public IDatabasePartitionBatch Delete<T>(string id)
        where T : class
    {
        return Guarded(() => _partitionBatch.Delete<T>(id));
    }

    public IDatabasePartitionBatch Patch<T>(
        string id,
        Action<IPatchOperations<T>> operations,
        Expression<Func<T, bool>>? condition = null)
        where T : class
    {
        // a patch addresses an id and does not carry an entity, so only the partition key values
        // its condition compares against need the tenant prefix
        return Guarded(() => _partitionBatch.Patch(
            id,
            operations,
            ComposeConditionPredicate(condition)));
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _partitionBatch.ExecuteAsync(cancellationToken);
        }
        catch (Exception e)
        {
            _cleanupException(e);
            throw;
        }
    }

    private IDatabasePartitionBatch Guarded(Action addOperation)
    {
        try
        {
            addOperation();
            return this;
        }
        catch (Exception e)
        {
            _cleanupException(e);
            throw;
        }
    }

    /// <summary>
    ///     Returns a copy of the entity whose broadest partition key component carries the tenant
    ///     prefix. A copy, because a provider reads the entity when the batch executes, not when the
    ///     operation is added, so the prefix has to survive until then - while the caller's instance
    ///     must not be left prefixed behind.
    /// </summary>
    private T Prefixed<T>(T entity)
        where T : class
    {
        var prefixedEntity = entity.Clone();
        var partitionKeyProperty = EntityMetadata<T>.PartitionKeyProperty;
        var partitionKeyValue = (string)partitionKeyProperty.GetValue(prefixedEntity)!;
        partitionKeyProperty.SetValue(
            prefixedEntity,
            BuildComposedPartitionKeyComponent(partitionKeyValue));
        return prefixedEntity;
    }

    private Expression<Func<T, bool>>? ComposeConditionPredicate<T>(Expression<Func<T, bool>>? condition)
        where T : class
    {
        if (condition == null)
        {
            return null;
        }

        var partitionKeyProperty = EntityMetadata<T>.PartitionKeyProperty;
        return GetPartitionKeyPrefixCondition<T>(partitionKeyProperty)
            .And(condition.ModifyPropertyValue(
                partitionKeyProperty.Name,
                BuildComposedPartitionKeyComponent));
    }

    private string BuildComposedPartitionKeyComponent(string? partitionKey)
    {
        return $"{_tenantId}{PrefixSeparator}{partitionKey}";
    }

    private Expression<Func<T, bool>> GetPartitionKeyPrefixCondition<T>(PropertyInfo partitionKeyProperty)
    {
        // Expression: entity => partitionKeyProperty(entity).StartsWith(tenantId)
        var parameterExpression = Expression.Parameter(
            typeof(T),
            "entity");
        var propertyExpression = Expression.Property(
            parameterExpression,
            partitionKeyProperty);
        var constant = Expression.Constant(
            _tenantId,
            typeof(string));
        var methodInfo = typeof(string).GetMethod(
            nameof(string.StartsWith),
            new[] { typeof(string) })!;
        Expression call = Expression.Call(
            propertyExpression,
            methodInfo,
            constant);
        return Expression.Lambda<Func<T, bool>>(
            call,
            parameterExpression);
    }
}
