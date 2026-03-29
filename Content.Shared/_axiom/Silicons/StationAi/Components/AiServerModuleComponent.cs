using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._axiom.Silicons.StationAi.Components;

/// <summary>
/// A module board that can be inserted into an AI Controller Server
/// to grant the AI new capabilities. Follows the borg module pattern.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AiServerModuleComponent : Component
{
    /// <summary>
    /// The server this module is installed into, if any.
    /// </summary>
    [DataField]
    public EntityUid? InstalledServer;

    public bool Installed => InstalledServer != null;

    /// <summary>
    /// Unique identifier to prevent duplicate modules of the same type.
    /// </summary>
    [DataField(required: true)]
    public string ModuleId = string.Empty;

    /// <summary>
    /// What type of capability this module provides.
    /// </summary>
    [DataField(required: true)]
    public AiModuleType ModuleType;

    /// <summary>
    /// Action prototype to grant to the AI brain when this module is installed.
    /// The action is revoked when the module is removed. Null = no action.
    /// </summary>
    [DataField]
    public EntProtoId? GrantedAction;

    /// <summary>
    /// The spawned action entity, tracked so we can revoke it.
    /// </summary>
    [DataField]
    public EntityUid? GrantedActionEntity;
}

[Serializable, NetSerializable]
public enum AiModuleType : byte
{
    Power,
    Atmos,
    Cargo,
    Security,
    Auth,
    Boris
}

[ByRefEvent]
public readonly record struct AiModuleInstalledEvent(EntityUid ServerEnt);

[ByRefEvent]
public readonly record struct AiModuleUninstalledEvent(EntityUid ServerEnt);
