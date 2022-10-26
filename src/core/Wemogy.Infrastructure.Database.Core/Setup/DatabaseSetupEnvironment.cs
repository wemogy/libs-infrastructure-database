using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Factories;

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

    public DatabaseSetupEnvironment AddRepository<TDatabaseRepository>()
        where TDatabaseRepository : class, IDatabaseRepository
    {
        var databaseRepositoryFactoryDelegate = _databaseRepositoryFactory.CreateDelegate<TDatabaseRepository>();
        _serviceCollection.AddScoped(serviceProvider => databaseRepositoryFactoryDelegate(serviceProvider));
        return this;
    }
}
