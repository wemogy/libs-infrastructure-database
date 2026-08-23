using System;
using System.Collections.Generic;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    /// <summary>
    ///     A single operation of an <see cref="InMemoryTransactionalBatch{TEntity}"/>. The batch only
    ///     records its operations; they are validated and applied when the batch is executed.
    /// </summary>
    /// <typeparam name="TEntity">The entity type the operation acts on</typeparam>
    internal class InMemoryTransactionalBatchOperation<TEntity>
        where TEntity : class
    {
        private InMemoryTransactionalBatchOperation(
            InMemoryTransactionalBatchOperationKind kind,
            TEntity? entity,
            string? id,
            IReadOnlyList<DatabasePatchOperation>? patchOperations = null,
            Func<TEntity, bool>? patchCondition = null)
        {
            Kind = kind;
            Entity = entity;
            Id = id;
            PatchOperations = patchOperations;
            PatchCondition = patchCondition;
        }

        public InMemoryTransactionalBatchOperationKind Kind { get; }

        /// <summary>
        ///     The entity to write, null for a <see cref="InMemoryTransactionalBatchOperationKind.Delete"/>.
        /// </summary>
        public TEntity? Entity { get; }

        /// <summary>
        ///     The id to delete or to patch, null for every other kind, which carries the id on
        ///     its entity.
        /// </summary>
        public string? Id { get; }

        /// <summary>
        ///     The operations to apply, set for a <see cref="InMemoryTransactionalBatchOperationKind.Patch"/>
        ///     only.
        /// </summary>
        public IReadOnlyList<DatabasePatchOperation>? PatchOperations { get; }

        /// <summary>
        ///     The compiled condition that has to hold for a patch to be applied, null when the
        ///     patch is unconditional.
        /// </summary>
        public Func<TEntity, bool>? PatchCondition { get; }

        public static InMemoryTransactionalBatchOperation<TEntity> Create(TEntity entity)
        {
            return new InMemoryTransactionalBatchOperation<TEntity>(
                InMemoryTransactionalBatchOperationKind.Create,
                entity,
                null);
        }

        public static InMemoryTransactionalBatchOperation<TEntity> Replace(TEntity entity)
        {
            return new InMemoryTransactionalBatchOperation<TEntity>(
                InMemoryTransactionalBatchOperationKind.Replace,
                entity,
                null);
        }

        public static InMemoryTransactionalBatchOperation<TEntity> Upsert(TEntity entity)
        {
            return new InMemoryTransactionalBatchOperation<TEntity>(
                InMemoryTransactionalBatchOperationKind.Upsert,
                entity,
                null);
        }

        public static InMemoryTransactionalBatchOperation<TEntity> Delete(string id)
        {
            return new InMemoryTransactionalBatchOperation<TEntity>(
                InMemoryTransactionalBatchOperationKind.Delete,
                null,
                id);
        }

        public static InMemoryTransactionalBatchOperation<TEntity> Patch(
            string id,
            IReadOnlyList<DatabasePatchOperation> patchOperations,
            Func<TEntity, bool>? patchCondition)
        {
            return new InMemoryTransactionalBatchOperation<TEntity>(
                InMemoryTransactionalBatchOperationKind.Patch,
                null,
                id,
                patchOperations,
                patchCondition);
        }
    }
}
