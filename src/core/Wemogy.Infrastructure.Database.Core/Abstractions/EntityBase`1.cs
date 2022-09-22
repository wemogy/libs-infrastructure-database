using System;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.Abstractions
{
    public abstract class EntityBase<TId> : IEntityBase<TId>
        where TId : IEquatable<TId>
    {
        [Id]
        public TId Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        [SoftDeleteFlag]
        public bool IsDeleted { get; set; }

        protected EntityBase(TId id)
        {
            Id = id;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
