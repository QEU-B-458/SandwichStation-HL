using Content.Shared._Sandwich.Silicons.StationAi.Components;
using Robust.Shared.Containers;

namespace Content.Shared._Sandwich.Silicons.StationAi;

public abstract class SharedAiNetworkSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] protected readonly SharedContainerSystem Container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AiNetworkServerComponent, ComponentStartup>(OnServerStartup);
    }

    private void OnServerStartup(EntityUid uid, AiNetworkServerComponent comp, ComponentStartup args)
    {
        comp.ModuleContainer = Container.EnsureContainer<Robust.Shared.Containers.Container>(uid, comp.ModuleContainerId);
        OnServerInit(uid, comp);
    }

    /// <summary>
    /// Called after module container is initialized. Override in server system for extra init logic.
    /// </summary>
    protected virtual void OnServerInit(EntityUid uid, AiNetworkServerComponent comp) { }

    public bool IsGridAuthorized(EntityUid coreUid, EntityUid? gridUid)
    {
        if (gridUid == null)
            return false;

        // Own grid is always fine
        var coreGrid = _xforms.GetGrid(coreUid);
        if (coreGrid == gridUid)
            return true;

        // Check server's authorized grid list
        var serverQuery = EntityQueryEnumerator<AiNetworkServerComponent>();
        while (serverQuery.MoveNext(out var serverUid, out var server))
        {
            if (server.LinkedCore != coreUid)
                continue;

            var netGrid = GetNetEntity(gridUid.Value);
            return server.AuthorizedGrids.Contains(netGrid);
        }

        // No server, own grid only
        return false;
    }

    /// <summary>
    /// Checks if a specific module type is installed and active in the server linked to this core.
    /// </summary>
    public bool HasModule(EntityUid coreUid, AiModuleType moduleType)
    {
        var serverQuery = EntityQueryEnumerator<AiNetworkServerComponent>();
        while (serverQuery.MoveNext(out var serverUid, out var server))
        {
            if (server.LinkedCore != coreUid)
                continue;

            foreach (var moduleEnt in server.ModuleContainer.ContainedEntities)
            {
                if (TryComp<AiServerModuleComponent>(moduleEnt, out var module) &&
                    module.ModuleType == moduleType)
                    return true;
            }

            return false;
        }

        return false;
    }
}
