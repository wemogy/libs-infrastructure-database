using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using ImpromptuInterface;
using Microsoft.Extensions.DependencyInjection;
using Polly.Contrib.WaitAndRetry;
using Wemogy.Core.DynamicProxies;
using Wemogy.Core.DynamicProxies.Extensions;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Delegates;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Repositories;

namespace Wemogy.Infrastructure.Database.Core.Factories;

public partial class DatabaseRepositoryFactoryFactory
{
    internal static IAsyncInterceptor? DatabaseClientProxy { get; set; }

    public DatabaseRepositoryFactoryDelegate<TDatabaseRepository> GetRepositoryFactory<TDatabaseRepository>(
        DatabaseRepositoryTypeMetadata databaseRepositoryTypeMetadata,
        DatabaseRepositoryOptions databaseRepositoryOptions,
        IDatabaseClient databaseClient)
        where TDatabaseRepository : class
    {
        var databaseRepositoryType = typeof(TDatabaseRepository);

        // create database repository
        var createDatabaseRepositoryGenericMethod = typeof(DatabaseRepositoryFactoryFactory)
            .GetGenericMethod(
                nameof(CreateDatabaseRepository),
                databaseRepositoryTypeMetadata.EntityType,
                databaseRepositoryTypeMetadata.PartitionKeyType,
                databaseRepositoryTypeMetadata.IdType);

        var getReadFiltersGenericMethod = typeof(DatabaseRepositoryFactoryFactory)
            .GetGenericMethod(
                nameof(GetReadFilters),
                databaseRepositoryTypeMetadata.EntityType,
                databaseRepositoryTypeMetadata.IdType);
        var repositoryReadFilterAttribute = databaseRepositoryType.GetCustomAttribute<RepositoryReadFilterAttribute>();

        var getPropertyFiltersGenericMethod = typeof(DatabaseRepositoryFactoryFactory)
            .GetGenericMethod(
                nameof(GetPropertyFilters),
                databaseRepositoryTypeMetadata.EntityType,
                databaseRepositoryTypeMetadata.IdType);
        var repositoryPropertyFilterAttribute = databaseRepositoryType.GetCustomAttribute<RepositoryPropertyFilterAttribute>();

        var retryProxy =
            new RetryProxy<PreconditionFailedErrorException>(
                Backoff.ExponentialBackoff(
                    TimeSpan.FromMilliseconds(100),
                    3));

        var t = typeof(IDatabaseClient<,,>);
        var interT = t.MakeGenericType(
            databaseRepositoryTypeMetadata.EntityType,
            databaseRepositoryTypeMetadata.PartitionKeyType,
            databaseRepositoryTypeMetadata.IdType);

        object databaseClientInstance = databaseClient;
        if (DatabaseClientProxy != null)
        {
            databaseClientInstance = DatabaseClientProxy.Wrap(
                interT,
                databaseClient);
        }

        return (serviceProvider) =>
        {
            var readFilters = getReadFiltersGenericMethod.Invoke(
                this,
                new object[] { serviceProvider, repositoryReadFilterAttribute });
            var propertyFilters = getPropertyFiltersGenericMethod.Invoke(
                this,
                new object[] { serviceProvider, repositoryPropertyFilterAttribute });
            var databaseRepository = createDatabaseRepositoryGenericMethod.Invoke(
                this,
                new[] { databaseClientInstance, databaseRepositoryOptions, readFilters, propertyFilters });
            return retryProxy.Wrap<TDatabaseRepository>(databaseRepository.ActLike<TDatabaseRepository>());
        };
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
        return new DatabaseRepository<TEntity, TPartitionKey, TId>(
            databaseClient,
            options,
            readFilters,
            propertyFilters);
    }

    private List<IDatabaseRepositoryReadFilter<TEntity>> GetReadFilters<TEntity, TId>(
        IServiceProvider serviceProvider, RepositoryReadFilterAttribute? repositoryReadFilterAttribute)
        where TEntity : class, IEntityBase<TId>
        where TId : IEquatable<TId>
    {
        var readFilters = repositoryReadFilterAttribute?.FilterTypes
            .Select(
                x => (IDatabaseRepositoryReadFilter<TEntity>)ActivatorUtilities.CreateInstance(
                    serviceProvider,
                    x))
            .ToList() ?? new List<IDatabaseRepositoryReadFilter<TEntity>>();
        return readFilters;
    }

    private List<IDatabaseRepositoryPropertyFilter<TEntity>> GetPropertyFilters<TEntity, TId>(
        IServiceProvider serviceProvider, RepositoryPropertyFilterAttribute? repositoryPropertyFilterAttribute)
        where TEntity : class, IEntityBase<TId>
        where TId : IEquatable<TId>
    {
        var propertyFilters = repositoryPropertyFilterAttribute?.FilterTypes
            .Select(
                x => (IDatabaseRepositoryPropertyFilter<TEntity>)ActivatorUtilities.CreateInstance(
                    serviceProvider,
                    x))
            .ToList() ?? new List<IDatabaseRepositoryPropertyFilter<TEntity>>();
        return propertyFilters;
    }
}
