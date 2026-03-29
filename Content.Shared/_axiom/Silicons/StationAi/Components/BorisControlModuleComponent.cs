using Robust.Shared.GameStates;

namespace Content.Shared._axiom.Silicons.StationAi.Components;

/// <summary>
/// Marks an AI server module as a Boris Control Module.
/// Generates a 4-digit pairing code that boris borg modules use to link.
/// Tracks all paired borgs for mind transfer control.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BorisControlModuleComponent : Component
{
    /// <summary>
    /// The 4-digit pairing code. Generated on MapInit, persists through remove/re-insert.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string PairingCode = string.Empty;

    /// <summary>
    /// All borg entities currently paired to this control module.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> PairedBorgs = new();
}
