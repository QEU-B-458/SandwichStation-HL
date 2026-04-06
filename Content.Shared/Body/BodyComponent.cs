using System.Collections.Generic;
using Content.Shared.Body.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

/// <summary>
/// Component on the entity that "has" a body, and that oversees entities with the <see cref="OrganComponent"/> inside it.
/// </summary>
/// <seealso cref="BodySystem" />
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BodySystem), typeof(Systems.SharedBodySystem))]
public sealed partial class BodyComponent : Component
{
    public const string ContainerID = "body_organs";

    /// <summary>
    /// Relevant template to spawn for this body.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<BodyPrototype>? Prototype;

    /// <summary>
    /// The actual container with entities with <see cref="OrganComponent" /> in it
    /// </summary>
    [ViewVariables]
    public Container? Organs;

    /// <summary>
    /// Container that holds the root body part.
    /// </summary>
    [ViewVariables]
    public ContainerSlot RootContainer = default!;

    [ViewVariables]
    public string RootPartSlot => RootContainer.ID;

    [DataField, AutoNetworkedField]
    public SoundSpecifier GibSound = new SoundCollectionSpecifier("gib");

    /// <summary>
    /// The amount of legs required to move at full speed.
    /// If 0, then legs do not impact speed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int RequiredLegs;

    [ViewVariables]
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> LegEntities = new();
}

/// <summary>
/// Raised on organ entity, when it is inserted into a body
/// </summary>
[ByRefEvent]
public readonly record struct OrganGotInsertedEvent(EntityUid Target);

/// <summary>
/// Raised on organ entity, when it is removed from a body
/// </summary>
[ByRefEvent]
public readonly record struct OrganGotRemovedEvent(EntityUid Target);

/// <summary>
/// Raised on body entity, when an organ is inserted into it
/// </summary>
[ByRefEvent]
public readonly record struct OrganInsertedIntoEvent(EntityUid Organ);

/// <summary>
/// Raised on body entity, when an organ is removed from it
/// </summary>
[ByRefEvent]
public readonly record struct OrganRemovedFromEvent(EntityUid Organ);
