using Content.Server._axiom.Atmos.Systems;

namespace Content.Server._axiom.Atmos.Components;

[RegisterComponent, Access(typeof(DockablePipeSystem))]
public sealed partial class DockablePipeComponent : Component
{
    /// <summary>
    /// The names of the nodes that are available to dock (multi-layer support).
    /// </summary>
    [DataField]
    public List<string> DockNodeNames = new();

    /// <summary>
    /// Single node name (legacy single-pipe support).
    /// </summary>
    [DataField]
    public string DockNodeName = string.Empty;

    /// <summary>
    /// The name of the internal node.
    /// </summary>
    [DataField]
    public string InternalNodeName = string.Empty;
}
