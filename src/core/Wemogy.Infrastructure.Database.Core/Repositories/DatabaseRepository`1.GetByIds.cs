using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
{
    public Task<List<TEntity>> GetByIdsAsync(List<string> ids, CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            x => ids.Contains(x.Id),
            cancellationToken);
    }
}
