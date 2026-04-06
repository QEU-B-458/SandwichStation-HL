using Content.Shared.Hands.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.Body;

[RegisterComponent, NetworkedComponent]
[Access(typeof(BodyPartHandsSystem))]
public sealed partial class BodyPartHandsComponent : Component
{
    [DataField(required: true)]
    public Dictionary<string, Hand> Hands = new();
}
