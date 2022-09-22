using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Abstractions;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Repositories;

public partial class ComposedPrimaryKeyDatabaseRepository<TEntity, TPartitionKey, TId, TInternalEntity, TComposedPrimaryKeyBuilder>
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
    where TInternalEntity : IEntityBase<string>
    where TComposedPrimaryKeyBuilder : IComposedPrimaryKeyBuilder<TId>
{
    public Task DeleteAsync(TId id)
    {
        return _databaseRepository.DeleteAsync(BuildComposedPrimaryKey(id));
    }

    public Task DeleteAsync(TId id, TPartitionKey partitionKey)
    {
        return _databaseRepository.DeleteAsync(BuildComposedPrimaryKey(id), partitionKey);
    }

    public Task DeleteAsync(Expression<Func<TEntity, bool>> predicate)
    {
        var internalEntityExpression = AdaptToInternalEntityExpression(predicate);
        return _databaseRepository.DeleteAsync(internalEntityExpression);
    }
}
