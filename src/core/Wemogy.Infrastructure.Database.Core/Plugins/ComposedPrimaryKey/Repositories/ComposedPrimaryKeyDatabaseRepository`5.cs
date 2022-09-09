using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using Wemogy.Core.ValueObjects.Abstractions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Abstractions;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Repositories;

public partial class ComposedPrimaryKeyDatabaseRepository<TEntity, TPartitionKey, TId, TInternalEntity, TComposedPrimaryKeyBuilder>
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
    }

    private TEntity AdaptToEntity(TInternalEntity internalEntity)
    {
        if (internalEntity is TEntity entity)
        {
            return entity;
        }

        internalEntity.Id = _composedPrimaryKeyBuilder.ExtractIdFromComposedPrimaryKey(internalEntity.Id).ToString();
        return internalEntity.Adapt<TEntity>(_typeAdapterConfig);
    }

    private TInternalEntity AdaptToInternalEntity(TEntity entity)
    {
        if (entity is TInternalEntity internalEntity)
        {
            return internalEntity;
        }

        internalEntity = entity.Adapt<TInternalEntity>(_typeAdapterConfig);
        internalEntity.Id = _composedPrimaryKeyBuilder.BuildComposedPrimaryKey(entity.Id);
        return internalEntity;
    }

    private string BuildComposedPrimaryKey(TId id)
    {
        return _composedPrimaryKeyBuilder.BuildComposedPrimaryKey(id);
    }

    private Expression<Func<TInternalEntity, bool>> AdaptToInternalEntityExpression(Expression<Func<TEntity, bool>> entityExpression)
    {
        if (entityExpression is Expression<Func<TInternalEntity, bool>> internalEntityExpression)
        {
            return internalEntityExpression;
        }

        throw new NotImplementedException();
    }
}
