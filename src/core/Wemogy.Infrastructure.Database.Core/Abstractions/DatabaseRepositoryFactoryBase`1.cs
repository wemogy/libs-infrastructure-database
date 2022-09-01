using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Wemogy.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public abstract class DatabaseRepositoryFactoryBase<TDatabaseClientOptions>
    where TDatabaseClientOptions : class
{
    private readonly IServiceCollection _serviceCollection;
    protected List<object> GeneralDatabaseClientParameters { get; }

    protected DatabaseRepositoryFactoryBase(IServiceCollection serviceCollection)
    {
        _serviceCollection = serviceCollection;
        GeneralDatabaseClientParameters = new List<object>();
    }

    public void AddDatabaseRepository<TDatabaseRepository>(TDatabaseClientOptions options)
        where TDatabaseRepository : class, IDatabaseRepository
    {
        var databaseRepositoryTypeMetadata = new DatabaseRepositoryTypeMetadata(typeof(TDatabaseRepository));
        var databaseClientType = GetDatabaseClientType(
            databaseRepositoryTypeMetadata.EntityType,
            databaseRepositoryTypeMetadata.PartitionKeyType,
            databaseRepositoryTypeMetadata.IdType);

        if (databaseClientType == null)
        {
            throw Error.Unexpected(
                "databaseClientTypeNotFound",
                $"Could not find a database client for {typeof(TDatabaseRepository).Name}");
        }

        var parameters = GeneralDatabaseClientParameters.ToList();
        parameters.Add(options);
        _serviceCollection.AddScoped(serviceProvider =>
            new RepositoryFactoryFactory().GetRepositoryFactory<TDatabaseRepository>(databaseRepositoryTypeMetadata)(
                serviceProvider, databaseClientType, parameters.ToArray()));
    }

    protected TDatabaseRepository CreateDatabaseRepository<TDatabaseRepository>(TDatabaseClientOptions options)
        where TDatabaseRepository : class, IDatabaseRepository
    {
        AddDatabaseRepository<TDatabaseRepository>(options);
        return _serviceCollection.BuildServiceProvider().GetRequiredService<TDatabaseRepository>();
    }

    protected abstract Type GetDatabaseClientType<TEntity, TPartitionKey, TId>()
        where TEntity : class, IEntityBase<TId>
        where TPartitionKey : IEquatable<TPartitionKey>
        where TId : IEquatable<TId>;

    private Type GetDatabaseClientType(Type entityType, Type partitionKeyType, Type idType)
    {
        var baseMethod = typeof(DatabaseRepositoryFactoryBase<TDatabaseClientOptions>)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .First(x => x.Name == nameof(GetDatabaseClientType) && x.IsGenericMethodDefinition);
        var genericMethod = baseMethod.MakeGenericMethod(entityType, partitionKeyType, idType);

        dynamic databaseClientType = genericMethod.Invoke(this, Array.Empty<object>());
        return databaseClientType;
    }
}
