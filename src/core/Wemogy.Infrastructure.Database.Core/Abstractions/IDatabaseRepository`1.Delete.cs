using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity> : IDatabaseRepository
    where TEntity : IEntityBase
{
    Task DeleteAsync(string id);

    Task DeleteAsync(string id, string partitionKey);

    Task DeleteAsync(Expression<Func<TEntity, bool>> predicate);
}
