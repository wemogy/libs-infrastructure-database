using System.Collections.Generic;
using System.Linq;
using Wemogy.Core.Extensions;

namespace Wemogy.Infrastructure.Database.Core.ValueObjects;

public class QueryParameters
{
    private List<string> _noCamelCasePathList;

    public QueryParameters()
    {
        _noCamelCasePathList = new List<string>();
    }

    public int? Take { get; set; }
    public List<QueryFilter> Filters { get; set; } = new ();

    public List<QuerySorting> Sortings { get; set; } = new ();

    public bool ContainsFilters => Filters.Any();

    public bool ContainsSortings => Sortings.Any();

    public bool HasSearchAfter => Sortings.Any(x => !string.IsNullOrWhiteSpace(x.SearchAfter));

    public void SetNoCamelCasePathList(List<string> noCamelCasePathList)
    {
        _noCamelCasePathList = noCamelCasePathList;
    }

    public bool HasSortingForProperty(string property)
    {
        return ContainsSortings && Sortings.Any(x => x.OrderBy == property);
    }

    public QuerySorting? GetQuerySortingForProperty(string property)
    {
        if (!ContainsSortings)
        {
            return null;
        }

        return Sortings.FirstOrDefault(x => x.OrderBy == property);
    }

    public QueryFilter? GetQueryFilterForProperty(string property)
    {
        if (!ContainsFilters)
        {
            return null;
        }

        return Filters.FirstOrDefault(x => x.Property == property);
    }

    public void RemoveSorting(string property)
    {
        if (!ContainsSortings)
        {
            return;
        }

        Sortings.RemoveAll(x => x.OrderBy == property);
    }

    public void EnsureCamelCase()
    {
        Filters.ForEach(
            x =>
            {
                if (_noCamelCasePathList.Any(path => x.Property.StartsWith(path)))
                {
                    return;
                }

                x.Property = x.Property.ToCamelCase();
            });
        Sortings.ForEach(
            x =>
            {
                if (_noCamelCasePathList.Any(path => x.OrderBy.StartsWith(path)))
                {
                    return;
                }

                x.OrderBy = x.OrderBy.ToCamelCase();
            });
    }
}
