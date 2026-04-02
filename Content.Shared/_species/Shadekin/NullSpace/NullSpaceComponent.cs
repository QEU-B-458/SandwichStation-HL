using System.Collections.Generic;
using Robust.Shared.GameStates;

namespace Content.Shared._species.Shadekin.NullSpace;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class NullSpaceComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Activated = false;

    [DataField]
    public List<string> SuppressedFactions = new();
}
