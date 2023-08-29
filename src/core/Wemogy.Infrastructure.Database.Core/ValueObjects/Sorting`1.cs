using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Wemogy.Infrastructure.Database.Core.Enums;

namespace Wemogy.Infrastructure.Database.Core.ValueObjects;

public class Sorting<TEntity>
{
    public List<SortingParameter> Parameters { get; }

    public Sorting()
    {
        Parameters = new List<SortingParameter>();
    }

    public Sorting<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> keySelector, SortDirection direction = SortDirection.Ascending)
    {
        Parameters.Add(new SortingParameter(keySelector, direction));
        return this;
    }

    public Sorting<TEntity> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector)
    {
        return OrderBy(keySelector, SortDirection.Descending);
    }

    /// <summary>
    /// Applies the sorting parameters to the source collection.
    /// </summary>
    /// <param name="source">The source collection.</param>
    /// <returns>The sorted collection.</returns>
    public IOrderedEnumerable<TEntity> ApplyTo(IEnumerable<TEntity> source)
    {
        IOrderedEnumerable<TEntity>? orderedEnumerable = null;

        foreach (var sortingParameter in Parameters)
        {
            orderedEnumerable = sortingParameter.Apply(orderedEnumerable ?? source);
        }

        return orderedEnumerable ?? source.OrderBy(x => 0);
    }

    /// <summary>
    /// Applies the sorting parameters to the source collection.
    /// </summary>
    /// <param name="source">The source collection.</param>
    /// <returns>The sorted collection.</returns>
    public IOrderedQueryable<TEntity> ApplyTo(IQueryable<TEntity> source)
    {
        IOrderedQueryable<TEntity>? orderedQueryable = null;

        foreach (var sortingParameter in Parameters)
        {
            orderedQueryable = sortingParameter.Apply(orderedQueryable ?? source);
        }

        return orderedQueryable ?? source.OrderBy(x => 0);
    }
}
