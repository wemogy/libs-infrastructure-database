using System;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Factories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;
using Wemogy.Infrastructure.Database.Cosmos.Factories;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Plugins.MultiTenantDatabase;

[Collection("Sequential")]
public class CosmosMultiTenantDatabaseRepositoryTests : MultiTenantDatabaseRepositoryTestsBase
{
    public CosmosMultiTenantDatabaseRepositoryTests()
        : base(
            GetFactoryUser(new MicrosoftTenantProvider()),
            GetFactoryUser(new AppleTenantProvider()),
            GetFactoryDataCenter(new DataCenterTenantProvider()))
    {
    }

    private static Func<IDatabaseRepository<User>> GetFactoryUser(IDatabaseTenantProvider provider)
    {
        return () =>
        {
            var cosmosDatabaseClientFactory = new CosmosDatabaseClientFactory(
                TestingConstants.ConnectionString,
                TestingConstants.DatabaseName,
                true);

            var multiTenantRepository = new MultiTenantDatabaseRepositoryFactory(
                cosmosDatabaseClientFactory,
                provider).CreateInstance<IUserRepository>();

            return multiTenantRepository;
        };
    }

    private static Func<IDatabaseRepository<DataCenter>> GetFactoryDataCenter(IDatabaseTenantProvider provider)
    {
        return () =>
        {
            var cosmosDatabaseClientFactory = new CosmosDatabaseClientFactory(
                TestingConstants.ConnectionString,
                TestingConstants.DatabaseName,
                true);

            var multiTenantRepository = new MultiTenantDatabaseRepositoryFactory(
                cosmosDatabaseClientFactory,
                provider).CreateInstance<IDataCenterRepository>();

            return multiTenantRepository;
        };
    }
}
