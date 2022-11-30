using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
{
    public async Task<TEntity> QuerySingleAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            predicate,
            cancellationToken);

        if (!items.Any())
        {
            throw DatabaseError.EntityNotFound(predicate.ToString());
        }

        if (items.Count > 1)
        {
            throw DatabaseError.UnexpectedMultipleResults();
        }

        var entity = items.First();

        // Throw exception if soft delete is enabled and entity is deleted
        if (SoftDeleteState.IsEnabled && IsSoftDeleted(entity))
        {
            throw DatabaseError.EntityNotFound(predicate.ToString());
        }

        await PropertyFilters.ApplyAsync(entity);

        return entity;
    }
}
