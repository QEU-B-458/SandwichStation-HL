using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._axiom.Silicons.StationAi;

/// <summary>
/// Raised on the deployment borg when the player presses "Upload to Core".
/// </summary>
public sealed partial class AiDeploymentUploadActionEvent : InstantActionEvent { }

/// <summary>
/// Raised on the AI brain (in the core) when the player presses "Return to Deployment Borg".
/// </summary>
public sealed partial class AiDeploymentShuntActionEvent : InstantActionEvent { }

/// <summary>
/// Raised on the deployment borg when the player presses "Toggle ID Card".
/// Ejects the card to hand or stores it back in the internal slot.
/// </summary>
public sealed partial class AiDeploymentToggleIdCardActionEvent : InstantActionEvent { }
