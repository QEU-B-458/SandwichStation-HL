using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Sandwich.Silicons.StationAi;
using Content.Shared._Sandwich.Silicons.StationAi.Components;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Silicons.StationAi;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Sandwich.Silicons.StationAi;

public sealed class AiNetworkSystem : SharedAiNetworkSystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAi = default!;

    // Tracks which server a multitool user has "saved" for linking
    private readonly Dictionary<EntityUid, EntityUid> _pendingLinks = new();

    public override void Initialize()
    {
        base.Initialize();

        // Server lifecycle (ComponentStartup handled in shared base class)
        SubscribeLocalEvent<AiNetworkServerComponent, ComponentShutdown>(OnServerShutdown);
        SubscribeLocalEvent<AiNetworkServerComponent, PowerChangedEvent>(OnServerPowerChanged);
        SubscribeLocalEvent<AiNetworkServerComponent, MoveEvent>(OnServerMoved);

        // Relay lifecycle
        SubscribeLocalEvent<AiRelayComponent, ComponentStartup>(OnRelayStartup);
        SubscribeLocalEvent<AiRelayComponent, ComponentShutdown>(OnRelayShutdown);
        SubscribeLocalEvent<AiRelayComponent, PowerChangedEvent>(OnRelayPowerChanged);
        SubscribeLocalEvent<AiRelayComponent, MoveEvent>(OnRelayMoved);

        // Core auto-links to server on same grid
        SubscribeLocalEvent<StationAiCoreComponent, ComponentStartup>(OnCoreStartup);

        // Multitool linking
        SubscribeLocalEvent<AiNetworkServerComponent, InteractUsingEvent>(OnServerInteractUsing);
        SubscribeLocalEvent<AiRelayComponent, InteractUsingEvent>(OnRelayInteractUsing);
        SubscribeLocalEvent<StationAiCoreComponent, InteractUsingEvent>(OnCoreInteractUsing);

        // Network UI
        SubscribeLocalEvent<StationAiHeldComponent, ToggleAiNetworkScreenEvent>(OnToggleNetworkScreen);
        SubscribeLocalEvent<StationAiHeldComponent, AiNetworkJumpMessage>(OnNetworkJump);
    }

    // --- Server Events ---

    protected override void OnServerInit(EntityUid uid, AiNetworkServerComponent comp)
    {
        AutoLinkSameGrid(uid, comp);
        RecomputeAuthorizedGrids(uid, comp);
    }

    private void OnServerShutdown(EntityUid uid, AiNetworkServerComponent comp, ComponentShutdown args)
    {
        foreach (var relayUid in comp.ConnectedRelays)
        {
            if (TryComp<AiRelayComponent>(relayUid, out var relay))
                relay.LinkedServer = null;
        }

        comp.ConnectedRelays.Clear();
        comp.AuthorizedGrids.Clear();

        comp.LinkedCore = null;
    }

    private void OnServerPowerChanged(EntityUid uid, AiNetworkServerComponent comp, ref PowerChangedEvent args)
    {
        RecomputeAuthorizedGrids(uid, comp);
    }

    private void OnServerMoved(EntityUid uid, AiNetworkServerComponent comp, ref MoveEvent args)
    {
        RecomputeAuthorizedGrids(uid, comp);
    }

    // --- Relay Events ---

    private void OnRelayStartup(EntityUid uid, AiRelayComponent comp, ComponentStartup args)
    {
        if (comp.LinkedServer != null)
            return;

        var relayGrid = _transform.GetGrid(uid);
        if (relayGrid == null)
            return;

        var query = EntityQueryEnumerator<AiNetworkServerComponent>();
        while (query.MoveNext(out var serverUid, out var server))
        {
            if (_transform.GetGrid(serverUid) == relayGrid)
            {
                LinkRelayToServer(uid, serverUid);
                break;
            }
        }
    }

    private void OnRelayShutdown(EntityUid uid, AiRelayComponent comp, ComponentShutdown args)
    {
        if (comp.LinkedServer != null && TryComp<AiNetworkServerComponent>(comp.LinkedServer, out var server))
        {
            server.ConnectedRelays.Remove(uid);
            RecomputeAuthorizedGrids(comp.LinkedServer.Value, server);
        }
    }

    private void OnRelayPowerChanged(EntityUid uid, AiRelayComponent comp, ref PowerChangedEvent args)
    {
        comp.Active = args.Powered;

        if (comp.LinkedServer != null && TryComp<AiNetworkServerComponent>(comp.LinkedServer, out var server))
            RecomputeAuthorizedGrids(comp.LinkedServer.Value, server);
    }

    private void OnRelayMoved(EntityUid uid, AiRelayComponent comp, ref MoveEvent args)
    {
        if (comp.LinkedServer != null && TryComp<AiNetworkServerComponent>(comp.LinkedServer, out var server))
            RecomputeAuthorizedGrids(comp.LinkedServer.Value, server);
    }

    // --- Core Events ---

    private void OnCoreStartup(EntityUid uid, StationAiCoreComponent comp, ComponentStartup args)
    {
        // Auto-link to a server on the same grid
        var coreGrid = _transform.GetGrid(uid);
        if (coreGrid == null)
            return;

        var query = EntityQueryEnumerator<AiNetworkServerComponent>();
        while (query.MoveNext(out var serverUid, out var server))
        {
            if (server.LinkedCore != null)
                continue;

            if (_transform.GetGrid(serverUid) == coreGrid)
            {
                LinkCoreToServer(uid, serverUid);
                break;
            }
        }
    }

    // --- Auto-Linking ---

    private void AutoLinkSameGrid(EntityUid serverUid, AiNetworkServerComponent comp)
    {
        var serverGrid = _transform.GetGrid(serverUid);
        if (serverGrid == null)
            return;

        if (comp.LinkedCore == null)
        {
            var coreQuery = EntityQueryEnumerator<StationAiCoreComponent>();
            while (coreQuery.MoveNext(out var coreUid, out _))
            {
                if (_transform.GetGrid(coreUid) == serverGrid)
                {
                    LinkCoreToServer(coreUid, serverUid);
                    break;
                }
            }
        }

        var relayQuery = EntityQueryEnumerator<AiRelayComponent>();
        while (relayQuery.MoveNext(out var relayUid, out var relay))
        {
            if (relay.LinkedServer != null)
                continue;

            if (_transform.GetGrid(relayUid) == serverGrid)
            {
                relay.LinkedServer = serverUid;
                comp.ConnectedRelays.Add(relayUid);
                Dirty(relayUid, relay);
            }
        }

        Dirty(serverUid, comp);
    }

    // --- Linking ---

    public void LinkCoreToServer(EntityUid coreUid, EntityUid serverUid)
    {
        if (!TryComp<AiNetworkServerComponent>(serverUid, out var server))
            return;

        server.LinkedCore = coreUid;
        Dirty(serverUid, server);
        RecomputeAuthorizedGrids(serverUid, server);

        var ev = new AiServerCoreLinkChangedEvent(serverUid);
        RaiseLocalEvent(serverUid, ref ev);
    }

    public void LinkRelayToServer(EntityUid relayUid, EntityUid serverUid)
    {
        if (!TryComp<AiRelayComponent>(relayUid, out var relay) ||
            !TryComp<AiNetworkServerComponent>(serverUid, out var server))
            return;

        if (relay.LinkedServer != null && relay.LinkedServer != serverUid &&
            TryComp<AiNetworkServerComponent>(relay.LinkedServer, out var oldServer))
        {
            oldServer.ConnectedRelays.Remove(relayUid);
            RecomputeAuthorizedGrids(relay.LinkedServer.Value, oldServer);
        }

        relay.LinkedServer = serverUid;
        server.ConnectedRelays.Add(relayUid);
        Dirty(relayUid, relay);
        Dirty(serverUid, server);
        RecomputeAuthorizedGrids(serverUid, server);
    }

    public void RecomputeAuthorizedGrids(EntityUid serverUid, AiNetworkServerComponent comp)
    {
        comp.AuthorizedGrids.Clear();

        var serverGrid = _transform.GetGrid(serverUid);
        if (serverGrid != null)
            comp.AuthorizedGrids.Add(GetNetEntity(serverGrid.Value));

        // Include core's grid too
        if (comp.LinkedCore != null)
        {
            var coreGrid = _transform.GetGrid(comp.LinkedCore.Value);
            if (coreGrid != null)
                comp.AuthorizedGrids.Add(GetNetEntity(coreGrid.Value));
        }

        // Unpowered = own grid only
        if (TryComp<ApcPowerReceiverComponent>(serverUid, out var power) && !power.Powered)
        {
            Dirty(serverUid, comp);
            return;
        }

        foreach (var relayUid in comp.ConnectedRelays)
        {
            if (!TryComp<AiRelayComponent>(relayUid, out var relay) || !relay.Active)
                continue;

            var relayGrid = _transform.GetGrid(relayUid);
            if (relayGrid != null)
                comp.AuthorizedGrids.Add(GetNetEntity(relayGrid.Value));
        }

        Dirty(serverUid, comp);
    }

    // --- Multitool Interaction ---

    private void OnServerInteractUsing(EntityUid uid, AiNetworkServerComponent comp, InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<NetworkConfiguratorComponent>(args.Used))
            return;

        _pendingLinks[args.User] = uid;
        _popup.PopupEntity("AI Controller Server saved to multitool.", uid, args.User);
        args.Handled = true;
    }

    private void OnRelayInteractUsing(EntityUid uid, AiRelayComponent comp, InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<NetworkConfiguratorComponent>(args.Used))
            return;

        if (!_pendingLinks.TryGetValue(args.User, out var serverUid) ||
            !TryComp<AiNetworkServerComponent>(serverUid, out _))
        {
            _popup.PopupEntity("Use the multitool on an AI Controller Server first.", uid, args.User);
            args.Handled = true;
            return;
        }

        LinkRelayToServer(uid, serverUid);
        _popup.PopupEntity("AI Relay linked to server.", uid, args.User);
        args.Handled = true;
    }

    private void OnCoreInteractUsing(EntityUid uid, StationAiCoreComponent comp, InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<NetworkConfiguratorComponent>(args.Used))
            return;

        if (!_pendingLinks.TryGetValue(args.User, out var serverUid) ||
            !TryComp<AiNetworkServerComponent>(serverUid, out _))
        {
            _popup.PopupEntity("Use the multitool on an AI Controller Server first.", uid, args.User);
            args.Handled = true;
            return;
        }

        LinkCoreToServer(uid, serverUid);
        _popup.PopupEntity("AI Core linked to server.", uid, args.User);
        args.Handled = true;
    }

    // --- AI Network UI ---

    private void OnToggleNetworkScreen(EntityUid uid, StationAiHeldComponent comp, ToggleAiNetworkScreenEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(uid, out var actor))
            return;

        args.Handled = true;

        _ui.TryToggleUi(uid, AiNetworkUiKey.Key, actor.PlayerSession);

        if (!_stationAi.TryGetCore(uid, out var core))
            return;

        var coreUid = core.Owner;
        var entries = new List<AiNetworkEntry>();
        var serverQuery = EntityQueryEnumerator<AiNetworkServerComponent>();
        while (serverQuery.MoveNext(out var serverUid, out var server))
        {
            if (server.LinkedCore != coreUid)
                continue;

            var serverName = MetaData(serverUid).EntityName;
            var serverPowered = !TryComp<ApcPowerReceiverComponent>(serverUid, out var serverPower) || serverPower.Powered;
            entries.Add(new AiNetworkEntry(GetNetEntity(serverUid), serverName, serverPowered));

            foreach (var relayUid in server.ConnectedRelays)
            {
                if (!TryComp<AiRelayComponent>(relayUid, out var relay))
                    continue;

                var relayName = MetaData(relayUid).EntityName;
                entries.Add(new AiNetworkEntry(GetNetEntity(relayUid), relayName, relay.Active));
            }

            break;
        }

        var state = new AiNetworkBuiState(entries);
        _ui.SetUiState(uid, AiNetworkUiKey.Key, state);
    }

    private void OnNetworkJump(EntityUid uid, StationAiHeldComponent comp, AiNetworkJumpMessage args)
    {
        var target = GetEntity(args.Target);

        if (!HasComp<AiNetworkServerComponent>(target) && !HasComp<AiRelayComponent>(target))
            return;

        if (!_stationAi.TryGetCore(uid, out var core) || core.Comp?.RemoteEntity == null)
            return;

        var eye = core.Comp.RemoteEntity.Value;
        _transform.DropNextTo(eye, target);
    }
}
