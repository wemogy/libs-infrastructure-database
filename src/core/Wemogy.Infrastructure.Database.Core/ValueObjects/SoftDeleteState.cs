using Wemogy.Core.ValueObjects.States;

namespace Wemogy.Infrastructure.Database.Core.ValueObjects
{
    public class SoftDeleteState : EnabledState
    {
        public SoftDeleteState(bool isEnabled)
            : base(isEnabled)
        {
        }
    }
}
