using Robust.Shared.GameStates;

namespace Content.Shared._Sandwich.Silicons.StationAi.Components;

/// <summary>
/// When placed on an entity, the Station AI requires the specified module type
/// installed in its linked AI Controller Server to interact with this entity.
/// Without the module, BUI interactions and radial actions are blocked.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AiModuleRequiredComponent : Component
{
    /// <summary>
    /// Which module type the AI needs to interact with this entity.
    /// </summary>
    [DataField(required: true)]
    public AiModuleType ModuleType;
}
