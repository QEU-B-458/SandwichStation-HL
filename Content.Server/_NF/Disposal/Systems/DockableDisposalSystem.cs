using Content.Server._NF.Disposal.Components;
using Content.Server.Shuttles.Events;
using Content.Shared._NF.Disposal.Visuals;
using Robust.Server.GameObjects;

namespace Content.Server._NF.Disposal.Systems;

public sealed class DockableDisposalSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DockableDisposalComponent, DockEvent>(OnDock);
        SubscribeLocalEvent<DockableDisposalComponent, UndockEvent>(OnUndock);
    }

    private void OnDock(Entity<DockableDisposalComponent> ent, ref DockEvent args)
    {
        _appearance.SetData(ent, DockableDisposalVisuals.Docked, true);
    }

    private void OnUndock(Entity<DockableDisposalComponent> ent, ref UndockEvent args)
    {
        _appearance.SetData(ent, DockableDisposalVisuals.Docked, false);
    }
}
