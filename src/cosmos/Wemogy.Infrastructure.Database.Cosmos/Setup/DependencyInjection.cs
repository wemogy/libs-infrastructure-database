using System.Collections.Generic;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Setup;
using Wemogy.Infrastructure.Database.Cosmos.Factories;

namespace Wemogy.Infrastructure.Database.Cosmos.Setup
{
    public static class DependencyInjection
    {
        public static DatabaseSetupEnvironment AddCosmosDatabase(
            this IServiceCollection serviceCollection,
            string connectionString,
            string databaseName,
            bool insecureDevelopmentMode = false,
            bool enableLogging = false,
            List<string>? containerNames = null)
        {
            var cosmosDatabaseClientFactory = new CosmosDatabaseClientFactory(
                connectionString,
                databaseName,
                insecureDevelopmentMode,
                enableLogging,
                containerNames);

            return serviceCollection
                .AddDatabase(cosmosDatabaseClientFactory);
        }

        public static DatabaseSetupEnvironment AddCosmosDatabase(
            this IServiceCollection serviceCollection,
            CosmosClient cosmosClient,
            string databaseName,
            bool enableLogging = false)
        {
            var cosmosDatabaseClientFactory = new CosmosDatabaseClientFactory(
                cosmosClient,
                databaseName,
                enableLogging);

            return serviceCollection
                .AddDatabase(cosmosDatabaseClientFactory);
        }
    }
}
