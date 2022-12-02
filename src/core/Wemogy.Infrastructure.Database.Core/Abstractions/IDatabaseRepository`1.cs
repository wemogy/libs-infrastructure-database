using Wemogy.Core.ValueObjects.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

/// <summary>
///     Interface used to abstract access to a persistence store provider, regardless of its type.
///     Can be used to abstract access for example to a cosmos-db database, or an in-memory store.
/// </summary>
/// <typeparam name="TEntity">Generic abstraction of the entity type being used in the repository</typeparam>
public partial interface IDatabaseRepository<TEntity> : IDatabaseRepositoryBase
    where TEntity : IEntityBase
{
    /// <summary>
    ///     Defines if soft-deletion is supported for the corresponding entity.
    /// </summary>
    IEnabledState SoftDelete { get; }
}
