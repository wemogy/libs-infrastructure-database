using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            ILogger logger,
            bool insecureDevelopmentMode = false)
        {
            var cosmosDatabaseClientFactory = new CosmosDatabaseClientFactory(
                connectionString,
                databaseName,
                logger,
                insecureDevelopmentMode);

            return serviceCollection
                .AddDatabase(cosmosDatabaseClientFactory);
        }
    }
}
