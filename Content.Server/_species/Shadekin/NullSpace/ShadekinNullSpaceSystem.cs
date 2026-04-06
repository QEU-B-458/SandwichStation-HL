using Content.Server.Atmos.Components;
using Content.Server.Ghost;
using Content.Shared.Actions;
using Content.Shared.Atmos;
using Content.Shared.Eye;
using Content.Shared.Light.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Toggleable;
using Content.Shared._species.Shadekin.NullSpace;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._species.Shadekin.NullSpace;

public sealed class ShadekinNullSpaceSystem : SharedShadekinNullSpaceSystem
{
    [Dependency] private readonly EyeSystem _eye = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly ContainerSystem _container = default!;

    private readonly EntProtoId _shadekinShadow = "ShadekinShadow";
    private readonly EntProtoId _phaseInEffect = "ShadekinPhaseInEffect";
    private readonly EntProtoId _phaseOutEffect = "ShadekinPhaseOutEffect";
    private readonly EntProtoId _nullPhaseAction = "NullPhaseAction";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NullPhaseComponent, ComponentStartup>(OnNullPhaseStartup);
        SubscribeLocalEvent<NullPhaseComponent, ComponentShutdown>(OnNullPhaseShutdown);
        SubscribeLocalEvent<NullPhaseComponent, ToggleActionEvent>(OnPhaseAction);
    }

    private void OnNullPhaseStartup(EntityUid uid, NullPhaseComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.PhaseAction, _nullPhaseAction, uid);
    }

    private void OnNullPhaseShutdown(EntityUid uid, NullPhaseComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.PhaseAction);

        if (TryComp<NullSpaceComponent>(uid, out var nullSpace) && nullSpace.Activated)
        {
            nullSpace.Activated = false;
            Dirty(uid, nullSpace);
            DisableNullspaceEffects(uid, nullSpace);
        }
    }

    private void OnPhaseAction(EntityUid uid, NullPhaseComponent component, ToggleActionEvent args)
    {
        if (!TryComp<NullSpaceComponent>(uid, out var nullSpace))
            return;

        if (TryComp<MobStateComponent>(uid, out var mobState) &&
            (mobState.CurrentState == MobState.Critical || mobState.CurrentState == MobState.Dead))
            return;

        if (_container.IsEntityInContainer(uid))
        {
            _popups.PopupEntity(Loc.GetString("phase-fail-generic"), uid, uid);
            return;
        }

        nullSpace.Activated = !nullSpace.Activated;
        Dirty(uid, nullSpace);

        BooNearbyLights(uid);
        SpawnAtPosition(_shadekinShadow, Transform(uid).Coordinates);

        if (nullSpace.Activated)
        {
            var effect = SpawnAtPosition(_phaseOutEffect, Transform(uid).Coordinates);
            Transform(effect).LocalRotation = Angle.Zero;
            EnableNullspaceEffects(uid, nullSpace);
        }
        else
        {
            var effect = SpawnAtPosition(_phaseInEffect, Transform(uid).Coordinates);
            Transform(effect).LocalRotation = Angle.Zero;
            DisableNullspaceEffects(uid, nullSpace);
        }

        _actions.SetToggled(component.PhaseAction, nullSpace.Activated);
        args.Handled = true;
    }

    protected override void OnMobStateChanged(EntityUid uid, NullSpaceComponent component, MobStateChangedEvent args)
    {
        base.OnMobStateChanged(uid, component, args);

        if (args.NewMobState != MobState.Critical && args.NewMobState != MobState.Dead)
            return;

        if (!component.Activated)
            return;

        DisableNullspaceEffects(uid, component);

        if (TryComp<NullPhaseComponent>(uid, out var phase))
            _actions.SetToggled(phase.PhaseAction, false);
    }

    private void EnableNullspaceEffects(EntityUid uid, NullSpaceComponent component)
    {
        var visibility = EnsureComp<VisibilityComponent>(uid);
        _visibility.RemoveLayer((uid, visibility), (int) VisibilityFlags.Normal, false);
        _visibility.AddLayer((uid, visibility), (int) VisibilityFlags.NullSpace, false);
        _visibility.RefreshVisibility(uid, visibility);

        if (TryComp<EyeComponent>(uid, out var eye))
            _eye.SetVisibilityMask(uid, eye.VisibilityMask | (int) VisibilityFlags.NullSpace, eye);

        var stealth = EnsureComp<StealthComponent>(uid);
        _stealth.SetVisibility(uid, 0.8f, stealth);
        _stealth.SetEnabled(uid, true, stealth);

        EnsureComp<PressureImmunityComponent>(uid);

        var weightless = EnsureComp<MovementIgnoreGravityComponent>(uid);
        weightless.Weightless = true;
        Dirty(uid, weightless);

        SuppressFactions(uid, component, true);
    }

    private void DisableNullspaceEffects(EntityUid uid, NullSpaceComponent component)
    {
        if (TryComp<VisibilityComponent>(uid, out var visibility))
        {
            _visibility.AddLayer((uid, visibility), (int) VisibilityFlags.Normal, false);
            _visibility.RemoveLayer((uid, visibility), (int) VisibilityFlags.NullSpace, false);
            _visibility.RefreshVisibility(uid, visibility);
        }

        if (TryComp<EyeComponent>(uid, out var eye))
            _eye.SetVisibilityMask(uid, (int) VisibilityFlags.Normal, eye);

        SuppressFactions(uid, component, false);

        RemComp<StealthComponent>(uid);
        RemComp<PressureImmunityComponent>(uid);
        RemComp<MovementIgnoreGravityComponent>(uid);
    }

    private void SuppressFactions(EntityUid uid, NullSpaceComponent component, bool suppress)
    {
        if (suppress)
        {
            component.SuppressedFactions.Clear();

            if (!TryComp<NpcFactionMemberComponent>(uid, out var factions))
                return;

            foreach (var faction in factions.Factions)
            {
                component.SuppressedFactions.Add(faction);
                _factions.RemoveFaction((uid, factions), faction, false);
            }

            return;
        }

        if (!TryComp<NpcFactionMemberComponent>(uid, out var factionComp))
            return;

        foreach (var faction in component.SuppressedFactions)
        {
            _factions.AddFaction((uid, factionComp), faction);
        }

        component.SuppressedFactions.Clear();
    }

    private void BooNearbyLights(EntityUid uid)
    {
        foreach (var light in _lookup.GetEntitiesInRange(uid, 5))
        {
            if (HasComp<PoweredLightComponent>(light))
                _ghost.DoGhostBooEvent(light);
        }
    }
}
