using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ImpromptuInterface;
using Microsoft.Extensions.DependencyInjection;
using Polly.Contrib.WaitAndRetry;
using Wemogy.Core.DynamicProxies;
using Wemogy.Core.DynamicProxies.Extensions;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Core.Extensions;
using Wemogy.Core.Reflection;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Delegates;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.ReadFilters;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Repositories;

namespace Wemogy.Infrastructure.Database.Core.Factories;

public partial class DatabaseRepositoryFactoryFactory
{
    public ComposedPrimaryKeyDatabaseRepositoryFactoryDelegate<TDatabaseRepository> GetRepositoryFactory<
        TDatabaseRepository,
        TComposedPrimaryKeyBuilder>(
        DatabaseRepositoryTypeMetadata databaseRepositoryTypeMetadata,
        DatabaseRepositoryOptions databaseRepositoryOptions,
        IDatabaseClientFactory databaseClientFactory)
        where TDatabaseRepository : class
        where TComposedPrimaryKeyBuilder : IComposedPrimaryKeyBuilder
    {
        var databaseRepositoryType = typeof(TDatabaseRepository);
        var internalEntityType = GetInternalEntityType(
            databaseRepositoryTypeMetadata.EntityType,
            databaseRepositoryTypeMetadata.IdType);

        // create database repository
        var createComposedPrimaryKeyDatabaseRepositoryGenericMethod = typeof(DatabaseRepositoryFactoryFactory)
            .GetGenericMethod(
                nameof(CreateComposedPrimaryKeyDatabaseRepository),
                databaseRepositoryTypeMetadata.EntityType,
                databaseRepositoryTypeMetadata.PartitionKeyType,
                databaseRepositoryTypeMetadata.IdType,
                internalEntityType,
                typeof(TComposedPrimaryKeyBuilder));

        var getReadFiltersGenericMethod = typeof(DatabaseRepositoryFactoryFactory)
            .GetGenericMethod(
                nameof(GetReadFilters),
                databaseRepositoryTypeMetadata.EntityType,
                databaseRepositoryTypeMetadata.IdType);
        var getWrappedReadFiltersGenericMethod = typeof(DatabaseRepositoryFactoryFactory)
            .GetGenericMethod(
                nameof(GetWrappedReadFilters),
                internalEntityType,
                databaseRepositoryTypeMetadata.EntityType,
                databaseRepositoryTypeMetadata.IdType);
        var repositoryReadFilterAttribute = databaseRepositoryType.GetCustomAttribute<RepositoryReadFilterAttribute>();

        var getPropertyFiltersGenericMethod = typeof(DatabaseRepositoryFactoryFactory)
            .GetGenericMethod(
                nameof(GetPropertyFilters),
                databaseRepositoryTypeMetadata.EntityType,
                databaseRepositoryTypeMetadata.IdType);
        var getWrappedPropertyFiltersGenericMethod = typeof(DatabaseRepositoryFactoryFactory)
            .GetGenericMethod(
                nameof(GetWrappedPropertyFilters),
                internalEntityType,
                databaseRepositoryTypeMetadata.EntityType,
                databaseRepositoryTypeMetadata.IdType);
        var repositoryPropertyFilterAttribute =
            databaseRepositoryType.GetCustomAttribute<RepositoryPropertyFilterAttribute>();

        var retryProxy =
            new RetryProxy<PreconditionFailedErrorException>(
                Backoff.ExponentialBackoff(
                    TimeSpan.FromMilliseconds(100),
                    3));

        var databaseClient = databaseClientFactory.InvokeGenericMethod<IDatabaseClient>(
            nameof(IDatabaseClientFactory.CreateClient),
            new[]
            {
                internalEntityType,
                databaseRepositoryTypeMetadata.PartitionKeyType,
                typeof(string)
            },
            databaseRepositoryOptions);

        object databaseClientInstance = databaseClient;
        if (DatabaseClientProxy != null)
        {
            var genericDatabaseClientInterfaceType = typeof(IDatabaseClient<,,>);
            var databaseClientInterfaceType = genericDatabaseClientInterfaceType.MakeGenericType(
                internalEntityType,
                databaseRepositoryTypeMetadata.PartitionKeyType,
                typeof(string));
            databaseClientInstance = DatabaseClientProxy.Wrap(
                databaseClientInterfaceType,
                databaseClient);
        }

        return serviceProvider =>
        {
            var composedPrimaryKeyBuilder = serviceProvider.GetRequiredService<TComposedPrimaryKeyBuilder>();
            var readFilters = getReadFiltersGenericMethod.Invoke(
                this,
                new object[] { serviceProvider, repositoryReadFilterAttribute });
            var internalReadFilters = getWrappedReadFiltersGenericMethod.Invoke(
                this,
                new object[] { readFilters, composedPrimaryKeyBuilder.GetComposedPrimaryKeyPrefix() });
            var propertyFilters = getPropertyFiltersGenericMethod.Invoke(
                this,
                new object[] { serviceProvider, repositoryPropertyFilterAttribute });
            var internalPropertyFilters = getWrappedPropertyFiltersGenericMethod.Invoke(
                this,
                new object[] { propertyFilters });
            var databaseRepository = createComposedPrimaryKeyDatabaseRepositoryGenericMethod.Invoke(
                this,
                new[]
                {
                    databaseClientInstance,
                    databaseRepositoryOptions,
                    internalReadFilters,
                    internalPropertyFilters,
                    composedPrimaryKeyBuilder
                });
            return retryProxy.Wrap<TDatabaseRepository>(databaseRepository.ActLike<TDatabaseRepository>());
        };
    }

    private
        ComposedPrimaryKeyDatabaseRepository<TEntity, TPartitionKey, TId, TInternalEntity, TComposedPrimaryKeyBuilder>
        CreateComposedPrimaryKeyDatabaseRepository<TEntity, TPartitionKey, TId, TInternalEntity,
            TComposedPrimaryKeyBuilder>(
            IDatabaseClient<TInternalEntity, TPartitionKey, string> databaseClient,
            DatabaseRepositoryOptions options,
            List<IDatabaseRepositoryReadFilter<TInternalEntity>> readFilters,
            List<IDatabaseRepositoryPropertyFilter<TInternalEntity>> propertyFilters,
            TComposedPrimaryKeyBuilder composedPrimaryKeyBuilder)
        where TEntity : class, IEntityBase<TId>
        where TPartitionKey : IEquatable<TPartitionKey>
        where TId : IEquatable<TId>
        where TInternalEntity : IEntityBase<string>
        where TComposedPrimaryKeyBuilder : IComposedPrimaryKeyBuilder<TId>
    {
        return new ComposedPrimaryKeyDatabaseRepository<TEntity, TPartitionKey, TId, TInternalEntity,
            TComposedPrimaryKeyBuilder>(
            databaseClient,
            options,
            readFilters,
            propertyFilters,
            composedPrimaryKeyBuilder);
    }

    private List<IDatabaseRepositoryReadFilter<TInternalEntity>> GetWrappedReadFilters<TInternalEntity, TEntity, TId>(
        List<IDatabaseRepositoryReadFilter<TEntity>> readFilters,
        string prefix)
        where TEntity : class, IEntityBase<TId>
        where TId : IEquatable<TId>
    {
        var internalReadFilters = readFilters
            .Select(
                x => new ComposedPrimaryKeyReadFilterWrapper<TInternalEntity, TEntity>(x, prefix) as IDatabaseRepositoryReadFilter<TInternalEntity>)
            .ToList();
        return internalReadFilters;
    }

    private List<IDatabaseRepositoryPropertyFilter<TInternalEntity>> GetWrappedPropertyFilters<TInternalEntity, TEntity, TId>(
        List<IDatabaseRepositoryPropertyFilter<TEntity>> propertyFilters)
        where TEntity : class, IEntityBase<TId>
        where TId : IEquatable<TId>
        where TInternalEntity : IEntityBase<string>
    {
        var composedPrimaryKeyPropertyFilterWrapper = new ComposedPrimaryKeyPropertyFilterWrapper<TInternalEntity, TEntity, TId>(
            propertyFilters);

        if (composedPrimaryKeyPropertyFilterWrapper.IsEmpty)
        {
            return new List<IDatabaseRepositoryPropertyFilter<TInternalEntity>>();
        }

        return new List<IDatabaseRepositoryPropertyFilter<TInternalEntity>>
        {
            composedPrimaryKeyPropertyFilterWrapper
        };
    }

    private Type GetInternalEntityType(Type entityType, Type idType)
    {
        if (idType == typeof(string))
        {
            return entityType;
        }

        var internalEntityType =
            new TypeEditor(entityType)
                .AddInterface(typeof(IEntityBase<string>))
                .ModifyPropertyType(
                    nameof(EntityBase.Id),
                    typeof(string))
                .CreateType("Internal" + entityType.Name);

        return internalEntityType;
    }
}
