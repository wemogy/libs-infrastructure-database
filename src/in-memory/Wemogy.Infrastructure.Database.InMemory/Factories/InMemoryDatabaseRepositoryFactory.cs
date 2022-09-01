using System;
using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.InMemory.Client;

namespace Wemogy.Infrastructure.Database.InMemory.Factories
{
    public class InMemoryDatabaseRepositoryFactory : DatabaseRepositoryFactoryBase<InMemoryDatabaseClientOptions>
    {
        public InMemoryDatabaseRepositoryFactory(IServiceCollection serviceCollection)
            : base(serviceCollection)
        {
        }

        protected override Type GetDatabaseClientType<TEntity, TPartitionKey, TId>()
        {
            return typeof(InMemoryDatabaseClient<TEntity, TPartitionKey, TId>);
        }

        public static TDatabaseRepository CreateInstance<TDatabaseRepository>()
            where TDatabaseRepository : class, IDatabaseRepository
        {
            var factory = new InMemoryDatabaseRepositoryFactory(
                new ServiceCollection());
            return factory.CreateDatabaseRepository<TDatabaseRepository>(new InMemoryDatabaseClientOptions());
        }
    }
}
