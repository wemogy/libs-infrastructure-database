using System.Collections.Generic;
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
    }
}
