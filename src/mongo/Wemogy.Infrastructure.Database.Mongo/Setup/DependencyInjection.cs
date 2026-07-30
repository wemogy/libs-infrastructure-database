using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Setup;
using Wemogy.Infrastructure.Database.Mongo.Factories;
using Wemogy.Infrastructure.Database.Mongo.Outbox;

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

            var environment = serviceCollection
                .AddDatabase(mongoDatabaseClientFactory);

            environment.Services.AddSingleton(
                typeof(IOutboxEventSource<,>),
                typeof(MongoOutboxEventSource<,>));

            return environment;
        }
    }
}
