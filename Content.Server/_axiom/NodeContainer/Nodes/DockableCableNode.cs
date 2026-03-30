using Content.Server.Power.Nodes;
using Content.Server.Shuttles.Components;
using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;

namespace Content.Server.NodeContainer.Nodes;

[DataDefinition]
public sealed partial class DockableCableNode : CableNode
{
    public override IEnumerable<Node> GetReachableNodes(
        Entity<TransformComponent> xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        Entity<MapGridComponent>? grid,
        IEntityManager entMan)
    {
        // Normal cable grid connectivity on this grid.
        foreach (var node in base.GetReachableNodes(xform, nodeQuery, xformQuery, grid, entMan))
        {
            yield return node;
        }

        if (!xform.Comp.Anchored || grid == null)
            yield break;

        // Cross-grid bridge when docked.
        if (entMan.TryGetComponent(Owner, out DockingComponent? docking)
            && docking.DockedWith != null
            && nodeQuery.TryComp(docking.DockedWith, out var otherNode))
        {
            foreach (var node in otherNode.Nodes.Values)
            {
                if (node is DockableCableNode cable)
                    yield return cable;
            }
        }
    }
}
