using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Factories;

namespace Wemogy.Infrastructure.Database.InMemory.Factories
{
    public static class InMemoryDatabaseRepositoryFactory
    {
        public static TDatabaseRepository CreateInstance<TDatabaseRepository>()
            where TDatabaseRepository : class, IDatabaseRepositoryBase
        {
            var databaseClientFactory = new InMemoryDatabaseClientFactory();
            return new DatabaseRepositoryFactory(databaseClientFactory)
                .CreateInstance<TDatabaseRepository>();
        }
    }
}
