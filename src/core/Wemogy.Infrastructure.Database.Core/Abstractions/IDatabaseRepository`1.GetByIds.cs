using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    Task<List<TEntity>> GetByIdsAsync(List<string> ids, CancellationToken cancellationToken = default);
}
