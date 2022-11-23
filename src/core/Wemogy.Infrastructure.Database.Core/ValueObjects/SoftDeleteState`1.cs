using Wemogy.Core.ValueObjects.States;
using Wemogy.Infrastructure.Database.Core.Extensions;

namespace Wemogy.Infrastructure.Database.Core.ValueObjects;

public class SoftDeleteState<TEntity> : EnabledState
{
    public SoftDeleteState(bool isEnabled)
        : base(isEnabled)
    {
        if (isEnabled)
        {
            typeof(TEntity).ThrowIfNotSoftDeletable();
        }
    }

    public new void Enable()
    {
        typeof(TEntity).ThrowIfNotSoftDeletable();
        base.Enable();
    }
}
