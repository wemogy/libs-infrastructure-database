using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : IEntityBase
{
    public async Task EnsureExistsAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        var isExisting = await ExistsAsync(
            id,
            partitionKey,
            cancellationToken);

        if (!isExisting)
        {
            throw DatabaseError.EntityNotFound(id, partitionKey);
        }
    }

    public async Task EnsureExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        var isExisting = await ExistsAsync(
            id,
            cancellationToken);

        if (!isExisting)
        {
            throw DatabaseError.EntityNotFound(id);
        }
    }

    public async Task EnsureExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var isExisting = await ExistsAsync(
            predicate,
            cancellationToken);

        if (!isExisting)
        {
            throw DatabaseError.EntityNotFound();
        }
    }
}
