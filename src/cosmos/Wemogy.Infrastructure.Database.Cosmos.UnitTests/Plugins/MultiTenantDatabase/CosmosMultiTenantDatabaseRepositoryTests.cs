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
    private readonly IDatabaseRepository<User> _changeFeedUserRepository =
        GetFactoryChangeFeedUser(new MicrosoftTenantProvider())();

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

    /// <summary>
    ///     A collection of its own, so a processor does not have to read its way through the write
    ///     history the rest of the Cosmos suite leaves in the shared one.
    /// </summary>
    protected override IDatabaseRepository<User> ChangeFeedUserRepository => _changeFeedUserRepository;

    private static Func<IDatabaseRepository<User>> GetFactoryChangeFeedUser(IDatabaseTenantProvider provider)
    {
        return () =>
        {
            var cosmosDatabaseClientFactory = new CosmosDatabaseClientFactory(
                TestingConstants.ConnectionString,
                TestingConstants.DatabaseName,
                true);

            return new MultiTenantDatabaseRepositoryFactory(
                cosmosDatabaseClientFactory,
                provider).CreateInstance<IChangeFeedUserRepository>();
        };
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
