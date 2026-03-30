using Content.Shared.Damage.Events;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Containers;

namespace Content.Server._species.Oni
{
    public sealed class OniSystem : EntitySystem
    {
        [Dependency] private readonly SharedGunSystem _gunSystem = default!;

        private const double GunInaccuracyFactor = 17.0;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<OniComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
            SubscribeLocalEvent<OniComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
            SubscribeLocalEvent<OniComponent, MeleeHitEvent>(OnOniMeleeHit);
            SubscribeLocalEvent<HeldByOniComponent, MeleeHitEvent>(OnHeldMeleeHit);
            SubscribeLocalEvent<HeldByOniComponent, StaminaMeleeHitEvent>(OnStamHit);
            SubscribeLocalEvent<HeldByOniComponent, GunRefreshModifiersEvent>(OnHeldGunRefresh);
        }

        private void OnEntInserted(EntityUid uid, OniComponent component, EntInsertedIntoContainerMessage args)
        {
            var heldComp = EnsureComp<HeldByOniComponent>(args.Entity);
            heldComp.Holder = uid;

            if (HasComp<GunComponent>(args.Entity))
            {
                _gunSystem.RefreshModifiers(args.Entity);
            }
        }

        private void OnEntRemoved(EntityUid uid, OniComponent component, EntRemovedFromContainerMessage args)
        {
            RemComp<HeldByOniComponent>(args.Entity);

            if (HasComp<GunComponent>(args.Entity))
            {
                _gunSystem.RefreshModifiers(args.Entity);
            }
        }

        private void OnHeldGunRefresh(EntityUid uid, HeldByOniComponent component, ref GunRefreshModifiersEvent args)
        {
            var gun = args.Gun.Comp;

            if (TryComp<GunWieldBonusComponent>(uid, out var bonus) && HasComp<WieldableComponent>(uid))
            {
                args.MinAngle += (gun.MinAngle + bonus.MinAngle) * GunInaccuracyFactor;
                args.AngleIncrease += (gun.AngleIncrease + bonus.AngleIncrease) * GunInaccuracyFactor;
                args.MaxAngle += (gun.MaxAngle + bonus.MaxAngle) * GunInaccuracyFactor;
            }
            else
            {
                args.MinAngle += gun.MinAngle * GunInaccuracyFactor;
                args.AngleIncrease += gun.AngleIncrease * GunInaccuracyFactor;
                args.MaxAngle += gun.MaxAngle * GunInaccuracyFactor;
            }
        }

        private void OnOniMeleeHit(EntityUid uid, OniComponent component, MeleeHitEvent args)
        {
            args.ModifiersList.Add(component.MeleeModifiers);
        }

        private void OnHeldMeleeHit(EntityUid uid, HeldByOniComponent component, MeleeHitEvent args)
        {
            if (!TryComp<OniComponent>(component.Holder, out var oni))
                return;

            args.ModifiersList.Add(oni.MeleeModifiers);
        }

        private void OnStamHit(EntityUid uid, HeldByOniComponent component, StaminaMeleeHitEvent args)
        {
            if (!TryComp<OniComponent>(component.Holder, out var oni))
                return;

            args.Multiplier *= oni.StamDamageMultiplier;
        }
    }
}
