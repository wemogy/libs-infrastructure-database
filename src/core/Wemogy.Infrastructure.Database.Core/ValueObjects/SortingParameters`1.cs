using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Wemogy.Infrastructure.Database.Core.Enums;

namespace Wemogy.Infrastructure.Database.Core.ValueObjects;

public class SortingParameters<TEntity> : List<SortingParameter>
{
    public SortingParameters<TEntity> AddOrderBy<TKey>(Expression<Func<TEntity, TKey>> keySelector, SortDirection direction = SortDirection.Ascending)
    {
        Add(new SortingParameter(keySelector, direction));
        return this;
    }

    public SortingParameters<TEntity> AddOrderByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector)
    {
        return AddOrderBy(keySelector, SortDirection.Descending);
    }

    public IOrderedEnumerable<TEntity> ApplyTo(IEnumerable<TEntity> source)
    {
        IOrderedEnumerable<TEntity>? orderedEnumerable = null;

        foreach (var sortingParameter in this)
        {
            orderedEnumerable = sortingParameter.Apply(orderedEnumerable ?? source);
        }

        return orderedEnumerable ?? source.OrderBy(x => 0);
    }

    public IOrderedQueryable<TEntity> ApplyTo(IQueryable<TEntity> source)
    {
        IOrderedQueryable<TEntity>? orderedQueryable = null;

        foreach (var sortingParameter in this)
        {
            orderedQueryable = sortingParameter.Apply(orderedQueryable ?? source);
        }

        return orderedQueryable ?? source.OrderBy(x => 0);
    }
}
