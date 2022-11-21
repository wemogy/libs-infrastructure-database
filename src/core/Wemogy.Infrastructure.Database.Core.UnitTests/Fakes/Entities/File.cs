using System;
using Bogus;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Constants;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

public class File : EntityBase
{
    public string Name { get; set; }

    [PartitionKey]
    public string PartitionKey { get; set; } = PartitionKeyDefaults.GlobalPartition;

    public File()
        : base(Guid.NewGuid().ToString())
    {
        Name = string.Empty;
    }

    public static Faker<File> Faker =>
        new Faker<File>()
            .RuleFor(
                x => x.Name,
                f => f.System.FileName());
}
