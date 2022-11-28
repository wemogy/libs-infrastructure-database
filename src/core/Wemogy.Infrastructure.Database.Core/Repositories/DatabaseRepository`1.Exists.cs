using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
{
    public async Task<bool> ExistsAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await GetAsync(
                id,
                partitionKey,
                cancellationToken);
            return true;
        }
        catch (NotFoundErrorException)
        {
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            await GetAsync(
                id,
                cancellationToken);
            return true;
        }
        catch (NotFoundErrorException)
        {
            return false;
        }
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await GetAsync(
                predicate,
                cancellationToken);
            return true;
        }
        catch (NotFoundErrorException)
        {
            return false;
        }
    }
}
