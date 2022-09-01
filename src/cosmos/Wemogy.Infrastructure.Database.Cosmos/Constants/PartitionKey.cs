namespace Wemogy.Infrastructure.Database.Cosmos.Constants
{
    public static class PartitionKey
    {
        public static readonly Models.PartitionKey<string> Global = new Models.PartitionKey<string>(PartitionKeyValue.Global);
    }
}
