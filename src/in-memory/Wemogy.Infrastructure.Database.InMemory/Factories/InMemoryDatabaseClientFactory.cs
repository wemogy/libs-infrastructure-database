using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.InMemory.Client;

namespace Wemogy.Infrastructure.Database.InMemory.Factories
{
    public class InMemoryDatabaseClientFactory : IDatabaseClientFactory
    {
        public IDatabaseClient<TEntity> CreateClient<TEntity>(DatabaseRepositoryOptions databaseRepositoryOptions)
            where TEntity : class, IEntityBase
        {
            return new InMemoryDatabaseClient<TEntity>();
        }
    }
}
