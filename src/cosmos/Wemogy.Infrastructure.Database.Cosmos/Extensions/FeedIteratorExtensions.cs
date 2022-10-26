using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Wemogy.Infrastructure.Database.Cosmos.Extensions
{
    public static class FeedIteratorExtensions
    {
        public static async Task IterateAsync<T>(this FeedIterator<T> feedIterator, Func<T, Task> callback,
            CancellationToken cancellationToken)
        {
            // Asynchronous query execution
            while (feedIterator.HasMoreResults)
                foreach (var item in await feedIterator.ReadNextAsync(cancellationToken))
                {
                    await callback(item);
                }
        }
    }
}
