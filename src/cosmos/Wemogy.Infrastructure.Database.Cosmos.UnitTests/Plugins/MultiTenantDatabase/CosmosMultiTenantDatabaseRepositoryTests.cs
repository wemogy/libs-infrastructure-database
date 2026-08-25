using System;
using System.Threading.Tasks;
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
            GetFactoryFilteredUser(new MicrosoftTenantProvider()),
            GetFactoryUser(new AppleTenantProvider()),
            GetFactoryDataCenter(new DataCenterTenantProvider()))
    {
    }

    /// <summary>
    ///     The lease container is not created by the provider, so the suite creates it before a
    ///     processor is started rather than relying on the emulator having been seeded with it.
    /// </summary>
    protected override Task PrepareChangeFeedAsync()
    {
        return TestingContainers.EnsureLeaseContainerAsync();
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

    private static Func<IDatabaseRepository<User>> GetFactoryFilteredUser(IDatabaseTenantProvider provider)
    {
        return () =>
        {
            var cosmosDatabaseClientFactory = new CosmosDatabaseClientFactory(
                TestingConstants.ConnectionString,
                TestingConstants.DatabaseName,
                true);

            var multiTenantRepository = new MultiTenantDatabaseRepositoryFactory(
                cosmosDatabaseClientFactory,
                provider).CreateInstance<IFilteredUserRepository>();

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
