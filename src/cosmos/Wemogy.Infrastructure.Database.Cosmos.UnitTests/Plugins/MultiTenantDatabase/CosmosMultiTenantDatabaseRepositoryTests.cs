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
    // TODO: Implement the wrapped methods with multi-tenant support and fix all tests.
    // TODO: Add more tests that cover edge cases for Multi-Tenancy
    public CosmosMultiTenantDatabaseRepositoryTests()
        : base(
            GetFactory(new MicrosoftTenantProvider()),
            GetFactory(new AppleTenantProvider()))
    {
    }

    private static Func<IDatabaseRepository<User>> GetFactory(IDatabaseTenantProvider provider)
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
}
