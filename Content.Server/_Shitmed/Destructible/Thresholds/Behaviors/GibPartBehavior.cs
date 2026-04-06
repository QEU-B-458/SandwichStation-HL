using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using JetBrains.Annotations;

// Leaving this one in the default namespace because I am afraid to test it 
// in the Shitmed namespace lmao.
namespace Content.Server.Destructible.Thresholds.Behaviors;

[UsedImplicitly]
[DataDefinition]
public sealed partial class GibPartBehavior : IThresholdBehavior
{
    public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
    {
        if (!system.EntityManager.TryGetComponent(owner, out BodyPartComponent? part))
            return;

        var bodySystem = system.EntityManager.System<SharedBodySystem>();
        bodySystem.DropPart(owner, part);
        system.EntityManager.DeleteEntity(owner);
    }
}
