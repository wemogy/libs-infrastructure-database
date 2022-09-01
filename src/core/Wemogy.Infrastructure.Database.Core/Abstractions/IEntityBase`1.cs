using System;

namespace Wemogy.Infrastructure.Database.Core.Abstractions
{
    public interface IEntityBase<TId>
        where TId : IEquatable<TId>
    {
        public TId Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public bool Deleted { get; set; }
    }
}
