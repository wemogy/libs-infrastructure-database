using System.Reflection;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;
using Wemogy.Infrastructure.Database.Core.Repositories;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
    where TEntity : IEntityBase
{
    private readonly IDatabaseRepository<TEntity> _databaseRepository;
    private readonly IDatabaseTenantProvider _databaseTenantProvider;
    private PropertyInfo _partitionKeyProperty;

    public MultiTenantDatabaseRepository(IDatabaseRepository<TEntity> databaseRepository)
    {
        _databaseRepository = databaseRepository;
        var partitionKeyProperty = typeof(TEntity).GetPropertyByCustomAttribute<PartitionKeyAttribute>();
        if (partitionKeyProperty == null)
        {
            throw Error.Unexpected(
                "",
                "");
        }

        _partitionKeyProperty = partitionKeyProperty;
    }

    private string BuildComposedPartitionKey(string partitionKey)
    {
        var tenantId = _databaseTenantProvider.GetTenantId();
        return $"{tenantId}_{partitionKey}";
    }

    private string GetPartitionKeyPrefix()
    {
        return _databaseTenantProvider.GetTenantId();
    }
}
