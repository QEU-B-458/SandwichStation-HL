using Robust.Shared.GameStates;

namespace Content.Shared._axiom.Silicons.StationAi.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AiRelayComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? LinkedServer;

    [DataField, AutoNetworkedField]
    public bool Active = true;
}
