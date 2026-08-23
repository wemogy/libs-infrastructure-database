using System;
using Bogus;
using Newtonsoft.Json;
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

    /// <summary>
    ///     A long counter, so the patch tests have an integral field to increment. Left out of
    ///     <see cref="Faker"/> on purpose, so it defaults to zero for every other suite.
    /// </summary>
    public long Credits { get; set; }

    /// <summary>
    ///     The cap the conditional patch tests hold <see cref="Credits"/> against. Being a field
    ///     of the document, a condition can compare the two members against each other.
    /// </summary>
    public long CreditsCap { get; set; }

    /// <summary>
    ///     A double counter, so the patch tests have a floating point field to increment.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    ///     Serialized under a name of its own, so the patch tests can prove that a path is
    ///     resolved through the serializer instead of a hand-rolled camelCase.
    /// </summary>
    [JsonProperty("customLabel")]
    public string Label { get; set; }

    public User()
        : base(Guid.NewGuid().ToString())
    {
        TenantId = string.Empty;
        Firstname = string.Empty;
        Lastname = string.Empty;
        PrivateNote = string.Empty;
        Label = string.Empty;
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
