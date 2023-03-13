using System;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;
using Wemogy.Infrastructure.Database.InMemory.Factories;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Plugins.MultiTenantDatabase;

[Collection("Sequential")]
public class InMemoryMultiTenantDatabaseRepositoryTests : MultiTenantDatabaseRepositoryTestsBase
{
    public InMemoryMultiTenantDatabaseRepositoryTests()
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
            var databaseRepository = InMemoryDatabaseRepositoryFactory.CreateInstance<IUserRepository>();

            var multiTenantRepository = new MultiTenantDatabaseRepository<User>(
                databaseRepository,
                provider);

            return multiTenantRepository;
        };
    }

    private static Func<IDatabaseRepository<DataCenter>> GetFactoryDataCenter(IDatabaseTenantProvider provider)
    {
        return () =>
        {
            var databaseRepository = InMemoryDatabaseRepositoryFactory.CreateInstance<IDataCenterRepository>();

            var multiTenantRepository = new MultiTenantDatabaseRepository<DataCenter>(
                databaseRepository,
                provider);

            return multiTenantRepository;
        };
    }
}
