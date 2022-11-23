using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

/// <summary>
///     Interface to describe how a repository should be filtered
/// </summary>
/// <typeparam name="TEntity">The entity for which the repository is used</typeparam>
public interface IDatabaseRepositoryReadFilter<TEntity>
{
    Task<Expression<Func<TEntity, bool>>> FilterAsync();
}
