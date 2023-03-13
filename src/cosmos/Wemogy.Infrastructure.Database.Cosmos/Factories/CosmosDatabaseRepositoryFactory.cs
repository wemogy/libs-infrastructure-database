using System.Collections.Generic;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Factories;

namespace Wemogy.Infrastructure.Database.Cosmos.Factories
{
    public static class CosmosDatabaseRepositoryFactory
    {
        public static TDatabaseRepository CreateInstance<TDatabaseRepository>(
            string connectionString,
            string databaseName,
            bool insecureDevelopmentMode = false,
            bool enableLogging = false,
            List<string>? containerNames = null)
            where TDatabaseRepository : class, IDatabaseRepositoryBase
        {
            var cosmosClientFactory = new CosmosDatabaseClientFactory(
                connectionString,
                databaseName,
                insecureDevelopmentMode,
                enableLogging,
                containerNames);
            return new DatabaseRepositoryFactory(cosmosClientFactory)
                .CreateInstance<TDatabaseRepository>();
        }
    }
}
