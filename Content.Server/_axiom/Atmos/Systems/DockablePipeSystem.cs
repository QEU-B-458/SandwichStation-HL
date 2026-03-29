using Content.Server._axiom.Atmos.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Shuttles.Events;
using Content.Shared._axiom.Atmos.Visuals;
using Content.Shared.NodeContainer;
using Robust.Server.GameObjects;

namespace Content.Server._axiom.Atmos.Systems;

public sealed class DockablePipeSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly NodeGroupSystem _nodeGroup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DockablePipeComponent, DockEvent>(OnDock);
        SubscribeLocalEvent<DockablePipeComponent, UndockEvent>(OnUndock);
    }

    private void OnDock(Entity<DockablePipeComponent> ent, ref DockEvent args)
    {
        if (!TryComp(ent, out NodeContainerComponent? nodeContainer))
            return;

        foreach (var name in GetAllNodeNames(ent.Comp))
        {
            if (_nodeContainer.TryGetNode(nodeContainer, name, out DockablePipeNode? dockablePipe))
                _nodeGroup.QueueReflood(dockablePipe);
        }

        _appearance.SetData(ent, DockablePipeVisuals.Docked, true);
    }

    private void OnUndock(Entity<DockablePipeComponent> ent, ref UndockEvent args)
    {
        if (!TryComp(ent, out NodeContainerComponent? nodeContainer))
            return;

        foreach (var name in GetAllNodeNames(ent.Comp))
        {
            if (_nodeContainer.TryGetNode(nodeContainer, name, out DockablePipeNode? dockablePipe))
            {
                _nodeGroup.QueueNodeRemove(dockablePipe);
                dockablePipe.Air.Clear();
            }
        }

        _appearance.SetData(ent, DockablePipeVisuals.Docked, false);
    }

    private List<string> GetAllNodeNames(DockablePipeComponent comp)
    {
        var names = new List<string>(comp.DockNodeNames);
        if (!string.IsNullOrEmpty(comp.DockNodeName) && !names.Contains(comp.DockNodeName))
            names.Add(comp.DockNodeName);
        return names;
    }
}
