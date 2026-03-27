using Content.Server._NF.Power.Systems;

namespace Content.Server._NF.Power.Components;

[RegisterComponent, Access(typeof(DockablePowerSystem))]
public sealed partial class DockablePowerComponent : Component
{
    [DataField]
    public string DockNodeName;
}
