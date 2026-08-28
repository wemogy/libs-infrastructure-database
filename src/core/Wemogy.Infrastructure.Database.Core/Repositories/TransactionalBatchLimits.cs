namespace Wemogy.Infrastructure.Database.Core.Repositories;

/// <summary>
///     The limits every kind of batch is held to, in one place so the typed and the mixed-type batch
///     cannot come to disagree about them.
/// </summary>
internal static class TransactionalBatchLimits
{
    /// <summary>
    ///     Cosmos DB caps a transactional batch at 100 operations. The cap is enforced for every
    ///     provider, so a batch that runs against the in-memory provider in a test cannot be larger
    ///     than one that runs against Cosmos DB in production.
    /// </summary>
    public const int MaxOperationCount = 100;
}
