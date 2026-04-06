using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Body.Part;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MovementBodyPartComponent : Component
{
    [DataField, AutoNetworkedField]
    public float WalkSpeed;

    [DataField, AutoNetworkedField]
    public float SprintSpeed;
}
