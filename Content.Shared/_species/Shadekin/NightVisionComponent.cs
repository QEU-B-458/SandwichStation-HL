namespace Content.Shared._species.Shadekin;

/// <summary>
/// Marker component for entities that have night vision active.
/// Added/removed by the Shadekin system based on light exposure.
/// </summary>
[RegisterComponent]
public sealed partial class NightVisionComponent : Component;
