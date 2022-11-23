using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public interface IDatabaseRepositoryReadFilter<TEntity>
{
    Task<Expression<Func<TEntity, bool>>> FilterAsync();
}
