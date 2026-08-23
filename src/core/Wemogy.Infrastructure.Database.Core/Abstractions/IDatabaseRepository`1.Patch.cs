using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Applies a partial update to a single document: only the fields the operations address
    ///     are written, the rest of the document is left alone and never sent.
    ///     <para>
    ///         When a condition is given, the document is only modified if it holds. The check and
    ///         the update are one atomic operation on the document, so a concurrent writer cannot
    ///         slip between them - which makes a conditional <c>Increment</c> a check-and-set that
    ///         needs neither a read nor a retry. A condition that does not hold throws a
    ///         <see cref="Wemogy.Core.Errors.Exceptions.ConflictErrorException"/> with the code
    ///         <c>PatchConditionNotMet</c> and applies nothing.
    ///     </para>
    /// </summary>
    /// <param name="id">The id of the document to patch</param>
    /// <param name="partitionKey">The partition key of the document to patch</param>
    /// <param name="operations">Adds the operations to apply, e.g. <c>p => p.Increment(x => x.Balance, 1)</c></param>
    /// <param name="condition">An optional condition that has to hold for the patch to be applied</param>
    /// <param name="cancellationToken">Token to cancel the patch</param>
    /// <returns>The document as it is after the patch</returns>
    Task<TEntity> PatchAsync(
        string id,
        string partitionKey,
        Action<IPatchOperations<TEntity>> operations,
        Expression<Func<TEntity, bool>>? condition = null,
        CancellationToken cancellationToken = default);
}
