using System.Reflection;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public abstract class DatabaseClientBase<TEntity>
    where TEntity : IEntityBase
{
    private readonly PropertyInfo _partitionKeyPropertyInfo;

    protected DatabaseClientBase()
    {
        var partitionKeyPropertyInfo = typeof(TEntity).GetPropertyByCustomAttribute<PartitionKeyAttribute>();
        if (partitionKeyPropertyInfo == null)
        {
            throw Error.Unexpected(
                "PartitionKeyPropertyNotFound",
                $"There is not partition key specified for the model {typeof(TEntity).FullName}");
        }

        _partitionKeyPropertyInfo = partitionKeyPropertyInfo;
    }

    protected string ResolvePartitionKeyValue(TEntity entity)
    {
        var partitionKeyValue = (string)_partitionKeyPropertyInfo.GetValue(entity);
        return partitionKeyValue;
    }
}
