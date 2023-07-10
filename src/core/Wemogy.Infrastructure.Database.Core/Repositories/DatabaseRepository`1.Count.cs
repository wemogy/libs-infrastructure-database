using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
{
    public async Task<long> CountAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        if (SoftDelete.IsEnabled)
        {
            predicate = predicate.And(_softDeleteFilterExpression);
        }

        var filter = await GetReadFilter();
        predicate = predicate.And(filter);

        return await _database.CountAsync(
            predicate,
            cancellationToken);
    }
}
