using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

/// <summary>
/// Marks an entity as being able to be inserted into an entity with <seealso cref="BodyComponent" />.
/// </summary>
/// <seealso cref="BodySystem" />
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BodySystem), typeof(Systems.SharedBodySystem))]
public sealed partial class OrganComponent : Component, ISurgeryToolComponent
{
    /// <summary>
    /// The body entity containing this organ, if any
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Body;

    /// <summary>
    /// Relevant body this organ originally belonged to.
    /// </summary>
    [DataField]
    public EntityUid? OriginalBody;

    /// <summary>
    /// What kind of organ is this, if any
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype>? Category;

    /// <summary>
    /// Surgery-facing slot id used by the upstream tool logic.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public string SlotId = "";

    [DataField, AlwaysPushInheritance]
    public string ToolName { get; set; } = "An organ";

    [DataField, AlwaysPushInheritance]
    public float Speed { get; set; } = 1f;

    /// <summary>
    /// If true, the organ will not heal an entity when transplanted into them.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool? Used { get; set; }

    /// <summary>
    /// When attached, the organ will ensure these components on the entity, and delete them on removal.
    /// </summary>
    [DataField]
    public ComponentRegistry? OnAdd;

    /// <summary>
    /// When removed, the organ will ensure these components on the entity, and add them on removal.
    /// </summary>
    [DataField]
    public ComponentRegistry? OnRemove;

    /// <summary>
    /// Is this organ working or not?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    /// Can this organ be enabled or disabled?
    /// </summary>
    [DataField]
    public bool CanEnable = true;
}
