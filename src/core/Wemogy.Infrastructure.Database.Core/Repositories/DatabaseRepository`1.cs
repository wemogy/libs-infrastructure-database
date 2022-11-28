using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Wemogy.Core.Extensions;
using Wemogy.Core.ValueObjects.Abstractions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

[assembly: InternalsVisibleTo("Wemogy.Infrastructure.Database.Core.UnitTests")]
[assembly: InternalsVisibleTo("Wemogy.Infrastructure.Database.Cosmos.UnitTests")]

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity> : IDatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
{
    private readonly List<IDatabaseRepositoryReadFilter<TEntity>> _readFilters;

    private readonly Expression<Func<TEntity, bool>> _softDeleteFilterExpression;

    private readonly PropertyInfo? _softDeleteFlagProperty;
    private IDatabaseClient<TEntity> _database;

    public DatabaseRepository(
        IDatabaseClient<TEntity> database,
        DatabaseRepositoryOptions options,
        List<IDatabaseRepositoryReadFilter<TEntity>> readFilters,
        List<IDatabaseRepositoryPropertyFilter<TEntity>> propertyFilters)
        : this(
            database,
            readFilters,
            propertyFilters,
            new SoftDeleteState<TEntity>(options.EnableSoftDelete))
    {
    }

    public DatabaseRepository(
        IDatabaseClient<TEntity> database,
        List<IDatabaseRepositoryReadFilter<TEntity>> readFilters,
        List<IDatabaseRepositoryPropertyFilter<TEntity>> propertyFilters,
        IEnabledState softDeleteState)
    {
        _database = database;
        _readFilters = readFilters;
        SoftDelete = softDeleteState;
        PropertyFilters = new PropertyFiltersState<TEntity>(
            true,
            propertyFilters);
        _softDeleteFlagProperty = typeof(TEntity).GetPropertyByCustomAttribute<SoftDeleteFlagAttribute>();

        // predicate = predicate.And(x => !((ISoftDeletable)x).Deleted);
        // ToDo: implement this
        if (_softDeleteFlagProperty == null)
        {
            _softDeleteFilterExpression = x => true;
        }
        else
        {
            // 1. Define the lambda expression parameter
            var parameterExpression = Expression.Parameter(
                typeof(TEntity),
                "entity");

            // 2. Call the getter and retrieve the value of the property
            var propertyExpr = Expression.Property(
                parameterExpression,
                _softDeleteFlagProperty);

            // 3. define the constant expression for the false boolean value
            var constant = Expression.Constant(false);

            // 4. create the binary equal expression
            var expression = Expression.Equal(
                propertyExpr,
                constant);

            // Create a lambda expression of the latest call
            _softDeleteFilterExpression = Expression.Lambda<Func<TEntity, bool>>(
                expression,
                parameterExpression);
        }
    }

    public PropertyFiltersState<TEntity> PropertyFilters { get; }
    public IEnabledState SoftDelete { get; }

    private bool IsSoftDeleted(TEntity entity)
    {
        if (_softDeleteFlagProperty == null)
        {
            return false;
        }

        return (bool)_softDeleteFlagProperty.GetValue(entity);
    }

    private async Task<Func<TEntity, bool>> GetReadFilter()
    {
        Expression<Func<TEntity, bool>> defaultFilter = x => true;
        var combinedExpressionBody = defaultFilter;
        foreach (var readFilter in _readFilters)
        {
            var filterExpression = await readFilter.FilterAsync();
            combinedExpressionBody = combinedExpressionBody.And(filterExpression);
        }

        // var lambda = Expression.Lambda<Func<TEntity, bool>>(combinedExpressionBody, defaultFilter.Parameters[0]);
        return combinedExpressionBody.Compile();
    }

    internal IDatabaseClient<TEntity> GetDatabaseClient()
    {
        return _database;
    }

    internal void SetDatabaseClient(IDatabaseClient<TEntity> database)
    {
        _database = database;
    }
}
