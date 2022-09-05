using System;
using System.Collections.Generic;
using System.Linq;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Repositories;

namespace Wemogy.Infrastructure.Database.Core.Registries;

public class RepositoryFactoryRegistry : RegistryBase<Type, Func<Type>>
{
    protected override Func<Type> InitializeEntry(Type key)
    {
        throw new NotImplementedException();
    }

    private DatabaseRepository<TEntity, TPartitionKey, TId> CreateDatabaseRepository<TEntity, TPartitionKey, TId>(
        IDatabaseClient<TEntity, TPartitionKey, TId> databaseClient,
        DatabaseRepositoryOptions options,
        List<IDatabaseRepositoryReadFilter<TEntity>> readFilters,
        List<IDatabaseRepositoryPropertyFilter<TEntity>> propertyFilters)
        where TEntity : class, IEntityBase<TId>
        where TPartitionKey : IEquatable<TPartitionKey>
        where TId : IEquatable<TId>
    {
        return new DatabaseRepository<TEntity, TPartitionKey, TId>(databaseClient, options, readFilters, propertyFilters);
    }

    private Type GetGenericDatabaseRepositoryType(Type databaseRepositoryType)
    {
        return databaseRepositoryType.GetInterfaces().First(x => x.GenericTypeArguments.Length == 3);
    }

    private Type GetEntityType(Type databaseRepositoryType)
    {
        return GetGenericDatabaseRepositoryType(databaseRepositoryType).GenericTypeArguments[0];
    }

    private Type GetPartitionKeyType(Type databaseRepositoryType)
    {
        return GetGenericDatabaseRepositoryType(databaseRepositoryType).GenericTypeArguments[1];
    }

    private Type GetIdType(Type databaseRepositoryType)
    {
        return GetGenericDatabaseRepositoryType(databaseRepositoryType).GenericTypeArguments[2];
    }
}
