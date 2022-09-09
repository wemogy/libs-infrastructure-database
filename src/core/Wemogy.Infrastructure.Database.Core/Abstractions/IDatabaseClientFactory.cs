using System;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public interface IDatabaseClientFactory
{
    IDatabaseClient<TEntity, TPartitionKey, TId> CreateClient<TEntity, TPartitionKey, TId>(DatabaseRepositoryOptions databaseRepositoryOptions)
        where TEntity : class, IEntityBase<TId>
        where TPartitionKey : IEquatable<TPartitionKey>
        where TId : IEquatable<TId>;
}
