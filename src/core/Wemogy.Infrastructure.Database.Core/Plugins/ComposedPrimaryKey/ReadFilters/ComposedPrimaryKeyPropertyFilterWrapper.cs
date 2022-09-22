using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mapster;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.ReadFilters;

public class
    ComposedPrimaryKeyPropertyFilterWrapper<TInternalEntity, TEntity, TId> : IDatabaseRepositoryPropertyFilter<
        TInternalEntity>
    where TInternalEntity : IEntityBase<string>
    where TEntity : IEntityBase<TId>
    where TId : IEquatable<TId>
{
    private readonly List<IDatabaseRepositoryPropertyFilter<TEntity>> _propertyFilters;
    private readonly TypeAdapterConfig _typeAdapterConfig;

    public bool IsEmpty => _propertyFilters.Count == 0;

    public ComposedPrimaryKeyPropertyFilterWrapper(
        List<IDatabaseRepositoryPropertyFilter<TEntity>> propertyFilters)
    {
        _propertyFilters = propertyFilters;
        _typeAdapterConfig = new TypeAdapterConfig();
        _typeAdapterConfig.NewConfig<TInternalEntity, TEntity>()
            .Ignore("Id");
        _typeAdapterConfig.NewConfig<TEntity, TInternalEntity>()
            .Ignore("Id");
    }

    public async Task FilterAsync(List<TInternalEntity> internalEntities)
    {
        var entities = internalEntities.Select(AdaptToEntity).ToList();

        foreach (var propertyFilter in _propertyFilters)
        {
            await propertyFilter.FilterAsync(entities);
        }

        for (var i = 0; i < internalEntities.Count; i++)
        {
            entities[i].Adapt(
                internalEntities[i],
                _typeAdapterConfig);
        }
    }

    private TEntity AdaptToEntity(TInternalEntity internalEntity)
    {
        if (internalEntity is TEntity entity)
        {
            return entity;
        }

        internalEntity = internalEntity.Clone();
        return internalEntity.Adapt<TEntity>(_typeAdapterConfig);
    }
}
