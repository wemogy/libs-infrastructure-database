using System;
using System.Linq.Expressions;
using System.Reflection;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Core.ValueObjects.Abstractions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity> : IDatabaseRepository<TEntity>
    where TEntity : IEntityBase
{
    private readonly IDatabaseRepository<TEntity> _databaseRepository;
    private readonly IDatabaseTenantProvider _databaseTenantProvider;
    private readonly PropertyInfo _partitionKeyProperty;

    public MultiTenantDatabaseRepository(IDatabaseRepository<TEntity> databaseRepository,
        IDatabaseTenantProvider databaseTenantProvider)
    {
        _databaseRepository = databaseRepository;
        _databaseTenantProvider = databaseTenantProvider;
        SoftDelete = databaseRepository.SoftDelete;
        _partitionKeyProperty = typeof(TEntity).GetPropertyByCustomAttribute<PartitionKeyAttribute>()!;
        if (_partitionKeyProperty == null)
        {
            throw Error.Unexpected(
                "PartitionKeyPropertyNotFound",
                $"There is not partition key specified for the model {typeof(TEntity).FullName}");
        }

        PartitionKeyPredicate = GetPartitionKeyPrefixCondition();
    }

    private Expression<Func<TEntity, bool>> PartitionKeyPredicate { get; }

    public IEnabledState SoftDelete { get; }

    private string BuildComposedPartitionKey(string? partitionKey)
    {
        return $"{GetPartitionKeyPrefix()}_{partitionKey}";
    }

    private string GetPartitionKeyPrefix()
    {
        return _databaseTenantProvider.GetTenantId();
    }

    private Action AddPartitionKeyPrefix(TEntity entity)
    {
        var partitionKeyValue = (string)_partitionKeyProperty.GetValue(entity);

        _partitionKeyProperty.SetValue(
            entity,
            BuildComposedPartitionKey(partitionKeyValue));

        return () =>
        {
            ReplacePartitionKey(
                entity,
                partitionKeyValue);
        };
    }

    private void RemovePartitionKeyPrefix(TEntity entity)
    {
        var partitionKeyValue = GetPartitionKeyValue(entity);

        ReplacePartitionKey(
            entity,
            partitionKeyValue);
    }

    private string GetPartitionKeyValue(TEntity entity)
    {
        var prefixedPartitionKeyValue = (string)_partitionKeyProperty.GetValue(entity);
        var valueToTrimOut = BuildComposedPartitionKey(null);

        var partitionKeyValue = prefixedPartitionKeyValue.Replace(
            valueToTrimOut,
            null);
        return partitionKeyValue;
    }

    private Expression<Func<TEntity, bool>> GetPartitionKeyPrefixCondition()
    {
        // Expression : x => partitionKeyProperty(x).StartsWith(value_of_partition_key_prefix_from_provider);

        // 1. Define the lambda expression parameter
        var parameterExpression = Expression.Parameter(
            typeof(TEntity),
            "entity");

        // 2. Call the getter and retrieve the value of the property
        var propertyExpr = Expression.Property(
            parameterExpression,
            _partitionKeyProperty);

        // 3. define the constant expression for the prefix from the provider (_databaseTenantProvider.GetTenantId())
        var constant = Expression.Constant(
            _databaseTenantProvider.GetTenantId(),
            typeof(string));

        // 4. Define the method to use
        var methodInfo = typeof(string).GetMethod(
            nameof(string.StartsWith),
            new[] { typeof(string) })!;

        // 5. Call the expression builder
        Expression call = Expression.Call(
            propertyExpr,
            methodInfo,
            constant);

        // Create a lambda expression of the latest call
        return Expression.Lambda<Func<TEntity, bool>>(
            call,
            parameterExpression);
    }

    private Expression<Func<TEntity, bool>> IdAndPartitionKeyPrefixedPredicate(string id)
    {
        return PartitionKeyPredicate.And(x => x.Id == id);
    }

    private QueryParameters GetQueryParametersWithPartitionKeyFilter(QueryParameters queryParameters)
    {
        var queryParametersInternal = queryParameters.Clone(); // leave the incoming reference intact!

        // TODO: Check if there is already a filter defined that is related to the partition key and prefix accordingly

        var partitionKeyPrefixFilter = GetPartitionKeyPrefixFilter();
        queryParametersInternal.Filters.Add(partitionKeyPrefixFilter);
        return queryParametersInternal;
    }

    private QueryFilter GetPartitionKeyPrefixFilter()
    {
        return new QueryFilter
        {
            Comparator = Comparator.StartsWithIgnoreCase,
            Value = _databaseTenantProvider.GetTenantId(),
            Property = _partitionKeyProperty.Name
        };
    }

    private void ReplacePartitionKey(TEntity entity, string partitionKeyValue)
    {
        _partitionKeyProperty.SetValue(
            entity,
            partitionKeyValue);
    }
}
