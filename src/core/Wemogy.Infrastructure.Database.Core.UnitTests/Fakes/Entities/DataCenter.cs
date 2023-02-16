using System;
using Bogus;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities
{
    public class DataCenter : EntityBase
    {
        public string Location { get; set; }

        [PartitionKey]
        public string PartitionKey { get; set; }

        public DataCenter()
            : base(Guid.NewGuid().ToString())
        {
            Location = string.Empty;
            PartitionKey = string.Empty;
        }

        public static Faker<DataCenter> Faker
        {
            get
            {
                return new Faker<DataCenter>()
                    .RuleFor(
                        x => x.PartitionKey,
                        f => f.Random.Guid().ToString())
                    .RuleFor(
                        x => x.Location,
                        f => f.Address.Country());
            }
        }
    }
}
