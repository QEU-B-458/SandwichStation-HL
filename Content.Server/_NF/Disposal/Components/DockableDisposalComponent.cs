using Content.Server._NF.Disposal.Systems;

namespace Content.Server._NF.Disposal.Components;

[RegisterComponent, Access(typeof(DockableDisposalSystem))]
public sealed partial class DockableDisposalComponent : Component
{
}
