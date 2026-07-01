using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Delegates;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Setup;

namespace Wemogy.Infrastructure.Database.Core.Factories;

public partial class DatabaseRepositoryFactory
{
    private readonly IDatabaseClientFactory _databaseClientFactory;
    private readonly DatabaseRepositoryFactoryFactory _repositoryFactoryFactory;

    public bool IsMultiTenantDatabaseSupported => _databaseClientFactory.IsMultiTenantDatabaseSupported;

    public DatabaseRepositoryFactory(IDatabaseClientFactory databaseClientFactory)
    {
        _databaseClientFactory = databaseClientFactory;
        _repositoryFactoryFactory = new DatabaseRepositoryFactoryFactory();
    }

    public TDatabaseRepository CreateInstance<TDatabaseRepository>()
        where TDatabaseRepository : class, IDatabaseRepositoryBase
    {
        var serviceCollection = new ServiceCollection();
        var databaseSetupEnvironment = new DatabaseSetupEnvironment(
            serviceCollection,
            _databaseClientFactory);
        databaseSetupEnvironment.AddRepository<TDatabaseRepository>();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        return serviceProvider.GetRequiredService<TDatabaseRepository>();
    }

    internal DatabaseRepositoryFactoryDelegate<TDatabaseRepository> CreateDelegate<TDatabaseRepository>()
        where TDatabaseRepository : class, IDatabaseRepositoryBase
    {
        var typeMetadata = new DatabaseRepositoryTypeMetadata(typeof(TDatabaseRepository));
        var databaseRepositoryOptions = ResolveDatabaseRepositoryOptions(typeMetadata);
        var databaseClient = _databaseClientFactory.InvokeGenericMethod<IDatabaseClient>(
            nameof(IDatabaseClientFactory.CreateClient),
            new[] { typeMetadata.EntityType },
            databaseRepositoryOptions);
        var repositoryFactory = _repositoryFactoryFactory.GetRepositoryFactory<TDatabaseRepository>(
            typeMetadata,
            databaseRepositoryOptions,
            databaseClient);

        return repositoryFactory;
    }

    internal Func<IServiceProvider, IDatabaseRepository<TEntity>> CreateDelegateWithOptions<TEntity>(
        DatabaseRepositoryOptions options)
        where TEntity : class, IEntityBase
    {
        var typeMetadata = new DatabaseRepositoryTypeMetadata(typeof(IDatabaseRepository<TEntity>));
        var databaseClient = _databaseClientFactory.InvokeGenericMethod<IDatabaseClient>(
            nameof(IDatabaseClientFactory.CreateClient),
            new[] { typeMetadata.EntityType },
            options);
        var repositoryFactory = _repositoryFactoryFactory.GetRepositoryFactory<IDatabaseRepository<TEntity>>(
            typeMetadata,
            options,
            databaseClient);

        return serviceProvider => repositoryFactory(serviceProvider);
    }

    private DatabaseRepositoryOptions ResolveDatabaseRepositoryOptions(DatabaseRepositoryTypeMetadata typeMetadata)
    {
        var repositoryOptionsAttribute =
            typeMetadata.DatabaseRepositoryType.GetCustomAttribute<RepositoryOptionsAttribute>()
            ?? typeMetadata.EntityType.GetCustomAttribute<RepositoryOptionsAttribute>();
        var databaseRepositoryOptions = new DatabaseRepositoryOptions(
            repositoryOptionsAttribute?.CollectionName ?? $"{typeMetadata.EntityType.Name.ToLower()}s",
            repositoryOptionsAttribute?.EnableSoftDelete ?? false);
        return databaseRepositoryOptions;
    }
}
