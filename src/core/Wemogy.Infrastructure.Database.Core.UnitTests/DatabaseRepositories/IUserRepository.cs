using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.CustomAttributes;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories.ReadFilters;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;

[RepositoryOptions(enableSoftDelete: true)]
[RepositoryReadFilter(typeof(GeneralUserReadFilter))]
public interface IUserRepository : IDatabaseRepository<User>
{
}
