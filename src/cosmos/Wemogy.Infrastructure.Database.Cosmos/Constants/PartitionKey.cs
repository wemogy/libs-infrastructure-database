using Wemogy.Infrastructure.Database.Cosmos.Models;

namespace Wemogy.Infrastructure.Database.Cosmos.Constants
{
    public static class PartitionKey
    {
        public static readonly PartitionKey<string> Global = new PartitionKey<string>(PartitionKeyValue.Global);
    }
}
