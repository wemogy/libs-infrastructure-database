using System;
using Wemogy.Core.Errors;

namespace Wemogy.Infrastructure.Database.Cosmos.Models
{
    public class PartitionKey<TPartitionKey>
    {
        public Microsoft.Azure.Cosmos.PartitionKey CosmosPartitionKey { get; }

        public PartitionKey(TPartitionKey partitionKey)
        {
            if (partitionKey is double partitionKeyDouble)
            {
                CosmosPartitionKey = new Microsoft.Azure.Cosmos.PartitionKey(partitionKeyDouble);
            }
            else if (partitionKey is bool partitionKeyBool)
            {
                CosmosPartitionKey = new Microsoft.Azure.Cosmos.PartitionKey(partitionKeyBool);
            }
            else if (partitionKey is null)
            {
                throw Error.Unexpected("PartitionKeyValueNull", $"The partition key can not be null");
            }
            else
            {
                CosmosPartitionKey = new Microsoft.Azure.Cosmos.PartitionKey(partitionKey.ToString());
            }
        }
    }
}
