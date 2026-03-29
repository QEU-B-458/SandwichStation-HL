using Content.Server.Mind;
// using Content.Shared._NF.Bank.Components; // _NF not yet ported to ss14-rebase
using Content.Shared._Sandwich.Silicons.StationAi;
using Content.Shared._Sandwich.Silicons.StationAi.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Sandwich.Silicons.StationAi;

/// <summary>
/// Handles the AI Deployment Borg system:
/// - Auto-links AI Interface to AI Controller Server on same grid
/// - Upload: deployment borg in AI Interface → mind transfers to AI core
/// - Shunt: AI in core → mind transfers back to deployment borg
/// </summary>
public sealed class AiDeploymentSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        // AI Interface auto-links to server on same grid.
        SubscribeLocalEvent<AiInterfaceComponent, ComponentStartup>(OnInterfaceStartup);

        // Deployment borg enters/leaves the AI Interface (EntityStorage).
        SubscribeLocalEvent<AiDeploymentBorgComponent, EntGotInsertedIntoContainerMessage>(OnBorgInserted);
        SubscribeLocalEvent<AiDeploymentBorgComponent, EntGotRemovedFromContainerMessage>(OnBorgRemoved);

        // Upload action — borg presses "Upload to Core".
        SubscribeLocalEvent<AiDeploymentBorgComponent, AiDeploymentUploadActionEvent>(OnUploadAction);

        // Shunt action — AI presses "Return to Deployment Borg".
        SubscribeLocalEvent<StationAiHeldComponent, AiDeploymentShuntActionEvent>(OnShuntAction);

        // When the deployment borg spawns as a job, set up the ID card and entity name from the player's character.
        SubscribeLocalEvent<AiDeploymentBorgComponent, PlayerSpawnCompleteEvent>(OnBorgSpawned);

        // Toggle ID card between internal slot and hand.
        SubscribeLocalEvent<AiDeploymentBorgComponent, AiDeploymentToggleIdCardActionEvent>(OnToggleIdCard);
    }

    // --- Spawn Setup ---

    /// <summary>
    /// When the deployment borg spawns as a job, program the ID card with the player's
    /// character name and set the borg's entity name to match. Also add a bank account.
    /// </summary>
    private void OnBorgSpawned(EntityUid uid, AiDeploymentBorgComponent comp, PlayerSpawnCompleteEvent args)
    {
        var characterName = args.Profile.Name;

        // Set the borg's entity name to the player's character name.
        _metadata.SetEntityName(uid, characterName);

        // Program the ID card in the id_card_slot with the character's name and job title.
        if (_itemSlots.TryGetSlot(uid, "id_card_slot", out var idSlot) && idSlot.Item is { } cardUid)
        {
            _idCard.TryChangeFullName(cardUid, characterName);
            _idCard.TryChangeJobTitle(cardUid, Loc.GetString("job-name-ai-deployment-borg"));
        }

        // Bank account sync disabled — _NF not yet ported to ss14-rebase.
        // EnsureComp<BankAccountComponent>(uid);

        // Grant the "Toggle ID Card" action.
        _actions.AddAction(uid, ref comp.IdCardActionEntity, "ActionAiDeploymentToggleIdCard");
        Dirty(uid, comp);
    }

    // --- Toggle ID Card ---

    /// <summary>
    /// Toggles the ID card between the internal ItemSlot and the borg's hand.
    /// </summary>
    private void OnToggleIdCard(EntityUid uid, AiDeploymentBorgComponent comp, AiDeploymentToggleIdCardActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!_itemSlots.TryGetSlot(uid, comp.IdCardSlotId, out var idSlot))
            return;

        if (idSlot.Item is { } cardInSlot)
        {
            // Card is in the slot — eject to hand.
            if (!_itemSlots.TryEject(uid, idSlot, uid, out var ejected, excludeUserAudio: true))
                return;

            if (!_hands.TryPickupAnyHand(uid, ejected.Value, checkActionBlocker: false))
            {
                // Hand full — put it back.
                _itemSlots.TryInsert(uid, idSlot, ejected.Value, uid, excludeUserAudio: true);
                _popup.PopupEntity(Loc.GetString("ai-deployment-hands-full"), uid, uid);
                return;
            }

            _audio.PlayPvs(comp.EjectSound, uid);
            _popup.PopupEntity(Loc.GetString("ai-deployment-id-ejected"), uid, uid);
        }
        else
        {
            // Card is not in the slot — try to store from hand.
            if (!_hands.TryGetActiveItem(uid, out var heldItem))
            {
                _popup.PopupEntity(Loc.GetString("ai-deployment-no-id-in-hand"), uid, uid);
                return;
            }

            if (!_itemSlots.TryInsert(uid, idSlot, heldItem.Value, uid, excludeUserAudio: true))
            {
                _popup.PopupEntity(Loc.GetString("ai-deployment-not-id-card"), uid, uid);
                return;
            }

            _audio.PlayPvs(comp.InsertSound, uid);
            _popup.PopupEntity(Loc.GetString("ai-deployment-id-stored"), uid, uid);
        }
    }

    // --- Auto-Link ---

    private void OnInterfaceStartup(EntityUid uid, AiInterfaceComponent comp, ComponentStartup args)
    {
        AutoLinkInterfaceToServer(uid, comp);
    }

    private void AutoLinkInterfaceToServer(EntityUid uid, AiInterfaceComponent comp)
    {
        if (comp.LinkedServer != null)
            return;

        var interfaceGrid = _transform.GetGrid(uid);
        if (interfaceGrid == null)
            return;

        var query = EntityQueryEnumerator<AiNetworkServerComponent>();
        while (query.MoveNext(out var serverUid, out _))
        {
            if (_transform.GetGrid(serverUid) == interfaceGrid)
            {
                comp.LinkedServer = serverUid;
                Dirty(uid, comp);
                break;
            }
        }
    }

    // --- Borg Enters/Leaves AI Interface ---

    private void OnBorgInserted(EntityUid uid, AiDeploymentBorgComponent comp, EntGotInsertedIntoContainerMessage args)
    {
        // Only care about insertion into an AI Interface's entity storage.
        if (args.Container.ID != "entity_storage")
            return;

        if (!HasComp<AiInterfaceComponent>(args.Container.Owner))
            return;

        // Grant "Upload to Core" action.
        EntityUid? uploadAction = null;
        _actions.AddAction(uid, ref uploadAction, "ActionAiDeploymentUpload");
        comp.UploadActionEntity = uploadAction;
        Dirty(uid, comp);
    }

    private void OnBorgRemoved(EntityUid uid, AiDeploymentBorgComponent comp, EntGotRemovedFromContainerMessage args)
    {
        if (args.Container.ID != "entity_storage")
            return;

        if (!HasComp<AiInterfaceComponent>(args.Container.Owner))
            return;

        // Remove "Upload to Core" action.
        if (comp.UploadActionEntity != null)
        {
            _actions.RemoveAction(uid, comp.UploadActionEntity);
            comp.UploadActionEntity = null;
            Dirty(uid, comp);
        }
    }

    // --- Upload: Borg → Core ---

    private void OnUploadAction(EntityUid uid, AiDeploymentBorgComponent comp, AiDeploymentUploadActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // Find which AI Interface we're inside.
        if (!_container.TryGetContainingContainer(uid, out var storageContainer))
        {
            _popup.PopupEntity(Loc.GetString("ai-deployment-not-in-interface"), uid, uid);
            return;
        }

        var interfaceUid = storageContainer.Owner;
        if (!TryComp<AiInterfaceComponent>(interfaceUid, out var aiInterface))
        {
            _popup.PopupEntity(Loc.GetString("ai-deployment-not-in-interface"), uid, uid);
            return;
        }

        // Check interface is linked to a server.
        if (aiInterface.LinkedServer == null || !TryComp<AiNetworkServerComponent>(aiInterface.LinkedServer.Value, out var server))
        {
            _popup.PopupEntity(Loc.GetString("ai-deployment-no-server"), uid, uid);
            return;
        }

        // Check server is linked to a core.
        if (server.LinkedCore == null || !TryComp<StationAiCoreComponent>(server.LinkedCore.Value, out _))
        {
            _popup.PopupEntity(Loc.GetString("ai-deployment-no-core"), uid, uid);
            return;
        }

        var coreUid = server.LinkedCore.Value;

        // Check core's mind slot is empty.
        if (!_container.TryGetContainer(coreUid, StationAiCoreComponent.Container, out var mindSlot))
        {
            _popup.PopupEntity(Loc.GetString("ai-deployment-core-error"), uid, uid);
            return;
        }

        if (mindSlot.ContainedEntities.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("ai-deployment-core-occupied"), uid, uid);
            return;
        }

        // Get the borg's mind.
        if (!_mind.TryGetMind(uid, out var mindId, out var mindComp))
        {
            _popup.PopupEntity(Loc.GetString("ai-deployment-no-mind"), uid, uid);
            return;
        }

        // Spawn a StationAiBrain and insert it into the core's mind slot.
        var brainUid = Spawn("StationAiBrain", Transform(coreUid).Coordinates);
        if (!_container.Insert(brainUid, mindSlot))
        {
            _popup.PopupEntity(Loc.GetString("ai-deployment-core-error"), uid, uid);
            QueueDel(brainUid);
            return;
        }

        // Transfer mind from borg to the new brain.
        _mind.TransferTo(mindId, brainUid, ghostCheckOverride: true, createGhost: false, mindComp);

        // Track the transfer.
        comp.SourceInterface = interfaceUid;
        Dirty(uid, comp);

        aiInterface.StoredBorg = uid;
        Dirty(interfaceUid, aiInterface);

        // Grant "Return to Deployment Borg" action on the brain.
        var deployComp = EnsureComp<AiDeploymentTrackingComponent>(brainUid);
        deployComp.SourceBorg = uid;
        deployComp.SourceInterface = interfaceUid;

        EntityUid? shuntAction = null;
        _actions.AddAction(brainUid, ref shuntAction, "ActionAiDeploymentShunt");
        deployComp.ShuntActionEntity = shuntAction;
        Dirty(brainUid, deployComp);

        // Remove upload action from borg.
        if (comp.UploadActionEntity != null)
        {
            _actions.RemoveAction(uid, comp.UploadActionEntity);
            comp.UploadActionEntity = null;
        }

        _popup.PopupEntity(Loc.GetString("ai-deployment-upload-success"), coreUid);
    }

    // --- Shunt: Core → Borg ---

    private void OnShuntAction(EntityUid uid, StationAiHeldComponent comp, AiDeploymentShuntActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryComp<AiDeploymentTrackingComponent>(uid, out var tracking))
        {
            _popup.PopupEntity(Loc.GetString("ai-deployment-no-borg"), uid, uid);
            return;
        }

        var borgUid = tracking.SourceBorg;
        if (borgUid == null || !Exists(borgUid.Value) || !TryComp<AiDeploymentBorgComponent>(borgUid.Value, out _))
        {
            _popup.PopupEntity(Loc.GetString("ai-deployment-borg-destroyed"), uid, uid);
            return;
        }

        // Get mind from the brain.
        if (!_mind.TryGetMind(uid, out var mindId, out var mindComp))
            return;

        // Transfer mind back to borg.
        _mind.TransferTo(mindId, borgUid.Value, ghostCheckOverride: true, createGhost: false, mindComp);

        // Clean up: remove the spawned brain from the core.
        if (tracking.ShuntActionEntity != null)
            _actions.RemoveAction(uid, tracking.ShuntActionEntity);

        // Remove brain from core container and delete it.
        _container.TryRemoveFromContainer(uid);
        QueueDel(uid);

        // Clear interface tracking.
        if (tracking.SourceInterface != null && TryComp<AiInterfaceComponent>(tracking.SourceInterface.Value, out var aiInterface))
        {
            aiInterface.StoredBorg = null;
            Dirty(tracking.SourceInterface.Value, aiInterface);
        }

        // Clear borg tracking.
        if (TryComp<AiDeploymentBorgComponent>(borgUid.Value, out var borgComp))
        {
            borgComp.SourceInterface = null;
            Dirty(borgUid.Value, borgComp);
        }

        _popup.PopupEntity(Loc.GetString("ai-deployment-shunt-success"), borgUid.Value);
    }
}
