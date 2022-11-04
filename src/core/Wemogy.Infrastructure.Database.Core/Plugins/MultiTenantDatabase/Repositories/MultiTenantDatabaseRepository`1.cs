using System.Reflection;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
    where TEntity : IEntityBase
{
    private readonly IDatabaseRepository<TEntity> _databaseRepository;
    private readonly IDatabaseTenantProvider _databaseTenantProvider;
    private readonly PropertyInfo _partitionKeyProperty;

    public MultiTenantDatabaseRepository(IDatabaseRepository<TEntity> databaseRepository,
        IDatabaseTenantProvider databaseTenantProvider)
    {
        _databaseRepository = databaseRepository;
        _databaseTenantProvider = databaseTenantProvider;
        _partitionKeyProperty = typeof(TEntity).GetPropertyByCustomAttribute<PartitionKeyAttribute>()!;
        if (_partitionKeyProperty == null)
        {
            throw Error.Unexpected(
                "PartitionKeyPropertyNotFound",
                $"There is not partition key specified for the model {typeof(TEntity).FullName}");
        }
    }

    private string BuildComposedPartitionKey(string? partitionKey) => $"{GetPartitionKeyPrefix()}_{partitionKey}";

    private string GetPartitionKeyPrefix() => _databaseTenantProvider.GetTenantId();

    private void AddPartitionKeyPrefix(TEntity entity)
    {
        var partitionKeyValue = (string)_partitionKeyProperty.GetValue(entity);

        _partitionKeyProperty.SetValue(
            entity,
            BuildComposedPartitionKey(partitionKeyValue));
    }

    private void RemovePartiotionKeyPrefix(TEntity entity)
    {
        var prefixedPartitionKeyValue = (string)_partitionKeyProperty.GetValue(entity);
        var valueToTrimOut = BuildComposedPartitionKey(null);

        var partitionKeyValue = prefixedPartitionKeyValue.Replace(
            valueToTrimOut,
            null);

        _partitionKeyProperty.SetValue(
            entity,
            partitionKeyValue);
    }
}
