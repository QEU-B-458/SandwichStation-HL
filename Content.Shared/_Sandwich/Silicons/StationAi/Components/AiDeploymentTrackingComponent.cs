using Robust.Shared.GameStates;

namespace Content.Shared._Sandwich.Silicons.StationAi.Components;

/// <summary>
/// Added to the StationAiBrain spawned during deployment upload.
/// Tracks the source borg and interface so the AI can shunt back.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AiDeploymentTrackingComponent : Component
{
    /// <summary>
    /// The deployment borg the AI uploaded from.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SourceBorg;

    /// <summary>
    /// The AI Interface the borg was docked at.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SourceInterface;

    /// <summary>
    /// The "Return to Deployment Borg" action entity. Tracked for clean removal.
    /// </summary>
    [DataField]
    public EntityUid? ShuntActionEntity;
}
