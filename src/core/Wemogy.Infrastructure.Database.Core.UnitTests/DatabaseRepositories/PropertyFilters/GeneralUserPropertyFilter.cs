using System.Collections.Generic;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories.PropertyFilters;

public class GeneralUserPropertyFilter : IDatabaseRepositoryPropertyFilter<User>
{
    public Task FilterAsync(List<User> entities)
    {
        foreach (var entity in entities)
        {
            entity.PrivateNote = string.Empty;
        }

        return Task.CompletedTask;
    }
}
