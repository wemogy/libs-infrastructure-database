using System;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.InMemory.Client;

namespace Wemogy.Infrastructure.Database.InMemory.Factories
{
    public class InMemoryDatabaseClientFactory : IDatabaseClientFactory
    {
        public IDatabaseClient<TEntity, TPartitionKey, TId> CreateClient<TEntity, TPartitionKey, TId>(DatabaseRepositoryOptions databaseRepositoryOptions)
            where TEntity : class, IEntityBase<TId>
            where TPartitionKey : IEquatable<TPartitionKey>
            where TId : IEquatable<TId>
        {
            return new InMemoryDatabaseClient<TEntity, TPartitionKey, TId>();
        }
    }
}
