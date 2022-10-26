using Microsoft.Azure.Cosmos;
using Wemogy.Core.Errors;

namespace Wemogy.Infrastructure.Database.Cosmos.Models
{
    public class PartitionKey<TPartitionKey>
    {
        public PartitionKey(TPartitionKey partitionKey)
        {
            if (partitionKey is double partitionKeyDouble)
            {
                CosmosPartitionKey = new PartitionKey(partitionKeyDouble);
            }
            else if (partitionKey is bool partitionKeyBool)
            {
                CosmosPartitionKey = new PartitionKey(partitionKeyBool);
            }
            else if (partitionKey is null)
            {
                throw Error.Unexpected(
                    "PartitionKeyValueNull",
                    "The partition key can not be null");
            }
            else
            {
                CosmosPartitionKey = new PartitionKey(partitionKey.ToString());
            }
        }

        public PartitionKey CosmosPartitionKey { get; }
    }
}
