using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Factories;

namespace Wemogy.Infrastructure.Database.Mongo.Factories
{
    public static class MongoDatabaseRepositoryFactory
    {
        public static TDatabaseRepository CreateInstance<TDatabaseRepository>(
            string connectionString,
            string databaseName,
            bool enableLogging = false)
            where TDatabaseRepository : class, IDatabaseRepositoryBase
        {
            var databaseClientFactory = new MongoDatabaseClientFactory(
                connectionString,
                databaseName,
                enableLogging);

            return new DatabaseRepositoryFactory(databaseClientFactory)
                .CreateInstance<TDatabaseRepository>();
        }
    }
}
