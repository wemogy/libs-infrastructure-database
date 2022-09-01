using System;
using Wemogy.Infrastructure.Database.Core.CustomAttributes;

namespace Wemogy.Infrastructure.Database.Core.Abstractions
{
    public abstract class EntityBase<TId> : IEntityBase<TId>
        where TId : IEquatable<TId>
    {
        [Id]
        public TId Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // ToDo: discuss if we move this property into a new abstract subclass called SoftDeletableEntity
        public bool Deleted { get; set; }

        protected EntityBase(TId id)
        {
            Id = id;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
