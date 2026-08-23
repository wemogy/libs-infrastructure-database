namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    /// <summary>
    ///     The kind of an operation recorded by an <see cref="InMemoryTransactionalBatch{TEntity}"/>.
    /// </summary>
    internal enum InMemoryTransactionalBatchOperationKind
    {
        Create,
        Replace,
        Upsert,
        Delete,
        Patch
    }
}
