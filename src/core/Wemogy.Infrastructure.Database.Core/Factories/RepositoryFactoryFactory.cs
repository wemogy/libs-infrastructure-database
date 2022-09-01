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
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.CustomAttributes;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Repositories;

namespace Wemogy.Infrastructure.Database.Core.Factories;

public class RepositoryFactoryFactory
{
    internal static IAsyncInterceptor? DatabaseClientProxy;

    public Func<IServiceProvider, Type, object[], TDatabaseRepository> GetRepositoryFactory<TDatabaseRepository>(DatabaseRepositoryTypeMetadata databaseRepositoryTypeMetadata)
        where TDatabaseRepository : class
    {
        var databaseRepositoryType = typeof(TDatabaseRepository);

        // create database repository
        var createDatabaseRepositoryMethodInfo = typeof(RepositoryFactoryFactory)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .First(x => x.Name == nameof(CreateDatabaseRepository) && x.IsGenericMethodDefinition &&
                        x.GetGenericArguments().Length == 3);
        var createDatabaseRepositoryGenericMethod = createDatabaseRepositoryMethodInfo.MakeGenericMethod(
            databaseRepositoryTypeMetadata.EntityType,
            databaseRepositoryTypeMetadata.PartitionKeyType,
            databaseRepositoryTypeMetadata.IdType);

        var repositoryOptionsAttribute = databaseRepositoryType.GetCustomAttribute<RepositoryOptionsAttribute>();
        var databaseRepositoryOptions = repositoryOptionsAttribute?.Options ?? DatabaseRepositoryOptions.Default;

        var getReadQueryFiltersMethodBase = typeof(RepositoryFactoryFactory)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .First(x => x.Name == nameof(GetReadQueryFilters) && x.IsGenericMethodDefinition &&
                        x.GetGenericArguments().Length == 2);
        var getReadQueryFiltersGenericMethod = getReadQueryFiltersMethodBase.MakeGenericMethod(
            databaseRepositoryTypeMetadata.EntityType,
            databaseRepositoryTypeMetadata.IdType);
        var repositoryReadFilterAttribute = databaseRepositoryType.GetCustomAttribute<RepositoryReadFilterAttribute>();

        var retryProxy =
            new RetryProxy<PreconditionFailedErrorException>(
                Backoff.ExponentialBackoff(
                TimeSpan.FromMilliseconds(100),
                3));

        var t = typeof(IDatabaseClient<,,>);
        var interT = t.MakeGenericType(databaseRepositoryTypeMetadata.EntityType,
            databaseRepositoryTypeMetadata.PartitionKeyType, databaseRepositoryTypeMetadata.IdType);

        return (serviceProvider, databaseClientType, parameters) =>
        {
            var databaseClientInstance = ActivatorUtilities.CreateInstance(serviceProvider, databaseClientType, parameters);
            if (DatabaseClientProxy != null)
            {
                databaseClientInstance = DatabaseClientProxy.Wrap(interT, databaseClientInstance);
            }
            var readFilters = getReadQueryFiltersGenericMethod.Invoke(this, new object[] { serviceProvider, repositoryReadFilterAttribute });
            var databaseRepository = createDatabaseRepositoryGenericMethod.Invoke(this, new[] { databaseClientInstance, databaseRepositoryOptions, readFilters });
            return retryProxy.Wrap<TDatabaseRepository>(databaseRepository.ActLike<TDatabaseRepository>());
        };
    }

    private DatabaseRepository<TEntity, TPartitionKey, TId> CreateDatabaseRepository<TEntity, TPartitionKey, TId>(
        IDatabaseClient<TEntity, TPartitionKey, TId> databaseClient,
        DatabaseRepositoryOptions options,
        List<IDatabaseRepositoryReadFilter<TEntity>> readFilters)
        where TEntity : class, IEntityBase<TId>
        where TPartitionKey : IEquatable<TPartitionKey>
        where TId : IEquatable<TId>
    {
        return new DatabaseRepository<TEntity, TPartitionKey, TId>(databaseClient, options, readFilters);
    }

    private List<IDatabaseRepositoryReadFilter<TEntity>> GetReadQueryFilters<TEntity, TId>(IServiceProvider serviceProvider, RepositoryReadFilterAttribute? repositoryReadFilterAttribute)
        where TEntity : class, IEntityBase<TId>
        where TId : IEquatable<TId>
    {
        var readFilters = repositoryReadFilterAttribute?.FilterTypes
            .Select(x => (IDatabaseRepositoryReadFilter<TEntity>)ActivatorUtilities.CreateInstance(serviceProvider, x)).ToList() ?? new List<IDatabaseRepositoryReadFilter<TEntity>>();
        return readFilters;
    }
}
