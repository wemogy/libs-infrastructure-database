using System;
using Bogus;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.UnitTests.Extensions;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

public class User : EntityBase
{
    [PartitionKey]
    public string TenantId { get; set; }

    public string Firstname { get; set; }

    public string Lastname { get; set; }

    public string PrivateNote { get; set; }

    public User()
        : base(Guid.NewGuid().ToString())
    {
        TenantId = string.Empty;
        Firstname = string.Empty;
        Lastname = string.Empty;
        PrivateNote = string.Empty;
    }

    public static Faker<User> Faker
    {
        get
        {
            return new Faker<User>()
                .RuleFor(
                    x => x.CreatedAt,
                    f => f.Date.PastDate().Clone())
                .RuleFor(
                    x => x.UpdatedAt,
                    f => f.Date.PastDate().Clone())
                .RuleFor(
                    x => x.TenantId,
                    f => f.Random.Guid().ToString())
                .RuleFor(
                    x => x.Firstname,
                    f => f.Name.FirstName())
                .RuleFor(
                    x => x.Lastname,
                    f => f.Name.LastName())
                .RuleFor(x => x.IsDeleted, f => false);
        }
    }
}
