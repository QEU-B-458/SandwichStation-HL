using Content.Server._axiom.Disposal.Systems;

namespace Content.Server._axiom.Disposal.Components;

[RegisterComponent, Access(typeof(DockableDisposalSystem))]
public sealed partial class DockableDisposalComponent : Component
{
}
