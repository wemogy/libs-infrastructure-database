using System.Collections.Generic;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Cosmos.Client;

namespace Wemogy.Infrastructure.Database.Cosmos.Factories
{
    public static class CosmosDatabaseRepositoryFactory
    {
        public static TDatabaseRepository CreateInstance<TDatabaseRepository>(
            string connectionString,
            string databaseName,
            bool insecureDevelopmentMode = false,
            bool enableLogging = false,
            List<string>? containerNames = null,
            string leaseContainerName = CosmosDatabaseClientOptions.DefaultLeaseContainerName)
            where TDatabaseRepository : class, IDatabaseRepositoryBase
        {
            var cosmosClientFactory = new CosmosDatabaseClientFactory(
                connectionString,
                databaseName,
                insecureDevelopmentMode,
                enableLogging,
                containerNames,
                leaseContainerName);
            return new DatabaseRepositoryFactory(cosmosClientFactory)
                .CreateInstance<TDatabaseRepository>();
        }
    }
}
