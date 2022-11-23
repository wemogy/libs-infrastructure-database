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

public class DatabaseRepositoryFactoryFactory
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
                databaseRepositoryTypeMetadata.EntityType);

        var getReadFiltersGenericMethod = typeof(DatabaseRepositoryFactoryFactory)
            .GetGenericMethod(
                nameof(GetReadFilters),
                databaseRepositoryTypeMetadata.EntityType);
        var repositoryReadFilterAttribute = databaseRepositoryType.GetCustomAttribute<RepositoryReadFilterAttribute>();

        var getPropertyFiltersGenericMethod = typeof(DatabaseRepositoryFactoryFactory)
            .GetGenericMethod(
                nameof(GetPropertyFilters),
                databaseRepositoryTypeMetadata.EntityType);
        var repositoryPropertyFilterAttribute =
            databaseRepositoryType.GetCustomAttribute<RepositoryPropertyFilterAttribute>();

        var retryProxy =
            new RetryProxy<PreconditionFailedErrorException>(
                Backoff.ExponentialBackoff(
                    TimeSpan.FromMilliseconds(100),
                    3));

        var t = typeof(IDatabaseClient<>);
        var interT = t.MakeGenericType(
            databaseRepositoryTypeMetadata.EntityType);

        object databaseClientInstance = databaseClient;
        if (DatabaseClientProxy != null)
        {
            databaseClientInstance = DatabaseClientProxy.Wrap(
                interT,
                databaseClient);
        }

        return serviceProvider =>
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

    private DatabaseRepository<TEntity> CreateDatabaseRepository<TEntity>(
        IDatabaseClient<TEntity> databaseClient,
        DatabaseRepositoryOptions options,
        List<IDatabaseRepositoryReadFilter<TEntity>> readFilters,
        List<IDatabaseRepositoryPropertyFilter<TEntity>> propertyFilters)
        where TEntity : class, IEntityBase
    {
        return new DatabaseRepository<TEntity>(
            databaseClient,
            options,
            readFilters,
            propertyFilters);
    }

    private List<IDatabaseRepositoryReadFilter<TEntity>> GetReadFilters<TEntity>(
        IServiceProvider serviceProvider, RepositoryReadFilterAttribute? repositoryReadFilterAttribute)
        where TEntity : IEntityBase
    {
        var readFilters = repositoryReadFilterAttribute?.FilterTypes
            .Select(
                x => (IDatabaseRepositoryReadFilter<TEntity>)ActivatorUtilities.CreateInstance(
                    serviceProvider,
                    x))
            .ToList() ?? new List<IDatabaseRepositoryReadFilter<TEntity>>();
        return readFilters;
    }

    private List<IDatabaseRepositoryPropertyFilter<TEntity>> GetPropertyFilters<TEntity>(
        IServiceProvider serviceProvider, RepositoryPropertyFilterAttribute? repositoryPropertyFilterAttribute)
        where TEntity : IEntityBase
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
