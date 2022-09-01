using Bogus;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Constants;
using Wemogy.Infrastructure.Database.Core.CustomAttributes;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

public class File : EntityBase
{
    public string Name { get; set; }

    [PartitionKey]
    public string PartitionKey { get; set; } = PartitionKeyDefaults.GlobalPartition;

    public File()
    {
        Name = string.Empty;
    }

    public static Faker<File> Faker
    {
        get
        {
            return new Faker<File>()
                .RuleFor(x => x.Name, f => f.System.FileName());
        }
    }
}
