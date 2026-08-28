using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;
using Wemogy.Infrastructure.Database.InMemory.Factories;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Repositories;

[Collection("Sequential")]
public class InMemoryMixedPartitionBatchTests : MixedPartitionBatchTestBase
{
    public InMemoryMixedPartitionBatchTests()
        : base(
            InMemoryDatabaseRepositoryFactory.CreateInstance<IUsageEventRepository>,
            InMemoryDatabaseRepositoryFactory.CreateInstance<IQuotaBalanceRepository>)
    {
    }
}
