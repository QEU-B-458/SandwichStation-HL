using Content.Server.Mind;
using Content.Shared._axiom.Silicons.StationAi;
using Content.Shared._axiom.Silicons.StationAi.Components;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._axiom.Silicons.StationAi;

/// <summary>
/// Handles AI mind transfer to/from Boris-paired borgs.
/// Also handles the Boris Control action and BUI for the AI player.
/// </summary>
public sealed class BorisTransferSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Boris Control action — opens the Boris Control panel.
        SubscribeLocalEvent<StationAiHeldComponent, ToggleBorisControlEvent>(OnToggleBorisControl);

        // BUI messages on AI brain for Boris Control panel.
        SubscribeLocalEvent<StationAiHeldComponent, BorisTransferToBorgMessage>(OnTransferToBorg);
        SubscribeLocalEvent<StationAiHeldComponent, BorisReturnToCoreMessage>(OnReturnToCore);

        // Return to Core action (available on borg when transferred).
        SubscribeLocalEvent<BorisTransferComponent, BorisReturnToCoreActionEvent>(OnReturnToCoreAction);
    }

    // --- Boris Control Action ---

    private void OnToggleBorisControl(EntityUid uid, StationAiHeldComponent comp, ToggleBorisControlEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(uid, out var actor))
            return;

        args.Handled = true;
        _ui.TryToggleUi(uid, BorisControlUiKey.Key, actor.PlayerSession);
        UpdateBorisControlUi(uid);
    }

    // --- Mind Transfer: Brain → Borg ---

    private void OnTransferToBorg(EntityUid uid, StationAiHeldComponent comp, BorisTransferToBorgMessage args)
    {
        var borgUid = GetEntity(args.Target);

        // Validate: borg exists and has a paired Boris module pointing at our server.
        if (!Exists(borgUid) || !TryComp<BorgChassisComponent>(borgUid, out var chassis))
            return;

        // Check borg is paired via Boris module.
        if (!TryFindPairedBorisModule(borgUid, chassis, out _))
            return;

        // Check we're not already transferred.
        if (TryComp<BorisTransferComponent>(uid, out var existing) && existing.TargetBorg != null)
            return;

        // Get the AI player's mind.
        if (!_mind.TryGetMind(uid, out var mindId, out var mindComp))
            return;

        // Transfer mind from brain to borg.
        _mind.TransferTo(mindId, borgUid, ghostCheckOverride: true, createGhost: false, mindComp);

        // Track the transfer on both entities.
        var brainTransfer = EnsureComp<BorisTransferComponent>(uid);
        brainTransfer.TargetBorg = borgUid;
        Dirty(uid, brainTransfer);

        var borgTransfer = EnsureComp<BorisTransferComponent>(borgUid);
        borgTransfer.SourceBrain = uid;
        Dirty(borgUid, borgTransfer);

        // Grant "Return to Core" action on the borg.
        EntityUid? returnAction = null;
        _actions.AddAction(borgUid, ref returnAction, "ActionBorisReturnToCore");
        borgTransfer.ReturnActionEntity = returnAction;

        // Close the Boris Control UI.
        if (TryComp<ActorComponent>(borgUid, out var actor))
            _ui.CloseUi(uid, BorisControlUiKey.Key, actor.PlayerSession);
    }

    // --- Mind Transfer: Borg → Brain ---

    private void OnReturnToCore(EntityUid uid, StationAiHeldComponent comp, BorisReturnToCoreMessage args)
    {
        // This message comes from the BUI, but the brain's mind is in the borg.
        // We need the borg entity to find the mind.
        if (!TryComp<BorisTransferComponent>(uid, out var brainTransfer) || brainTransfer.TargetBorg == null)
            return;

        DoReturnToCore(brainTransfer.TargetBorg.Value, uid);
    }

    private void OnReturnToCoreAction(EntityUid uid, BorisTransferComponent comp, BorisReturnToCoreActionEvent args)
    {
        if (args.Handled || comp.SourceBrain == null)
            return;

        args.Handled = true;
        DoReturnToCore(uid, comp.SourceBrain.Value);
    }

    private void DoReturnToCore(EntityUid borgUid, EntityUid brainUid)
    {
        // Get mind from the borg.
        if (!_mind.TryGetMind(borgUid, out var mindId, out var mindComp))
            return;

        // Transfer mind back to brain.
        _mind.TransferTo(mindId, brainUid, ghostCheckOverride: true, createGhost: false, mindComp);

        // Remove "Return to Core" action from borg (only the specific action, not all borg actions).
        if (TryComp<BorisTransferComponent>(borgUid, out var borgTransfer) && borgTransfer.ReturnActionEntity != null)
            _actions.RemoveAction(borgUid, borgTransfer.ReturnActionEntity);

        // Clean up transfer tracking.
        RemCompDeferred<BorisTransferComponent>(borgUid);

        if (TryComp<BorisTransferComponent>(brainUid, out var brainTransfer))
        {
            brainTransfer.TargetBorg = null;
            Dirty(brainUid, brainTransfer);
        }
    }

    // --- UI ---

    private void UpdateBorisControlUi(EntityUid brainUid)
    {
        // Find the server linked to this brain's core.
        if (!_container.TryGetContainingContainer(brainUid, out var brainContainer))
            return;

        var coreUid = brainContainer.Owner;
        if (!HasComp<StationAiCoreComponent>(coreUid))
            return;

        // Find server with Boris Control Module.
        var serverQuery = EntityQueryEnumerator<AiNetworkServerComponent>();
        while (serverQuery.MoveNext(out _, out var server))
        {
            if (server.LinkedCore != coreUid)
                continue;

            foreach (var moduleEnt in server.ModuleContainer.ContainedEntities)
            {
                if (!TryComp<BorisControlModuleComponent>(moduleEnt, out var borisControl))
                    continue;

                // Found the Boris Control Module — build UI state.
                var borgs = new List<BorisControlBorgEntry>();
                foreach (var borgUid in borisControl.PairedBorgs)
                {
                    if (Exists(borgUid))
                        borgs.Add(new BorisControlBorgEntry(GetNetEntity(borgUid), MetaData(borgUid).EntityName));
                }

                var isTransferred = TryComp<BorisTransferComponent>(brainUid, out var transfer) && transfer.TargetBorg != null;
                var currentBorg = isTransferred ? (NetEntity?) GetNetEntity(transfer!.TargetBorg!.Value) : null;

                var state = new BorisControlBuiState(borisControl.PairingCode ?? "????", borgs, isTransferred, currentBorg);
                _ui.SetUiState(brainUid, BorisControlUiKey.Key, state);
                return;
            }
        }

        // No Boris module found — set empty state so the UI still renders.
        var emptyState = new BorisControlBuiState("????", new List<BorisControlBorgEntry>());
        _ui.SetUiState(brainUid, BorisControlUiKey.Key, emptyState);
    }

    // --- Helpers ---

    private bool TryFindPairedBorisModule(EntityUid chassisUid, BorgChassisComponent chassis, out EntityUid pairedServer)
    {
        pairedServer = EntityUid.Invalid;

        var brain = chassis.BrainEntity;
        if (brain == null)
            return false;

        if (!TryComp<BorisModuleComponent>(brain.Value, out var boris) || boris.PairedServer == null)
            return false;

        pairedServer = boris.PairedServer.Value;
        return true;
    }
}
