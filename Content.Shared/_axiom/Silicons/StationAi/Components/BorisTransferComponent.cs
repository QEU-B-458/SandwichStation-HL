using Robust.Shared.GameStates;

namespace Content.Shared._axiom.Silicons.StationAi.Components;

/// <summary>
/// Added to a borg when an AI mind transfers into it via the Boris system.
/// Tracks the original AI brain so the mind can return.
/// Also added to the AI brain to track that it's currently transferred out.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BorisTransferComponent : Component
{
    /// <summary>
    /// The AI brain entity the mind came from (set on the borg).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SourceBrain;

    /// <summary>
    /// The borg entity the mind transferred into (set on the brain).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? TargetBorg;

    /// <summary>
    /// The "Return to Core" action entity granted on the borg. Tracked so we can remove only this action.
    /// </summary>
    [DataField]
    public EntityUid? ReturnActionEntity;
}
