using Content.Shared.Access;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sandwich.Silicons.StationAi.Components;

/// <summary>
/// A borg module that allows the borg to be paired to an AI server
/// via a 4-digit code from the Boris Control Module.
/// When paired, the borg's own access is stripped and replaced with
/// access from the AI's Auth module ID card.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BorisModuleComponent : Component
{
    /// <summary>
    /// The AI server this borg is paired to, if any.
    /// Set when crew enters the correct 4-digit pairing code.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? PairedServer;

    /// <summary>
    /// The Boris Control Module entity this borg is linked to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? PairedControlModule;

    /// <summary>
    /// Backup of the borg's original access tags before Boris pairing stripped them.
    /// Restored when the borg is unpaired so regular borg access is preserved.
    /// Groups are expanded into tags at MapInit, so we only back up the final tags.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<AccessLevelPrototype>> OriginalAccessTags = new();
}
