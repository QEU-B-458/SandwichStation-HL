namespace Content.Shared._axiom.Silicons.StationAi.Components;

/// <summary>
/// Marker component on AxiomAiBrain.
/// Identifies Axiom Station AI brains so SharedAxiomAiSystem
/// can apply network-based grid authorization instead of vanilla same-grid-only checks.
/// </summary>
[RegisterComponent]
public sealed partial class AxiomAiComponent : Component;
