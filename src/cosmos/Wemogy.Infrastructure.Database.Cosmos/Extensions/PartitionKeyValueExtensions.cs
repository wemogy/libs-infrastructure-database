using Microsoft.Azure.Cosmos;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using CosmosPartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

namespace Wemogy.Infrastructure.Database.Cosmos.Extensions
{
    public static class PartitionKeyValueExtensions
    {
        /// <summary>
        ///     Translates a partition key into the one the Cosmos SDK addresses a partition with.
        ///     A key of several components becomes a hierarchical partition key, which the SDK
        ///     only accepts through <see cref="PartitionKeyBuilder"/>.
        /// </summary>
        /// <param name="partitionKey">The partition key to translate</param>
        /// <returns>The partition key of the Cosmos SDK</returns>
        public static CosmosPartitionKey ToCosmosPartitionKey(this PartitionKeyValue partitionKey)
        {
            // a single value keeps going through the typed wrapper, so the value it produces - and
            // the error an invalid one raises - does not depend on how deep the key is
            if (!partitionKey.IsHierarchical)
            {
                return new Models.PartitionKey<string>(partitionKey[0]).CosmosPartitionKey;
            }

            var partitionKeyBuilder = new PartitionKeyBuilder();
            foreach (var component in partitionKey.Components)
            {
                partitionKeyBuilder.Add(component);
            }

            return partitionKeyBuilder.Build();
        }
    }
}
