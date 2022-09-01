using System;

namespace Wemogy.Infrastructure.Database.Core.Abstractions
{
    public abstract class EntityBase : EntityBase<Guid>
    {
        protected EntityBase()
            : base(Guid.NewGuid())
        {
        }
    }
}
