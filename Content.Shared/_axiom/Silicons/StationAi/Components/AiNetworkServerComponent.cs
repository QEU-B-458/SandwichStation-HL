using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._axiom.Silicons.StationAi.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AiNetworkServerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? LinkedCore;

    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> ConnectedRelays = new();

    /// <summary>
    /// Grids the AI can access. Rebuilt when relays change.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<NetEntity> AuthorizedGrids = new();

    #region Modules

    /// <summary>
    /// Whitelist for what module entities can be inserted.
    /// </summary>
    [DataField]
    public EntityWhitelist? ModuleWhitelist;

    /// <summary>
    /// Maximum number of modules this server can hold.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxModules = 4;

    /// <summary>
    /// Container ID for the module container.
    /// </summary>
    [DataField]
    public string ModuleContainerId = "ai_server_module";

    [ViewVariables]
    public Container ModuleContainer = default!;

    public int ModuleCount => ModuleContainer.ContainedEntities.Count;

    #endregion
}

/// <summary>
/// Raised on the server entity when its linked core changes.
/// </summary>
[ByRefEvent]
public readonly record struct AiServerCoreLinkChangedEvent(EntityUid ServerUid);
