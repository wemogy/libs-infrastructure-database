using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Fakes;

// reuses the existing "users" container (partition key path /tenantId),
// so no additional container provisioning is required locally or in CI
[RepositoryOptions(collectionName: "users")]
public interface IUserWithETagRepository : IDatabaseRepository<UserWithETag>
{
}
