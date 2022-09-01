using System;
using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Cosmos.Client;

namespace Wemogy.Infrastructure.Database.Cosmos.Factories
{
    public class CosmosDatabaseRepositoryFactory : DatabaseRepositoryFactoryBase<CosmosDatabaseClientOptions>
    {
        public CosmosDatabaseRepositoryFactory(
            IServiceCollection serviceCollection,
            string connectionString,
            bool insecureDevelopmentMode = false)
            : base(serviceCollection)
        {
            var cosmosClient = CosmosClientFactory.FromConnectionString(connectionString, insecureDevelopmentMode);
            GeneralDatabaseClientParameters.Add(cosmosClient);
        }

        public TDatabaseRepository CreateDatabaseRepository<TDatabaseRepository>(
            string databaseName,
            string? customContainerName = null)
            where TDatabaseRepository : class, IDatabaseRepository
        {
            var options = new CosmosDatabaseClientOptions(databaseName, customContainerName);
            return CreateDatabaseRepository<TDatabaseRepository>(options);
        }

        protected override Type GetDatabaseClientType<TEntity, TPartitionKey, TId>()
            where TEntity : class
        {
            return typeof(CosmosDatabaseClient<TEntity, TPartitionKey, TId>);
        }

        public static TDatabaseRepository CreateInstance<TDatabaseRepository>(
            string connectionString,
            string databaseName,
            bool insecureDevelopmentMode = false,
            string? customContainerName = null,
            IServiceCollection? serviceCollection = null)
            where TDatabaseRepository : class, IDatabaseRepository
        {
            serviceCollection ??= new ServiceCollection();

            var factory = new CosmosDatabaseRepositoryFactory(
                serviceCollection,
                connectionString,
                insecureDevelopmentMode);
            return factory.CreateDatabaseRepository<TDatabaseRepository>(
                new CosmosDatabaseClientOptions(databaseName, customContainerName));
        }
    }
}
