using Content.Server.Power.Components;
using Content.Shared._Sandwich.Silicons.StationAi;
using Content.Shared._Sandwich.Silicons.StationAi.Components;
using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Radio.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.UserInterface;
using Content.Shared.Whitelist;
using Content.Shared.Wires;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._Sandwich.Silicons.StationAi;

public sealed class AiServerModuleSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly SoundPathSpecifier InsertSound = new("/Audio/Machines/terminal_insert_disc.ogg");
    private static readonly SoundPathSpecifier RemoveSound = new("/Audio/Machines/tray_eject.ogg");

    public override void Initialize()
    {
        base.Initialize();

        // Module insertion via interaction
        SubscribeLocalEvent<AiNetworkServerComponent, AfterInteractUsingEvent>(OnServerInteractUsing);

        // Container events for install/uninstall
        SubscribeLocalEvent<AiNetworkServerComponent, EntInsertedIntoContainerMessage>(OnModuleInserted);
        SubscribeLocalEvent<AiNetworkServerComponent, EntRemovedFromContainerMessage>(OnModuleRemoved);

        // UI
        SubscribeLocalEvent<AiNetworkServerComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<AiNetworkServerComponent, AiServerSetNameBuiMessage>(OnSetName);
        SubscribeLocalEvent<AiNetworkServerComponent, AiServerRemoveModuleBuiMessage>(OnRemoveModule);

        // Auth module UI messages
        SubscribeLocalEvent<AiNetworkServerComponent, AiServerRemoveAuthIdCardBuiMessage>(OnRemoveAuthIdCard);
        SubscribeLocalEvent<AiNetworkServerComponent, AiServerRemoveAuthKeyBuiMessage>(OnRemoveAuthKey);

        // AI brain action to open server UI remotely
        SubscribeLocalEvent<StationAiHeldComponent, ToggleAiServerPanelEvent>(OnToggleServerPanel);

        // AI brain forwarded BUI messages (from the brain's copy of the server UI)
        SubscribeLocalEvent<StationAiHeldComponent, AiServerSetNameBuiMessage>(OnRemoteSetName);
        SubscribeLocalEvent<StationAiHeldComponent, AiServerRemoveModuleBuiMessage>(OnRemoteRemoveModule);
        SubscribeLocalEvent<StationAiHeldComponent, AiServerRemoveAuthIdCardBuiMessage>(OnRemoteRemoveAuthIdCard);
        SubscribeLocalEvent<StationAiHeldComponent, AiServerRemoveAuthKeyBuiMessage>(OnRemoteRemoveAuthKey);
    }

    // --- UI ---

    private void OnBeforeUiOpen(EntityUid uid, AiNetworkServerComponent comp, BeforeActivatableUIOpenEvent args)
    {
        UpdateUI(uid, comp);
    }

    private void OnToggleServerPanel(EntityUid uid, StationAiHeldComponent comp, ToggleAiServerPanelEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(uid, out var actor))
            return;

        args.Handled = true;

        // Open BUI on the brain entity (not the server — avoids range/panel checks).
        _ui.TryToggleUi(uid, AiServerUiKey.Key, actor.PlayerSession);

        // Push server state to the brain's BUI.
        UpdateRemoteServerUi(uid);
    }

    /// <summary>
    /// Finds the linked server and pushes its state to the brain's AiServerUiKey BUI.
    /// </summary>
    private void UpdateRemoteServerUi(EntityUid brainUid)
    {
        if (!_container.TryGetContainingContainer(brainUid, out var brainContainer))
            return;

        var coreUid = brainContainer.Owner;
        if (!HasComp<StationAiCoreComponent>(coreUid))
            return;

        var query = EntityQueryEnumerator<AiNetworkServerComponent>();
        while (query.MoveNext(out var serverUid, out var server))
        {
            if (server.LinkedCore != coreUid)
                continue;

            // Build the same state as the server UI.
            var state = BuildServerUiState(serverUid, server);
            _ui.SetUiState(brainUid, AiServerUiKey.Key, state);
            return;
        }
    }

    private void OnSetName(EntityUid uid, AiNetworkServerComponent comp, AiServerSetNameBuiMessage args)
    {
        if (args.Name.Length > HumanoidCharacterProfile.MaxNameLength ||
            args.Name.Length == 0 ||
            string.IsNullOrWhiteSpace(args.Name))
        {
            return;
        }

        var name = args.Name.Trim();
        _metaData.SetEntityName(uid, name);
    }

    private void OnRemoveModule(EntityUid uid, AiNetworkServerComponent comp, AiServerRemoveModuleBuiMessage args)
    {
        var module = GetEntity(args.Module);

        if (!comp.ModuleContainer.Contains(module))
            return;

        _container.Remove(module, comp.ModuleContainer);
        _audio.PlayPvs(RemoveSound, uid);
        _popup.PopupEntity(Loc.GetString("ai-server-module-removed"), uid, args.Actor);

        UpdateUI(uid, comp);
    }

    private void OnRemoveAuthIdCard(EntityUid uid, AiNetworkServerComponent comp, AiServerRemoveAuthIdCardBuiMessage args)
    {
        if (!TryFindAuthModule(comp, out var authModuleUid))
            return;

        if (!_itemSlots.TryGetSlot(authModuleUid, AiAuthModuleComponent.IdCardSlotId, out var slot) ||
            slot.Item == null)
            return;

        if (!_itemSlots.TryEject(authModuleUid, AiAuthModuleComponent.IdCardSlotId, args.Actor, out var ejectedCard, excludeUserAudio: true))
            return;

        _transform.DropNextTo(ejectedCard.Value, uid);
        _audio.PlayPvs(RemoveSound, uid);
        UpdateUI(uid, comp);
    }

    private void OnRemoveAuthKey(EntityUid uid, AiNetworkServerComponent comp, AiServerRemoveAuthKeyBuiMessage args)
    {
        var keyUid = GetEntity(args.Key);

        if (!TryFindAuthModule(comp, out var authModuleUid))
            return;

        if (!_container.TryGetContainer(authModuleUid, EncryptionKeyHolderComponent.KeyContainerName, out var keyContainer))
            return;

        if (!keyContainer.Contains(keyUid))
            return;

        _container.Remove(keyUid, keyContainer);
        _transform.DropNextTo(keyUid, uid);
        _audio.PlayPvs(RemoveSound, uid);
        UpdateUI(uid, comp);
    }

    public void UpdateUI(EntityUid uid, AiNetworkServerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        var state = BuildServerUiState(uid, comp);
        _ui.SetUiState(uid, AiServerUiKey.Key, state);

        // Also update any AI brain viewing this server remotely.
        if (comp.LinkedCore != null &&
            _container.TryGetContainer(comp.LinkedCore.Value, StationAiCoreComponent.Container, out var coreContainer) &&
            coreContainer.ContainedEntities.Count > 0)
        {
            var brainUid = coreContainer.ContainedEntities[0];
            _ui.SetUiState(brainUid, AiServerUiKey.Key, state);
        }
    }

    private AiServerBuiState BuildServerUiState(EntityUid serverUid, AiNetworkServerComponent comp)
    {
        var hasCore = comp.LinkedCore != null && Exists(comp.LinkedCore.Value);
        var coreNet = hasCore ? GetNetEntity(comp.LinkedCore!.Value) : (NetEntity?) null;

        // Auth module data
        var hasAuth = false;
        NetEntity? authIdCard = null;
        var authKeys = new List<NetEntity>();

        if (TryFindAuthModule(comp, out var authModuleUid))
        {
            hasAuth = true;

            if (_itemSlots.TryGetSlot(authModuleUid, AiAuthModuleComponent.IdCardSlotId, out var slot) &&
                slot.Item != null)
            {
                authIdCard = GetNetEntity(slot.Item.Value);
            }

            if (TryComp<EncryptionKeyHolderComponent>(authModuleUid, out var keyHolder) &&
                _container.TryGetContainer(authModuleUid, EncryptionKeyHolderComponent.KeyContainerName, out var keyContainer))
            {
                foreach (var key in keyContainer.ContainedEntities)
                {
                    authKeys.Add(GetNetEntity(key));
                }
            }
        }

        return new AiServerBuiState(GetNetEntity(serverUid), hasCore, coreNet, hasAuth, authIdCard, authKeys);
    }

    // --- Remote BUI message forwarding (AI brain → linked server) ---

    private void OnRemoteSetName(EntityUid uid, StationAiHeldComponent comp, AiServerSetNameBuiMessage args)
    {
        if (!TryGetLinkedServer(uid, out var serverUid, out var server))
            return;

        OnSetName(serverUid, server, args);
        UpdateRemoteServerUi(uid);
    }

    private void OnRemoteRemoveModule(EntityUid uid, StationAiHeldComponent comp, AiServerRemoveModuleBuiMessage args)
    {
        if (!TryGetLinkedServer(uid, out var serverUid, out var server))
            return;

        OnRemoveModule(serverUid, server, args);
        UpdateRemoteServerUi(uid);
    }

    private void OnRemoteRemoveAuthIdCard(EntityUid uid, StationAiHeldComponent comp, AiServerRemoveAuthIdCardBuiMessage args)
    {
        if (!TryGetLinkedServer(uid, out var serverUid, out var server))
            return;

        OnRemoveAuthIdCard(serverUid, server, args);
        UpdateRemoteServerUi(uid);
    }

    private void OnRemoteRemoveAuthKey(EntityUid uid, StationAiHeldComponent comp, AiServerRemoveAuthKeyBuiMessage args)
    {
        if (!TryGetLinkedServer(uid, out var serverUid, out var server))
            return;

        OnRemoveAuthKey(serverUid, server, args);
        UpdateRemoteServerUi(uid);
    }

    /// <summary>
    /// Finds the server linked to the AI core containing this brain.
    /// </summary>
    private bool TryGetLinkedServer(EntityUid brainUid, out EntityUid serverUid, out AiNetworkServerComponent server)
    {
        serverUid = EntityUid.Invalid;
        server = default!;

        if (!_container.TryGetContainingContainer(brainUid, out var brainContainer))
            return false;

        var coreUid = brainContainer.Owner;
        if (!HasComp<StationAiCoreComponent>(coreUid))
            return false;

        var query = EntityQueryEnumerator<AiNetworkServerComponent>();
        while (query.MoveNext(out var sUid, out var s))
        {
            if (s.LinkedCore != coreUid)
                continue;

            serverUid = sUid;
            server = s;
            return true;
        }

        return false;
    }

    private bool TryFindAuthModule(AiNetworkServerComponent server, out EntityUid authModuleUid)
    {
        authModuleUid = EntityUid.Invalid;

        foreach (var moduleEnt in server.ModuleContainer.ContainedEntities)
        {
            if (HasComp<AiAuthModuleComponent>(moduleEnt))
            {
                authModuleUid = moduleEnt;
                return true;
            }
        }

        return false;
    }

    // --- Module Insertion ---

    private void OnServerInteractUsing(EntityUid uid, AiNetworkServerComponent comp, AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Handled)
            return;

        // Try to insert into Auth module first (ID card or encryption key)
        if (TryInsertIntoAuthModule(uid, args.Used, comp, args.User))
        {
            args.Handled = true;
            return;
        }

        if (!TryComp<AiServerModuleComponent>(args.Used, out var module))
            return;

        // Panel must be open (screwdriver)
        if (TryComp<WiresPanelComponent>(uid, out var panel) && !panel.Open)
        {
            _popup.PopupEntity(Loc.GetString("ai-server-panel-closed"), uid, args.User);
            args.Handled = true;
            return;
        }

        if (!CanInsertModule(uid, args.Used, comp, module, args.User))
        {
            args.Handled = true;
            return;
        }

        _container.Insert(args.Used, comp.ModuleContainer);
        _audio.PlayPvs(InsertSound, uid);
        _popup.PopupEntity(Loc.GetString("ai-server-module-installed"), uid, args.User);
        args.Handled = true;

        UpdateUI(uid, comp);
    }

    private bool CanInsertModule(
        EntityUid serverUid,
        EntityUid moduleUid,
        AiNetworkServerComponent server,
        AiServerModuleComponent module,
        EntityUid user)
    {
        // Check max modules
        if (server.ModuleCount >= server.MaxModules)
        {
            _popup.PopupEntity(Loc.GetString("ai-server-module-full"), serverUid, user);
            return false;
        }

        // Check whitelist
        if (!_whitelist.IsWhitelistPassOrNull(server.ModuleWhitelist, moduleUid))
        {
            _popup.PopupEntity(Loc.GetString("ai-server-module-incompatible"), serverUid, user);
            return false;
        }

        // Check for duplicates by ModuleId
        foreach (var existing in server.ModuleContainer.ContainedEntities)
        {
            if (TryComp<AiServerModuleComponent>(existing, out var existingModule) &&
                existingModule.ModuleId == module.ModuleId)
            {
                _popup.PopupEntity(Loc.GetString("ai-server-module-duplicate"), serverUid, user);
                return false;
            }
        }

        // Check server power
        if (TryComp<ApcPowerReceiverComponent>(serverUid, out var power) && !power.Powered)
        {
            _popup.PopupEntity(Loc.GetString("ai-server-no-power"), serverUid, user);
            return false;
        }

        return true;
    }

    // --- Auth Module Insertion ---

    private bool TryInsertIntoAuthModule(EntityUid serverUid, EntityUid itemUid, AiNetworkServerComponent comp, EntityUid user)
    {
        if (!TryFindAuthModule(comp, out var authModuleUid))
            return false;

        // Try inserting as ID card
        if (HasComp<IdCardComponent>(itemUid))
        {
            if (_itemSlots.TryGetSlot(authModuleUid, AiAuthModuleComponent.IdCardSlotId, out var slot))
            {
                if (slot.Item != null)
                {
                    _popup.PopupEntity(Loc.GetString("ai-auth-id-slot-full"), serverUid, user);
                    return true;
                }

                if (_itemSlots.TryInsert(authModuleUid, AiAuthModuleComponent.IdCardSlotId, itemUid, user, excludeUserAudio: true))
                {
                    _audio.PlayPvs(InsertSound, serverUid);
                    UpdateUI(serverUid, comp);
                    return true;
                }
            }
            return true;
        }

        // Try inserting as encryption key
        if (HasComp<EncryptionKeyComponent>(itemUid))
        {
            if (!TryComp<EncryptionKeyHolderComponent>(authModuleUid, out var keyHolder))
                return false;

            if (!_container.TryGetContainer(authModuleUid, EncryptionKeyHolderComponent.KeyContainerName, out var keyContainer))
                return false;

            if (keyContainer.ContainedEntities.Count >= keyHolder.KeySlots)
            {
                _popup.PopupEntity(Loc.GetString("ai-auth-key-slots-full"), serverUid, user);
                return true;
            }

            if (_container.Insert(itemUid, keyContainer))
            {
                _audio.PlayPvs(InsertSound, serverUid);
                UpdateUI(serverUid, comp);
                return true;
            }
            return true;
        }

        return false;
    }

    // --- Container Events ---

    private void OnModuleInserted(EntityUid uid, AiNetworkServerComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != comp.ModuleContainerId)
            return;

        if (!TryComp<AiServerModuleComponent>(args.Entity, out var module))
            return;

        module.InstalledServer = uid;
        var ev = new AiModuleInstalledEvent(uid);
        RaiseLocalEvent(args.Entity, ref ev);
    }

    private void OnModuleRemoved(EntityUid uid, AiNetworkServerComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != comp.ModuleContainerId)
            return;

        if (!TryComp<AiServerModuleComponent>(args.Entity, out var module))
            return;

        var ev = new AiModuleUninstalledEvent(uid);
        RaiseLocalEvent(args.Entity, ref ev);
        module.InstalledServer = null;
    }
}
