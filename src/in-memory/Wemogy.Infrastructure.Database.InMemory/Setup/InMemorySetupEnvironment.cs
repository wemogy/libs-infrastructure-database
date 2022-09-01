using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.InMemory.Client;
using Wemogy.Infrastructure.Database.InMemory.Factories;

namespace Wemogy.Infrastructure.Database.InMemory.Setup
{
    public class InMemorySetupEnvironment
    {
        private readonly InMemoryDatabaseRepositoryFactory _inMemoryDatabaseRepositoryFactory;

        public InMemorySetupEnvironment(InMemoryDatabaseRepositoryFactory inMemoryDatabaseRepositoryFactory)
        {
            _inMemoryDatabaseRepositoryFactory = inMemoryDatabaseRepositoryFactory;
        }

        public InMemorySetupEnvironment AddRepository<TDatabaseRepository>()
            where TDatabaseRepository : class, IDatabaseRepository
        {
            _inMemoryDatabaseRepositoryFactory.AddDatabaseRepository<TDatabaseRepository>(
                new InMemoryDatabaseClientOptions());
            return this;
        }
    }
}
