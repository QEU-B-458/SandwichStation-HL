using Content.Server.Shuttles.Components;
using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;

namespace Content.Server.NodeContainer.Nodes;

/// <summary>
/// A cable node that also connects across docked grids.
/// Cannot subclass CableNode (sealed), so this extends Node directly
/// and replicates the docking cross-link logic only.
/// The entity must also have a regular CableNode for normal power grid participation.
/// </summary>
[DataDefinition]
public sealed partial class DockableCableNode : Node
{
    public override IEnumerable<Node> GetReachableNodes(
        Entity<TransformComponent> xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        Entity<MapGridComponent>? grid,
        IEntityManager entMan)
    {
        if (!xform.Comp.Anchored || grid == null)
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
