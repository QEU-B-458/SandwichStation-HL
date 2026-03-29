using Robust.Shared.GameStates;

namespace Content.Shared._Sandwich.Silicons.StationAi.Components;

/// <summary>
/// Marks an AI server module as the Auth module.
/// The Auth module holds an ID card (via ItemSlots) and encryption keys
/// (via EncryptionKeyHolderComponent) that define the AI's access permissions
/// and radio channels.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AiAuthModuleComponent : Component
{
    /// <summary>
    /// The ItemSlot ID used for the ID card slot on this module.
    /// Must match the key in the ItemSlots YAML definition.
    /// </summary>
    public const string IdCardSlotId = "auth_id_card";
}
