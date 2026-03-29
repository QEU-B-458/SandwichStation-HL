using Content.Server._axiom.Power.Systems;

namespace Content.Server._axiom.Power.Components;

[RegisterComponent, Access(typeof(DockablePowerSystem))]
public sealed partial class DockablePowerComponent : Component
{
    [DataField]
    public string DockNodeName = string.Empty;
}
