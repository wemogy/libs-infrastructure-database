using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : IEntityBase
{
    public Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            x => true,
            cancellationToken);
    }
}
