using Content.Server._NF.Power.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Shuttles.Events;
using Content.Shared._NF.Power.Visuals;
using Content.Shared.NodeContainer;
using Robust.Server.GameObjects;

namespace Content.Server._NF.Power.Systems;

public sealed class DockablePowerSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly NodeGroupSystem _nodeGroup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DockablePowerComponent, DockEvent>(OnDock);
        SubscribeLocalEvent<DockablePowerComponent, UndockEvent>(OnUndock);
    }

    private void OnDock(Entity<DockablePowerComponent> ent, ref DockEvent args)
    {
        if (string.IsNullOrEmpty(ent.Comp.DockNodeName) ||
            !TryComp(ent, out NodeContainerComponent? nodeContainer) ||
            !_nodeContainer.TryGetNode(nodeContainer, ent.Comp.DockNodeName, out DockableCableNode? dockableCable))
            return;

        _nodeGroup.QueueReflood(dockableCable);
        _appearance.SetData(ent, DockablePowerVisuals.Docked, true);
    }

    private void OnUndock(Entity<DockablePowerComponent> ent, ref UndockEvent args)
    {
        if (string.IsNullOrEmpty(ent.Comp.DockNodeName) ||
            !TryComp(ent, out NodeContainerComponent? nodeContainer) ||
            !_nodeContainer.TryGetNode(nodeContainer, ent.Comp.DockNodeName, out DockableCableNode? dockableCable))
            return;

        _nodeGroup.QueueNodeRemove(dockableCable);
        _appearance.SetData(ent, DockablePowerVisuals.Docked, false);
    }
}
