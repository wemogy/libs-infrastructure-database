using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Cosmos.Client;
using Wemogy.Infrastructure.Database.Cosmos.Factories;

namespace Wemogy.Infrastructure.Database.Cosmos.Setup
{
    public class CosmosSetupEnvironment
    {
        private readonly CosmosDatabaseRepositoryFactory _cosmosDatabaseRepositoryFactory;
        private readonly string _databaseName;

        public CosmosSetupEnvironment(CosmosDatabaseRepositoryFactory cosmosDatabaseRepositoryFactory, string databaseName)
        {
            _cosmosDatabaseRepositoryFactory = cosmosDatabaseRepositoryFactory;
            _databaseName = databaseName;
        }

        public CosmosSetupEnvironment AddRepository<TDatabaseRepository>(string? customContainerName = null)
            where TDatabaseRepository : class, IDatabaseRepository
        {
            _cosmosDatabaseRepositoryFactory.AddDatabaseRepository<TDatabaseRepository>(
                new CosmosDatabaseClientOptions(_databaseName, customContainerName));
            return this;
        }
    }
}
