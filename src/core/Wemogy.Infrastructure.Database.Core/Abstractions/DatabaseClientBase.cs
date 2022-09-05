using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public abstract class DatabaseClientBase<TEntity, TPartitionKey, TId>
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
{
    private readonly PropertyInfo _partitionKeyPropertyInfo;

    protected DatabaseClientBase()
    {
        var partitionKeyPropertyInfo = typeof(TEntity).GetPropertyByCustomAttribute<PartitionKeyAttribute>();
        if (partitionKeyPropertyInfo == null)
        {
            throw Error.Unexpected("PartitionKeyPropertyNotFound", $"There is not partition key specified for the model {typeof(TEntity).FullName}");
        }

        _partitionKeyPropertyInfo = partitionKeyPropertyInfo;
    }

    protected TPartitionKey ResolvePartitionKeyValue(TEntity entity)
    {
        var partitionKeyValue = (TPartitionKey)_partitionKeyPropertyInfo.GetValue(entity);
        return partitionKeyValue;
    }
}
