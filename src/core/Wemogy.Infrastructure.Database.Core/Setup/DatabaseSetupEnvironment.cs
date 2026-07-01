using System;
using Microsoft.Extensions.DependencyInjection;
using Wemogy.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Constants;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Factories;

namespace Wemogy.Infrastructure.Database.Core.Setup;

public class DatabaseSetupEnvironment
{
    private readonly DatabaseRepositoryFactory _databaseRepositoryFactory;
    private readonly IServiceCollection _serviceCollection;

    public DatabaseSetupEnvironment(IServiceCollection serviceCollection, IDatabaseClientFactory databaseClientFactory)
    {
        _serviceCollection = serviceCollection;
        _databaseRepositoryFactory = new DatabaseRepositoryFactory(databaseClientFactory);
    }

    public IServiceCollection Services => _serviceCollection;

    public Func<IServiceProvider, IDatabaseRepository<TEntity>> CreateOutboxRepositoryDelegate<TEntity>(
        string containerName)
        where TEntity : class, IEntityBase
    {
        var options = new DatabaseRepositoryOptions(containerName, enableSoftDelete: false);
        return _databaseRepositoryFactory.CreateDelegateWithOptions<TEntity>(options);
    }

    public DatabaseSetupEnvironment AddRepository<TDatabaseRepository>()
        where TDatabaseRepository : class, IDatabaseRepositoryBase
    {
        var databaseRepositoryFactoryDelegate = _databaseRepositoryFactory.CreateDelegate<TDatabaseRepository>();
        _serviceCollection.AddScoped(serviceProvider => databaseRepositoryFactoryDelegate(serviceProvider));
        return this;
    }

    public DatabaseSetupEnvironment AddRepository<TDatabaseRepository, TDatabaseTenantProvider>()
        where TDatabaseRepository : class, IDatabaseRepositoryBase
        where TDatabaseTenantProvider : IDatabaseTenantProvider
    {
        if (!_databaseRepositoryFactory.IsMultiTenantDatabaseSupported)
        {
            throw Error.Unexpected(
                ErrorCodes.MultiTenantDatabaseNotSupported,
                "The database client factory does not support multi-tenant databases. Feel free to contribute!");
        }

        var databaseRepositoryFactoryDelegate = _databaseRepositoryFactory.CreateDelegate<TDatabaseRepository>();
        var createInstanceDelegate = MultiTenantDatabaseRepositoryFactory.CreateInstanceDelegate<TDatabaseRepository>();
        _serviceCollection.AddScoped(
            serviceProvider =>
            {
                var repository = databaseRepositoryFactoryDelegate(serviceProvider);
                var databaseTenantProvider = serviceProvider.GetRequiredService<TDatabaseTenantProvider>();
                return createInstanceDelegate(repository, databaseTenantProvider);
            });
        return this;
    }
}
