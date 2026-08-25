using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Wemogy.Core.Errors;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Core.Expressions;
using Wemogy.Core.Extensions;
using Wemogy.Core.ValueObjects.Abstractions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity> : IDatabaseRepository<TEntity>
    where TEntity : IEntityBase
{
    private const string PrefixSeparator = "__";
    private readonly IDatabaseRepository<TEntity> _databaseRepository;
    private readonly IDatabaseTenantProvider _databaseTenantProvider;
    private readonly PartitionKeyDefinition _partitionKeyDefinition;

    /// <summary>
    ///     The property holding the broadest component of the partition key. The tenant prefix is
    ///     composed into that component alone, so the hierarchy below it keeps the meaning the
    ///     entity gave it, and the prefix guard every read path adds stays a single StartsWith.
    /// </summary>
    private readonly PropertyInfo _partitionKeyProperty;

    private Expression<Func<TEntity, bool>> PartitionKeyPredicate { get; }

    public IEnabledState SoftDelete { get; }

    public MultiTenantDatabaseRepository(
        IDatabaseRepository<TEntity> databaseRepository,
        IDatabaseTenantProvider databaseTenantProvider)
    {
        _databaseRepository = databaseRepository;
        _databaseTenantProvider = databaseTenantProvider;
        SoftDelete = databaseRepository.SoftDelete;
        _partitionKeyDefinition = PartitionKeyDefinition.Resolve(typeof(TEntity));
        _partitionKeyProperty = _partitionKeyDefinition.FirstProperty;

        PartitionKeyPredicate = GetPartitionKeyPrefixCondition();
    }

    private static void SetMessage(Exception exception, string message)
    {
        if (exception == null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        var type = typeof(Exception);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var fieldInfo = type.GetField("_message", flags);
        fieldInfo?.SetValue(exception, message);
    }

    private string BuildComposedPartitionKeyComponent(string? partitionKey)
    {
        return $"{GetPartitionKeyPrefix()}{PrefixSeparator}{partitionKey}";
    }

    /// <summary>
    ///     Composes the tenant prefix into the broadest component of the key and leaves the
    ///     narrower ones as they are.
    /// </summary>
    private PartitionKeyValue BuildComposedPartitionKey(PartitionKeyValue partitionKey)
    {
        return partitionKey.WithComponent(
            0,
            BuildComposedPartitionKeyComponent(partitionKey[0]));
    }

    private string GetPartitionKeyPrefix()
    {
        return _databaseTenantProvider.GetTenantId();
    }

    private Action AddPartitionKeyPrefix(TEntity entity)
    {
        var partitionKeyValue = (string)_partitionKeyProperty.GetValue(entity)!;

        _partitionKeyProperty.SetValue(
            entity,
            BuildComposedPartitionKeyComponent(partitionKeyValue));

        return () =>
        {
            ReplaceFirstPartitionKeyComponent(
                entity,
                partitionKeyValue);
        };
    }

    private void RemovePartitionKeyPrefix(TEntity entity)
    {
        var prefixedPartitionKeyValue = (string)_partitionKeyProperty.GetValue(entity)!;
        var valueToTrimOut = BuildComposedPartitionKeyComponent(null);

        var partitionKeyValue = prefixedPartitionKeyValue.Replace(
            valueToTrimOut,
            null);

        ReplaceFirstPartitionKeyComponent(
            entity,
            partitionKeyValue);
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
        queryParameters = queryParameters.Clone(); // leave the incoming reference intact!

        // Check if there is already a filter defined that is related to the partition key and prefix accordingly
        var partitionKeyPropertyName = _partitionKeyProperty.Name.ToCamelCase();
        foreach (var partitionKeyFilter in queryParameters.Filters.Where(x => x.Property == partitionKeyPropertyName))
        {
            switch (partitionKeyFilter.Comparator)
            {
                case Comparator.Equals:
                case Comparator.NotEquals:
                case Comparator.StartsWith:
                case Comparator.StartsWithIgnoreCase:
                    var stringPartitionKeyValue = partitionKeyFilter.Value.FromJson<string>();
                    partitionKeyFilter.Value = BuildComposedPartitionKeyComponent(stringPartitionKeyValue).ToJson();
                    break;
                case Comparator.IsOneOf:
                    var stringListPartitionKeyValue = partitionKeyFilter.Value.FromJson<List<string>>() ?? new List<string>();
                    partitionKeyFilter.Value = stringListPartitionKeyValue
                        .Select(BuildComposedPartitionKeyComponent)
                        .ToList()
                        .ToJson();
                    break;
                default:
                    throw Error.Failure(
                        "ComparatorNotSupported",
                        $"Comparator not supported for partition key filtering: {partitionKeyFilter.Comparator}");
            }
        }

        var partitionKeyPrefixFilter = GetPartitionKeyPrefixFilter(partitionKeyPropertyName);
        queryParameters.Filters.Add(partitionKeyPrefixFilter);
        return queryParameters;
    }

    private QueryFilter GetPartitionKeyPrefixFilter(string partitionKeyPropertyName)
    {
        return new QueryFilter
        {
            Comparator = Comparator.StartsWithIgnoreCase,
            Value = _databaseTenantProvider.GetTenantId().ToJson(),
            Property = partitionKeyPropertyName
        };
    }

    private void ReplaceFirstPartitionKeyComponent(TEntity entity, string partitionKeyValue)
    {
        _partitionKeyProperty.SetValue(
            entity,
            partitionKeyValue);
    }

    /// <summary>
    ///     Writes the unprefixed key the caller passed in back onto the entity the provider
    ///     returned, so the entity a caller gets never carries the tenant prefix.
    /// </summary>
    private void ReplacePartitionKey(TEntity entity, PartitionKeyValue partitionKey)
    {
        _partitionKeyDefinition.SetValue(
            entity,
            partitionKey);
    }

    private Expression<Func<TEntity, bool>> BuildComposedPartitionKeyPredicate(
        Expression<Func<TEntity, bool>> predicate)
    {
        return PartitionKeyPredicate.And(
            predicate.ModifyPropertyValue(
                _partitionKeyProperty.Name,
                BuildComposedPartitionKeyComponent));
    }

    private void CleanupException(Exception exception)
    {
        if (exception is ErrorException errorException)
        {
            errorException.Code = RemovePartitionKeyPrefix(errorException.Code);
            errorException.Description = RemovePartitionKeyPrefix(errorException.Description);
        }

        SetMessage(
            exception,
            RemovePartitionKeyPrefix(exception.Message));
    }

    private string RemovePartitionKeyPrefix(string partitionKey)
    {
        var prefixToTrimOut = BuildComposedPartitionKeyComponent(null);
        return partitionKey.Replace(
            prefixToTrimOut,
            null);
    }
}
