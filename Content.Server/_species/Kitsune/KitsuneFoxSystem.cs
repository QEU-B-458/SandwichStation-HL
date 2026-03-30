using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared._species.Kitsune;
using Content.Server.Damage.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Stunnable;

namespace Content.Server._species.Kitsune;

public sealed class KitsuneFoxSystem : EntitySystem
{
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KitsuneFoxComponent, StunnedEvent>(OnStunned);
    }

    private void OnStunned(Entity<KitsuneFoxComponent> ent, ref StunnedEvent args)
    {
        if (!TryComp<PolymorphedEntityComponent>(ent, out var polymorph) || polymorph.Parent is not { } parent)
            return;
        var staminaDamage = _stamina.GetStaminaDamage(ent);
        _stamina.TakeStaminaDamage(parent, staminaDamage);
        _polymorph.Revert(ent.Owner);
    }
}
