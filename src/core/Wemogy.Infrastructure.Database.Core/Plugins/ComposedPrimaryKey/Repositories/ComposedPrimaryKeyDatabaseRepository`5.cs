using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Mapster;
using Wemogy.Core.Errors;
using Wemogy.Core.Expressions;
using Wemogy.Core.Extensions;
using Wemogy.Core.ValueObjects.Abstractions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Abstractions;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Repositories;

public partial class ComposedPrimaryKeyDatabaseRepository<TEntity, TPartitionKey, TId, TInternalEntity,
        TComposedPrimaryKeyBuilder>
    : IDatabaseRepository<TEntity, TPartitionKey, TId>
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
    where TInternalEntity : IEntityBase<string>
    where TComposedPrimaryKeyBuilder : IComposedPrimaryKeyBuilder<TId>
{
    public IEnabledState SoftDelete { get; }
    private readonly DatabaseRepository<TInternalEntity, TPartitionKey, string> _databaseRepository;
    private readonly TComposedPrimaryKeyBuilder _composedPrimaryKeyBuilder;
    private readonly TypeAdapterConfig _typeAdapterConfig;

    public ComposedPrimaryKeyDatabaseRepository(
        IDatabaseClient<TInternalEntity, TPartitionKey, string> database,
        DatabaseRepositoryOptions options,
        List<IDatabaseRepositoryReadFilter<TInternalEntity>> readFilters,
        List<IDatabaseRepositoryPropertyFilter<TInternalEntity>> propertyFilters,
        TComposedPrimaryKeyBuilder composedPrimaryKeyBuilder)
    {
        SoftDelete = new SoftDeleteState<TInternalEntity>(options.EnableSoftDelete);
        _databaseRepository = new DatabaseRepository<TInternalEntity, TPartitionKey, string>(
            database,
            readFilters,
            propertyFilters,
            SoftDelete);
        _composedPrimaryKeyBuilder = composedPrimaryKeyBuilder;
        _typeAdapterConfig = new TypeAdapterConfig();
        /*
        .NewConfig<TInternalEntity, TEntity>()
        .BeforeMapping(
            (src, _) =>
            {
                MapContext.Current = new MapContext
                {
                    Parameters =
                    {
                        ["id"] = _composedPrimaryKeyBuilder.ExtractIdFromComposedPrimaryKey(src.Id)
                    }
                };
            })
        .Ignore(x => x.Id)
        .Config;*/
    }

    private TEntity AdaptToEntity(TInternalEntity internalEntity)
    {
        internalEntity = internalEntity.Clone();
        internalEntity.Id = _composedPrimaryKeyBuilder.ExtractIdFromComposedPrimaryKey(internalEntity.Id).ToString();
        if (internalEntity is TEntity entity)
        {
            return entity;
        }

        return internalEntity.Adapt<TEntity>(_typeAdapterConfig);
    }

    private TInternalEntity AdaptToInternalEntity(TEntity entity)
    {
        var internalEntity = entity.Adapt<TInternalEntity>(_typeAdapterConfig);
        internalEntity.Id = _composedPrimaryKeyBuilder.BuildComposedPrimaryKey(entity.Id);
        return internalEntity;
    }

    private string BuildComposedPrimaryKey(TId id)
    {
        return _composedPrimaryKeyBuilder.BuildComposedPrimaryKey(id);
    }


    private QueryParameters AdaptToInternalEntityQueryParameters(QueryParameters queryParameters)
    {
        var idPropertyName = nameof(IEntityBase<TId>.Id).ToCamelCase();
        var idFiltersCount = queryParameters.Filters.Count(x => x.Property == idPropertyName);
        var hasIdSorting = queryParameters.Sortings.Any(x => x.OrderBy == idPropertyName);

        if (idFiltersCount == 0 && !hasIdSorting)
        {
            return queryParameters;
        }

        var internalQueryParameters = new QueryParameters
        {
            Take = queryParameters.Take
        };

        foreach (var queryFilter in queryParameters.Filters)
        {
            var internalQueryFilter = queryFilter.Clone();
            if (internalQueryFilter.Property == idPropertyName)
            {
                throw new NotImplementedException();

                // internalQueryFilter.Value = ;
            }

            internalQueryParameters.Filters.Add(internalQueryFilter);
        }

        foreach (var querySorting in queryParameters.Sortings)
        {
            var internalQuerySorting = querySorting.Clone();
            if (internalQuerySorting.SearchAfter != null && internalQuerySorting.OrderBy == idPropertyName)
            {
                var id = internalQuerySorting.SearchAfter.FromJson<TId>();

                if (id == null)
                {
                    throw Error.Unexpected(
                        "IdDeserializationFailed",
                        $"The id '{internalQuerySorting.SearchAfter}' could not be deserialized to {typeof(TId).FullName}.");
                }

                internalQuerySorting.SearchAfter = BuildComposedPrimaryKey(id).ToJson();
            }

            queryParameters.Sortings.Add(internalQuerySorting);
        }

        return internalQueryParameters;
    }

    private Expression<Func<TInternalEntity, bool>> AdaptToInternalEntityExpression(
        Expression<Func<TEntity, bool>> entityExpression)
    {
        if (entityExpression is Expression<Func<TInternalEntity, bool>> internalEntityExpression)
        {
            return internalEntityExpression;
        }

        var mappedExpression =
            entityExpression.ReplaceFunctionalBinaryExpressionParameterType<TEntity, TInternalEntity>(
                _composedPrimaryKeyBuilder.GetComposedPrimaryKeyPrefix());
        return mappedExpression;
    }
}
