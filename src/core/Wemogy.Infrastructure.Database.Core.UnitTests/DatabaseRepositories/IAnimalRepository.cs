using System;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;

[RepositoryOptions(enableSoftDelete: true)]
public interface IAnimalRepository : IDatabaseRepository<Animal, Guid, string>
{
}
