using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Cosmos.Setup;

namespace Wemogy.Infrastructure.Database.Cosmos.Factories
{
    public static class CosmosDatabaseRepositoryFactory
    {
        public static TDatabaseRepository CreateInstance<TDatabaseRepository>(
            string connectionString,
            string databaseName,
            bool insecureDevelopmentMode = false)
            where TDatabaseRepository : class, IDatabaseRepository
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddCosmosDatabase(
                    connectionString,
                    databaseName,
                    insecureDevelopmentMode)
                .AddRepository<TDatabaseRepository>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceProvider.GetRequiredService<TDatabaseRepository>();
        }
    }
}
