using System;

namespace Wemogy.Infrastructure.Database.Core.Abstractions
{
    public interface IDatabaseRepository<TEntity> : IDatabaseRepository<TEntity, Guid, Guid>
        where TEntity : IEntityBase<Guid>
    {
    }
}
