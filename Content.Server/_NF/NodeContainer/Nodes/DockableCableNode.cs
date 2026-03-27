using Content.Server.Power.Nodes;
using Content.Server.Shuttles.Components;
using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;

namespace Content.Server.NodeContainer.Nodes;

[DataDefinition]
public sealed partial class DockableCableNode : CableNode
{
    public override IEnumerable<Node> GetReachableNodes(TransformComponent xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        MapGridComponent? grid,
        IEntityManager entMan)
    {
        foreach (var node in base.GetReachableNodes(xform, nodeQuery, xformQuery, grid, entMan))
        {
            yield return node;
        }

        if (!xform.Anchored || grid == null)
            yield break;

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
