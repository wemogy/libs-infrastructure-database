using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;
using Wemogy.Infrastructure.Database.InMemory.Factories;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Plugins.MultiTenantDatabase;

/// <summary>
///     The same behaviour, seen through the multi-tenant plugin: it composes its prefix into the
///     broadest component of the key, so the hierarchy below it has to survive unchanged.
/// </summary>
[Collection("Sequential")]
public class InMemoryMultiTenantHierarchicalPartitionKeyTests : HierarchicalPartitionKeyTestBase
{
    public InMemoryMultiTenantHierarchicalPartitionKeyTests()
        : base(() => new MultiTenantDatabaseRepository<UsageEvent>(
            InMemoryDatabaseRepositoryFactory.CreateInstance<IUsageEventRepository>(),
            new MicrosoftTenantProvider()))
    {
    }
}
