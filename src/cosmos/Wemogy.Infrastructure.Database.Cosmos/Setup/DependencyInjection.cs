using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Cosmos.Factories;

namespace Wemogy.Infrastructure.Database.Cosmos.Setup
{
    public static class DependencyInjection
    {
        public static CosmosSetupEnvironment AddCosmosDatabaseClient(
            this IServiceCollection serviceCollection,
            string connectionString,
            string databaseName,
            bool insecureDevelopmentMode = false)
        {
            return new CosmosSetupEnvironment(
                new CosmosDatabaseRepositoryFactory(serviceCollection, connectionString, insecureDevelopmentMode),
                databaseName);
        }
    }
}
