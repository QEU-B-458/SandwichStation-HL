using Content.Server._NF.Atmos.Systems;

namespace Content.Server._NF.Atmos.Components;

[RegisterComponent, Access(typeof(DockablePipeSystem))]
public sealed partial class DockablePipeComponent : Component
{
    /// <summary>
    /// The names of the nodes that are available to dock.
    /// </summary>
    [DataField]
    public List<string> DockNodeNames = new();

    /// <summary>
    /// The name of the node that is available to dock.
    /// </summary>
    [DataField]
    public string DockNodeName = string.Empty;

    /// <summary>
    /// The name of the internal node
    /// </summary>
    [DataField]
    public string InternalNodeName = string.Empty;
}
