using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public interface IDatabaseRepositoryPropertyFilter<TEntity>
{
    Task FilterAsync(List<TEntity> entities);
}
