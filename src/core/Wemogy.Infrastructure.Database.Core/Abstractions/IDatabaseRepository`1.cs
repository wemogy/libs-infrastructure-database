using Wemogy.Core.ValueObjects.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity> : IDatabaseRepository
    where TEntity : IEntityBase
{
    IEnabledState SoftDelete { get; }
}
