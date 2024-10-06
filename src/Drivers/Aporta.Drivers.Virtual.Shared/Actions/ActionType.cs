using Aporta.Shared.Models;

namespace Aporta.Drivers.Virtual.Shared.Actions
{
    public enum ActionType
    {
        [Description("Badge Swipe")]
        BadgeSwipe,
        AddUpdateReader,
        EditReader,
        RemoveReader,
        AddInput,
        RemoveInput,
        AddOutput,
        RemoveOutput,
        
    }
}
