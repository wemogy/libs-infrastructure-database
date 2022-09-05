using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wemogy.Core.ValueObjects.States;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.ValueObjects;

public class PropertyFiltersState<TEntity> : EnabledState
{
    private readonly List<IDatabaseRepositoryPropertyFilter<TEntity>> _propertyFilters;

    public PropertyFiltersState(bool isEnabled, List<IDatabaseRepositoryPropertyFilter<TEntity>> propertyFilters)
        : base(isEnabled)
    {
        _propertyFilters = propertyFilters;
    }

    internal Task ApplyAsync(TEntity entity)
    {
        return ApplyAsync(new List<TEntity>() { entity });
    }

    internal async Task ApplyAsync(List<TEntity> entities)
    {
        if (!IsEnabled)
        {
            return;
        }

        foreach (var propertyFilter in _propertyFilters)
        {
            await propertyFilter.FilterAsync(entities);
        }
    }

    internal Func<TEntity, Task> Wrap(Func<TEntity, Task> func)
    {
        return async entity =>
        {
            await ApplyAsync(entity);
            await func(entity);
        };
    }
}
