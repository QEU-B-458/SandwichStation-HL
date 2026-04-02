using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._species.Shadekin;

/// <summary>
/// Networked Shadekin night-vision state.
/// Kept species-local so the rest of the Shadekin feature set can stay grouped together.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NightVisionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active = true;

    [DataField, AutoNetworkedField]
    public float Strength = 1f;

    [DataField]
    public EntProtoId EffectPrototype = "EffectNightVision";
}
