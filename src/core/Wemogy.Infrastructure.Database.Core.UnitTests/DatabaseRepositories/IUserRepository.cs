using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories.PropertyFilters;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories.ReadFilters;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;

[RepositoryOptions(enableSoftDelete: true)]
// [RepositoryReadFilter(typeof(GeneralUserReadFilter))]
// [RepositoryPropertyFilter(typeof(GeneralUserPropertyFilter))]
public interface IUserRepository : IDatabaseRepository<User>
{
}
