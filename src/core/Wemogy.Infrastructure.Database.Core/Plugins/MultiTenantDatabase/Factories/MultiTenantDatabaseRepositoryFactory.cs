using System;
using ImpromptuInterface;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Constants;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Factories;

public class MultiTenantDatabaseRepositoryFactory
{
    private readonly DatabaseRepositoryFactory _databaseRepositoryFactory;
    private readonly IDatabaseTenantProvider _databaseTenantProvider;

    public MultiTenantDatabaseRepositoryFactory(
        IDatabaseClientFactory databaseClientFactory,
        IDatabaseTenantProvider databaseTenantProvider)
    {
        _databaseTenantProvider = databaseTenantProvider;
        _databaseRepositoryFactory = new DatabaseRepositoryFactory(databaseClientFactory);
    }

    public TDatabaseRepository CreateInstance<TDatabaseRepository>()
        where TDatabaseRepository : class, IDatabaseRepositoryBase
    {
        var repository = _databaseRepositoryFactory.CreateInstance<TDatabaseRepository>();
        var createInstanceDelegate = CreateInstanceDelegate<TDatabaseRepository>();
        return createInstanceDelegate(
            repository,
            _databaseTenantProvider);
    }

    internal static Func<TDatabaseRepository, IDatabaseTenantProvider, TDatabaseRepository> CreateInstanceDelegate<TDatabaseRepository>()
        where TDatabaseRepository : class, IDatabaseRepositoryBase
    {
        if (!typeof(TDatabaseRepository).InheritsOrImplements(
                typeof(IDatabaseRepository<>),
                out var databaseRepositoryType) || databaseRepositoryType == null)
        {
            throw Error.Failure(
                "NotImplementsIDatabaseRepositoryBase",
                $"The type {typeof(TDatabaseRepository).FullName} does not implement IDatabaseRepositoryBase");
        }

        var entityType = databaseRepositoryType.GenericTypeArguments[0];
        var entityMultiTenantRepositoryType = typeof(MultiTenantDatabaseRepository<>).MakeGenericType(entityType);

        TDatabaseRepository CreateInstance(TDatabaseRepository repository, IDatabaseTenantProvider databaseTenantProvider)
        {
            return Activator.CreateInstance(
                entityMultiTenantRepositoryType,
                repository,
                databaseTenantProvider).ActLike<TDatabaseRepository>();
        }

        return CreateInstance;
    }
}
