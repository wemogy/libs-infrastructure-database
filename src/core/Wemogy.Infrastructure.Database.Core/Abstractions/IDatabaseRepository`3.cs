using System;
using Wemogy.Core.ValueObjects.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity, in TPartitionKey, TId> : IDatabaseRepository
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
{
    IEnabledState SoftDelete { get; }
}
