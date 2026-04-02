namespace Content.Shared._species.Shadekin.NullSpace;

[RegisterComponent]
public sealed partial class NullPhaseComponent : Component
{
    [DataField]
    public EntityUid? PhaseAction;
}
