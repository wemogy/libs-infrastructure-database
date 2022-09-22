using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Setup;

public class DatabaseSetupEnvironment
{
    private readonly IServiceCollection _serviceCollection;
    private readonly DatabaseRepositoryFactory _databaseRepositoryFactory;

    public DatabaseSetupEnvironment(IServiceCollection serviceCollection, IDatabaseClientFactory databaseClientFactory)
    {
        _serviceCollection = serviceCollection;
        _databaseRepositoryFactory = new DatabaseRepositoryFactory(databaseClientFactory);
    }

    public DatabaseSetupEnvironment AddRepository<TDatabaseRepository>()
        where TDatabaseRepository : class, IDatabaseRepository
    {
        var databaseRepositoryFactoryDelegate = _databaseRepositoryFactory.CreateDelegate<TDatabaseRepository>();
        _serviceCollection.AddScoped(serviceProvider => databaseRepositoryFactoryDelegate(serviceProvider));
        return this;
    }

    public DatabaseSetupEnvironment AddRepository<TDatabaseRepository, TComposedPrimaryKey>()
        where TDatabaseRepository : class, IDatabaseRepository
        where TComposedPrimaryKey : IComposedPrimaryKeyBuilder
    {
        var databaseRepositoryFactoryDelegate = _databaseRepositoryFactory.CreateDelegate<TDatabaseRepository, TComposedPrimaryKey>();
        _serviceCollection.AddScoped(serviceProvider => databaseRepositoryFactoryDelegate(serviceProvider));
        return this;
    }
}
