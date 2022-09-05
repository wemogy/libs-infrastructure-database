using System;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public abstract class GlobalEntityBase : GlobalEntityBase<Guid>
{
    protected GlobalEntityBase()
        : base(Guid.NewGuid())
    {
    }
}
