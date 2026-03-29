using Robust.Shared.GameStates;

namespace Content.Shared._Sandwich.Silicons.StationAi.Components;

/// <summary>
/// Placed on a recolored borg charger entity that serves as the upload/shunt
/// station for AI Deployment Borgs. Auto-links to an AI Controller Server on
/// the same grid.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AiInterfaceComponent : Component
{
    /// <summary>
    /// The AI Controller Server this interface is linked to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? LinkedServer;

    /// <summary>
    /// The deployment borg currently stored in this interface (inside EntityStorage).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? StoredBorg;

    /// <summary>
    /// Container ID for the EntityStorage that holds the borg.
    /// </summary>
    [DataField]
    public string StorageContainerId = "entity_storage";
}
