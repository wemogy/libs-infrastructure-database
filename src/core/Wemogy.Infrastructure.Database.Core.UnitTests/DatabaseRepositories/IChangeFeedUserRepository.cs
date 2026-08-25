using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;

/// <summary>
///     A collection of its own for the change feed tests, so they do not read the feed of the
///     collection the rest of the suite writes to.
///     <para>
///         A change feed processor has to read its way to the end of the feed before it sees the
///         write a test just made, and how long that takes grows with everything ever written to the
///         collection - not with what it currently holds, so emptying it does not help. Sharing
///         <see cref="IUserRepository"/> made every change feed test wait for the write history of
///         several hundred other tests, which is both slow and a timeout waiting to happen.
///     </para>
/// </summary>
[RepositoryOptions(enableSoftDelete: true, collectionName: "changefeedusers")]
public interface IChangeFeedUserRepository : IDatabaseRepository<User>
{
}
