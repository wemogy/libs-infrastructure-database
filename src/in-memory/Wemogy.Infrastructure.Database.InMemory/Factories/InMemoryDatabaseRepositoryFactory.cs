using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.InMemory.Setup;

namespace Wemogy.Infrastructure.Database.InMemory.Factories
{
    public static class InMemoryDatabaseRepositoryFactory
    {
        public static TDatabaseRepository CreateInstance<TDatabaseRepository>()
            where TDatabaseRepository : class, IDatabaseRepository
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddInMemoryDatabaseClient()
                .AddRepository<TDatabaseRepository>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceProvider.GetRequiredService<TDatabaseRepository>();
        }
    }
}
