using System;
using Bogus;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

public class User : EntityBase
{
    [PartitionKey]
    public Guid TenantId { get; set; }

    public string Firstname { get; set; }

    public string Lastname { get; set; }

    public string PrivateNote { get; set; }

    public User()
    {
        TenantId = Guid.Empty;
        Firstname = string.Empty;
        Lastname = string.Empty;
        PrivateNote = string.Empty;
    }

    public static Faker<User> Faker
    {
        get
        {
            return new Faker<User>()
                .RuleFor(x => x.CreatedAt, f => f.Date.Past().Clone())
                .RuleFor(x => x.UpdatedAt, f => f.Date.Past().Clone())
                .RuleFor(x => x.TenantId, f => f.Random.Guid())
                .RuleFor(x => x.Firstname, f => f.Name.FirstName())
                .RuleFor(x => x.Lastname, f => f.Name.LastName());
        }
    }
}
