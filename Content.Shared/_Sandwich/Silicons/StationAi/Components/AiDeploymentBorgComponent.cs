using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Sandwich.Silicons.StationAi.Components;

/// <summary>
/// Marker on the AI Deployment Borg chassis — a special borg that carries
/// the AI player to a core and uploads via an AI Interface.
/// Intentionally useless beyond transportation.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AiDeploymentBorgComponent : Component
{
    /// <summary>
    /// The AI Interface this borg uploaded from. Set during upload so we can shunt back.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SourceInterface;

    /// <summary>
    /// The "Upload to Core" action entity. Tracked for clean removal.
    /// </summary>
    [DataField]
    public EntityUid? UploadActionEntity;

    /// <summary>
    /// The "Toggle ID Card" action entity. Always present on a living deployment borg.
    /// </summary>
    [DataField]
    public EntityUid? IdCardActionEntity;

    /// <summary>
    /// The ItemSlot ID for the internal ID card storage.
    /// </summary>
    [DataField]
    public string IdCardSlotId = "id_card_slot";

    [DataField]
    public SoundSpecifier InsertSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");

    [DataField]
    public SoundSpecifier EjectSound = new SoundPathSpecifier("/Audio/Machines/tray_eject.ogg");
}
