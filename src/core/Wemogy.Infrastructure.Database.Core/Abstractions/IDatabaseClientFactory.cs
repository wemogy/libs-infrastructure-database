using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public interface IDatabaseClientFactory
{
    IDatabaseClient<TEntity> CreateClient<TEntity>(
        DatabaseRepositoryOptions databaseRepositoryOptions)
        where TEntity : class, IEntityBase;
}
