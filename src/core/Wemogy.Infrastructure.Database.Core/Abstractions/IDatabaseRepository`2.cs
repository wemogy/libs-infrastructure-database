using System;

namespace Wemogy.Infrastructure.Database.Core.Abstractions
{
    public interface IDatabaseRepository<TEntity, in TPartitionKey> : IDatabaseRepository<TEntity, TPartitionKey, Guid>
        where TEntity : IEntityBase<Guid>
        where TPartitionKey : IEquatable<TPartitionKey>
    {
    }
}
