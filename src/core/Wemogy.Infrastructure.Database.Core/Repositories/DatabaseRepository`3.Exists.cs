using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

[assembly: InternalsVisibleTo("Wemogy.Infrastructure.Database.Core.UnitTests")]
[assembly: InternalsVisibleTo("Wemogy.Infrastructure.Database.Cosmos.UnitTests")]

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
}
