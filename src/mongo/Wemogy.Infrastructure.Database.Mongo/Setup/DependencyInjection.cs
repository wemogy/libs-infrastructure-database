using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Setup;
using Wemogy.Infrastructure.Database.Mongo.Factories;

namespace Wemogy.Infrastructure.Database.Mongo.Setup
{
    public static class DependencyInjection
    {
        public static DatabaseSetupEnvironment AddMongoDatabase(
            this IServiceCollection serviceCollection,
            string connectionString,
            string databaseName,
            bool enableLogging = false)
        {
            var mongoDatabaseClientFactory = new MongoDatabaseClientFactory(
                connectionString,
                databaseName,
                enableLogging);

            return serviceCollection
                .AddDatabase(mongoDatabaseClientFactory);
        }
    }
}
