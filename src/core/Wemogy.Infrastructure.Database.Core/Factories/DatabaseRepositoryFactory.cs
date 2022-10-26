using System.Reflection;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Delegates;
using Wemogy.Infrastructure.Database.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.Core.Factories;

public class DatabaseRepositoryFactory
{
    private readonly IDatabaseClientFactory _databaseClientFactory;
    private readonly DatabaseRepositoryFactoryFactory _repositoryFactoryFactory;

    public DatabaseRepositoryFactory(IDatabaseClientFactory databaseClientFactory)
    {
        _databaseClientFactory = databaseClientFactory;
        _repositoryFactoryFactory = new DatabaseRepositoryFactoryFactory();
    }

    public DatabaseRepositoryFactoryDelegate<TDatabaseRepository> CreateDelegate<TDatabaseRepository>()
        where TDatabaseRepository : class, IDatabaseRepository
    {
        var typeMetadata = new DatabaseRepositoryTypeMetadata(typeof(TDatabaseRepository));
        var databaseRepositoryOptions = ResolveDatabaseRepositoryOptions(typeMetadata);
        var databaseClient = _databaseClientFactory.InvokeGenericMethod<IDatabaseClient>(
            nameof(IDatabaseClientFactory.CreateClient),
            new[] { typeMetadata.EntityType, typeMetadata.PartitionKeyType, typeMetadata.IdType },
            databaseRepositoryOptions);
        var repositoryFactory = _repositoryFactoryFactory.GetRepositoryFactory<TDatabaseRepository>(
            typeMetadata,
            databaseRepositoryOptions,
            databaseClient);

        return repositoryFactory;
    }

    private DatabaseRepositoryOptions ResolveDatabaseRepositoryOptions(DatabaseRepositoryTypeMetadata typeMetadata)
    {
        var repositoryOptionsAttribute =
            typeMetadata.DatabaseRepositoryType.GetCustomAttribute<RepositoryOptionsAttribute>();
        var databaseRepositoryOptions = new DatabaseRepositoryOptions(
            repositoryOptionsAttribute?.CollectionName ?? $"{typeMetadata.EntityType.Name.ToLower()}s",
            repositoryOptionsAttribute?.EnableSoftDelete ?? typeMetadata.EntityType.IsSoftDeletable());
        return databaseRepositoryOptions;
    }
}
