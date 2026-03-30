using Content.Shared._axiom.Silicons.StationAi.Components;
using Content.Shared.Interaction;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Shared._axiom.Silicons.StationAi;

/// <summary>
/// Overrides vanilla StationAI same-grid-only checks for AxiomAiBrain entities,
/// allowing interaction with doors and devices on relay-authorized grids.
/// Runs after SharedStationAiSystem so vanilla AI behavior is unchanged.
/// </summary>
public abstract class SharedAxiomAiSystem : EntitySystem
{
    [Dependency] private readonly SharedStationAiSystem _stationAi = default!;
    [Dependency] private readonly SharedAiNetworkSystem _aiNetwork = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAiWhitelistComponent, BoundUserInterfaceCheckRangeEvent>(
            OnAxiomAiBuiCheck,
            after: [typeof(SharedStationAiSystem)]);

        SubscribeLocalEvent<StationAiOverlayComponent, InRangeOverrideEvent>(
            OnAxiomAiInRange,
            after: [typeof(SharedStationAiSystem)]);
    }

    private void OnAxiomAiBuiCheck(Entity<StationAiWhitelistComponent> ent, ref BoundUserInterfaceCheckRangeEvent args)
    {
        // Only override if vanilla already set Fail — if it passed, nothing to do.
        if (args.Result == BoundUserInterfaceRangeResult.Pass)
            return;

        // Only applies to Axiom AI brains.
        if (!HasComp<AxiomAiComponent>(args.Actor))
            return;

        if (!_stationAi.TryGetCore(args.Actor, out var core))
            return;

        var targetGrid = Transform(args.Target).GridUid;
        if (_aiNetwork.IsGridAuthorized(core.Owner, targetGrid))
            args.Result = BoundUserInterfaceRangeResult.Pass;
    }

    private void OnAxiomAiInRange(Entity<StationAiOverlayComponent> ent, ref InRangeOverrideEvent args)
    {
        // Only override if vanilla left InRange false.
        if (args.InRange)
            return;

        // Only applies to Axiom AI brains.
        if (!HasComp<AxiomAiComponent>(args.User))
            return;

        if (!_stationAi.TryGetCore(args.User, out var core))
            return;

        var targetGrid = Transform(args.Target).GridUid;
        if (_aiNetwork.IsGridAuthorized(core.Owner, targetGrid))
            args.InRange = true;
    }
}
