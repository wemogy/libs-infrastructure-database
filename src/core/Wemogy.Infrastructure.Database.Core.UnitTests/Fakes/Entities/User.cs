using System;
using Bogus;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.CustomAttributes;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

public class User : EntityBase
{
    [PartitionKey]
    public Guid TenantId { get; set; }

    public string Firstname { get; set; }

    public string Lastname { get; set; }

    public User()
    {
        TenantId = Guid.Empty;
        Firstname = string.Empty;
        Lastname = string.Empty;
    }

    public static Faker<User> Faker
    {
        get
        {
            return new Faker<User>()
                .RuleFor(x => x.TenantId, f => f.Random.Guid())
                .RuleFor(x => x.Firstname, f => f.Name.FirstName())
                .RuleFor(x => x.Lastname, f => f.Name.LastName());
        }
    }
}
