using Content.Shared.CombatMode.Pacification;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._species.Shadekin.NullSpace;

public abstract class SharedShadekinNullSpaceSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;

    private readonly EntProtoId _shadekinShadow = "ShadekinShadow";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullSpaceComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<NullSpaceComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NullSpaceComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<NullSpaceComponent, BeforeThrowEvent>(OnBeforeThrow);
        SubscribeLocalEvent<NullSpaceComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<NullSpaceComponent, ShotAttemptedEvent>(OnShootAttempt);
        SubscribeLocalEvent<NullSpaceComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<NullSpaceComponent, PreventCollideEvent>(OnPreventCollision);
    }

    protected virtual void OnStartup(EntityUid uid, NullSpaceComponent component, MapInitEvent args)
    {
    }

    protected virtual void OnShutdown(EntityUid uid, NullSpaceComponent component, ComponentShutdown args)
    {
    }

    protected virtual void OnMobStateChanged(EntityUid uid, NullSpaceComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical && args.NewMobState != MobState.Dead)
            return;

        SpawnAtPosition(_shadekinShadow, Transform(uid).Coordinates);

        // Deactivate but keep the component so ability works after revival
        if (component.Activated)
        {
            component.Activated = false;
            Dirty(uid, component);
        }
    }

    private void OnShootAttempt(Entity<NullSpaceComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!ent.Comp.Activated)
            return;

        args.Cancel();
    }

    private void OnAttackAttempt(EntityUid uid, NullSpaceComponent component, AttackAttemptEvent args)
    {
        if (!component.Activated)
            return;

        if (HasComp<NullSpaceComponent>(args.Target) && TryComp<NullSpaceComponent>(args.Target, out var targetComp) && targetComp.Activated)
            return;

        args.Cancel();
    }

    private void OnBeforeThrow(Entity<NullSpaceComponent> ent, ref BeforeThrowEvent args)
    {
        if (!ent.Comp.Activated)
            return;

        var ev = new AttemptPacifiedThrowEvent(args.ItemUid, ent);
        RaiseLocalEvent(args.ItemUid, ref ev);
        if (ev.Cancelled)
            args.Cancelled = true;
    }

    private void OnInteractionAttempt(EntityUid uid, NullSpaceComponent component, ref InteractionAttemptEvent args)
    {
        if (!component.Activated)
            return;

        if (args.Target != null && TryComp<NullSpaceComponent>(args.Target, out var targetComp) && targetComp.Activated)
            return;

        args.Cancelled = true;
    }

    private void OnPreventCollision(EntityUid uid, NullSpaceComponent component, ref PreventCollideEvent args)
    {
        if (!component.Activated)
            return;

        if (!_net.IsClient)
            args.Cancelled = true;
    }
}
