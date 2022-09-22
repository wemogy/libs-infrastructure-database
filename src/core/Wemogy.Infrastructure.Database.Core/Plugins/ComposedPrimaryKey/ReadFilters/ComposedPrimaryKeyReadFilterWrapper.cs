using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Wemogy.Core.Expressions;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.ReadFilters;

public class
    ComposedPrimaryKeyReadFilterWrapper<TInternalEntity, TEntity> : IDatabaseRepositoryReadFilter<TInternalEntity>
{
    private readonly IDatabaseRepositoryReadFilter<TEntity> _readFilter;
    private readonly string _prefix;

    public ComposedPrimaryKeyReadFilterWrapper(IDatabaseRepositoryReadFilter<TEntity> readFilter, string prefix)
    {
        _readFilter = readFilter;
        _prefix = prefix;
    }

    public async Task<Expression<Func<TInternalEntity, bool>>> FilterAsync()
    {
        var filter = await _readFilter.FilterAsync();
        var mappedExpression =
            filter.ReplaceFunctionalBinaryExpressionParameterType<TEntity, TInternalEntity>(_prefix);
        return mappedExpression;
    }
}
