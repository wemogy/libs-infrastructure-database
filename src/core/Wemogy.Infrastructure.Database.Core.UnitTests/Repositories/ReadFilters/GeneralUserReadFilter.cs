using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories.ReadFilters;

public class GeneralUserReadFilter : IDatabaseRepositoryReadFilter<User>
{
    public Task<Expression<Func<User, bool>>> FilterAsync()
    {
        return Task.FromResult((Expression<Func<User, bool>>)(x => x.Firstname != "John"));
    }
}
