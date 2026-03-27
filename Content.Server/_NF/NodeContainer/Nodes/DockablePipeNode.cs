using Content.Server.Shuttles.Components;
using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;

namespace Content.Server.NodeContainer.Nodes;


[DataDefinition, Virtual]
public partial class DockablePipeNode : PipeNode
{

    public override IEnumerable<Node> GetReachableNodes(TransformComponent xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        MapGridComponent? grid,
        IEntityManager entMan)
    {
        foreach (var pipe in base.GetReachableNodes(xform, nodeQuery, xformQuery, grid, entMan))
        {
            yield return pipe;
        }

        if (!xform.Anchored || grid == null)
            yield break;

        if (entMan.TryGetComponent(Owner, out DockingComponent? docking)
            && docking.DockedWith != null
            && nodeQuery.TryComp(docking.DockedWith, out var otherNode))
        {
            foreach (var node in otherNode.Nodes.Values)
            {
                if (node is DockablePipeNode pipe && pipe.CurrentPipeLayer == CurrentPipeLayer)
                    yield return pipe;
            }
        }
    }
}
