using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity, TPartitionKey, TId>
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
{
    public async Task<bool> ExistsAsync(
        TId id,
        TPartitionKey partitionKey,
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

    public async Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
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

    public async Task EnsureExistAsync(
        TId id,
        TPartitionKey partitionKey,
        CancellationToken cancellationToken = default)
    {
        await GetAsync(
               id,
               partitionKey,
               cancellationToken);
    }

    public async Task EnsureExistAsync(TId id, CancellationToken cancellationToken = default)
    {
        await GetAsync(
                id,
                cancellationToken);
    }

    public async Task EnsureExistAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        await GetAsync(
                predicate,
                cancellationToken);
    }
}
