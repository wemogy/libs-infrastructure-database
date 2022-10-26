using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger>();

            serviceCollection
                .AddCosmosDatabase(connectionString, databaseName, logger, insecureDevelopmentMode)
                .AddRepository<TDatabaseRepository>();

            return serviceProvider.GetRequiredService<TDatabaseRepository>();
        }
    }
}
