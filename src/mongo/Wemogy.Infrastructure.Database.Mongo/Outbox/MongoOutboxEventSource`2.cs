using System;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Outbox.Entities;

namespace Wemogy.Infrastructure.Database.Mongo.Outbox
{
    internal class MongoOutboxEventSource<TOutboxEvent, TPayload> : IOutboxEventSource<TOutboxEvent, TPayload>
        where TOutboxEvent : OutboxEventBase<TPayload>, IEntityBase
        where TPayload : class
    {
        public Task StartAsync(Func<TOutboxEvent, Task> onEvent, CancellationToken cancellationToken)
        {
            throw new NotSupportedException(
                "Transactional Outbox is not supported for MongoDB. Use Cosmos DB or InMemory provider.");
        }
    }
}
