using System.Reflection;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public abstract class DatabaseClientBase<TEntity>
    where TEntity : class
{
    private readonly PropertyInfo _partitionKeyPropertyInfo;
    private readonly PropertyInfo _idPropertyInfo;

    protected DatabaseClientBase()
    {
        var idPropertyInfo = typeof(TEntity).GetPropertyByCustomAttribute<IdAttribute>();
        if (idPropertyInfo == null)
        {
            throw Error.Unexpected(
                "IdPropertyNotFound",
                $"There is no ID attribute specified for the model {typeof(TEntity).FullName}");
        }

        _idPropertyInfo = idPropertyInfo;

        var partitionKeyPropertyInfo = typeof(TEntity).GetPropertyByCustomAttribute<PartitionKeyAttribute>();
        if (partitionKeyPropertyInfo == null)
        {
            throw Error.Unexpected(
                "PartitionKeyPropertyNotFound",
                $"There is no PartitionKey attribute specified for the model {typeof(TEntity).FullName}");
        }

        _partitionKeyPropertyInfo = partitionKeyPropertyInfo;
    }

    protected string ResolveIdValue(TEntity entity)
    {
        var idValue = (string)_idPropertyInfo.GetValue(entity);
        return idValue;
    }

    protected string ResolvePartitionKeyValue(TEntity entity)
    {
        var partitionKeyValue = (string)_partitionKeyPropertyInfo.GetValue(entity);
        return partitionKeyValue;
    }
}
