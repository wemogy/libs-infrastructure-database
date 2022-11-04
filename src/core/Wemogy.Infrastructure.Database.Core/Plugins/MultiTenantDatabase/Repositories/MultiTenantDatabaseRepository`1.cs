using System.Reflection;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Core.ValueObjects.Abstractions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity> : IDatabaseRepository<TEntity>
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
        SoftDelete = databaseRepository.SoftDelete;
        _partitionKeyProperty = typeof(TEntity).GetPropertyByCustomAttribute<PartitionKeyAttribute>()!;
        if (_partitionKeyProperty == null)
        {
            throw Error.Unexpected(
                "PartitionKeyPropertyNotFound",
                $"There is not partition key specified for the model {typeof(TEntity).FullName}");
        }
    }

    public IEnabledState SoftDelete { get; }

    private string BuildComposedPartitionKey(string? partitionKey)
    {
        return $"{GetPartitionKeyPrefix()}_{partitionKey}";
    }

    private string GetPartitionKeyPrefix()
    {
        return _databaseTenantProvider.GetTenantId();
    }

    private void AddPartitionKeyPrefix(TEntity entity)
    {
        var partitionKeyValue = (string)_partitionKeyProperty.GetValue(entity);

        _partitionKeyProperty.SetValue(
            entity,
            BuildComposedPartitionKey(partitionKeyValue));
    }

    private void RemovePartitionKeyPrefix(TEntity entity)
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
