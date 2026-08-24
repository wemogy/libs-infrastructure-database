using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> PatchAsync(
        string id,
        string partitionKey,
        Action<IPatchOperations<TEntity>> operations,
        Expression<Func<TEntity, bool>>? condition = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var patchedEntity = await _databaseRepository.PatchAsync(
                id,
                BuildComposedPartitionKey(partitionKey),
                operations,
                ComposeConditionPredicate(condition),
                cancellationToken);

            ReplacePartitionKey(
                patchedEntity,
                partitionKey);

            return patchedEntity;
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
    }

    /// <summary>
    ///     Prefixes the partition key values a patch condition compares against, so a condition
    ///     like <c>x => x.TenantId == "acme"</c> addresses the partition the tenant actually
    ///     writes to. A condition that does not mention the partition key is left as it is, apart
    ///     from the prefix guard every read path adds as well.
    /// </summary>
    private Expression<Func<TEntity, bool>>? ComposeConditionPredicate(Expression<Func<TEntity, bool>>? condition)
    {
        return condition == null ? null : BuildComposedPartitionKeyPredicate(condition);
    }
}
