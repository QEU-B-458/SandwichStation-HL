using Content.Shared._axiom.Silicons.StationAi.Components;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._axiom.Silicons.StationAi;

/// <summary>
/// Handles radio channel sync for the AI Auth module.
/// When encryption keys change in the Auth module, syncs channels to the AI brain
/// AND to any paired Boris borgs.
/// Access handling is in <see cref="SharedAiAuthAccessSystem"/> (shared code, needed for client prediction).
/// </summary>
public sealed class AiAuthRadioSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Radio: when encryption keys change on an Auth module, sync to AI brain.
        SubscribeLocalEvent<AiAuthModuleComponent, EncryptionChannelsChangedEvent>(OnAuthKeysChanged);

        // Radio: when Auth module is installed/removed from server, sync radio.
        SubscribeLocalEvent<AiAuthModuleComponent, AiModuleInstalledEvent>(OnAuthModuleInstalled);
        SubscribeLocalEvent<AiAuthModuleComponent, AiModuleUninstalledEvent>(OnAuthModuleUninstalled);

        // Radio: when a brain is inserted into a core, sync radio from any existing Auth module.
        // We subscribe on the brain (StationAiHeldComponent) not the core, because
        // SharedStationAiSystem already subscribes (StationAiCoreComponent, EntInsertedIntoContainerMessage).
        SubscribeLocalEvent<StationAiHeldComponent, EntInsertedIntoContainerMessage>(OnBrainInserted);

        // Radio: when a server gets linked to a core, sync radio from any existing Auth module.
        SubscribeLocalEvent<AiNetworkServerComponent, AiServerCoreLinkChangedEvent>(OnServerCoreLinkChanged);

        // Radio: fallback sync after all ContainerFill operations complete on server MapInit.
        // This catches cases where auto-link + ContainerFill ordering causes the initial sync to miss.
        SubscribeLocalEvent<AiNetworkServerComponent, MapInitEvent>(OnServerMapInit);

        // Boris borg radio: sync when a borg pairs, reset when it unpairs.
        SubscribeLocalEvent<BorisRadioSyncNeededEvent>(OnBorisRadioSyncNeeded);
        SubscribeLocalEvent<BorisRadioResetNeededEvent>(OnBorisRadioResetNeeded);
    }

    private void OnServerMapInit(EntityUid uid, AiNetworkServerComponent comp, MapInitEvent args)
    {
        if (comp.LinkedCore == null)
            return;

        if (!TryFindAuthModule(comp, out var authModuleUid))
            return;

        SyncRadioFromAuthModule(authModuleUid);
    }

    private void OnAuthKeysChanged(EntityUid uid, AiAuthModuleComponent comp, EncryptionChannelsChangedEvent args)
    {
        SyncRadioFromAuthModule(uid);
    }

    private void OnAuthModuleInstalled(EntityUid uid, AiAuthModuleComponent comp, ref AiModuleInstalledEvent args)
    {
        SyncRadioFromAuthModule(uid);
    }

    private void OnAuthModuleUninstalled(EntityUid uid, AiAuthModuleComponent comp, ref AiModuleUninstalledEvent args)
    {
        if (!TryComp<AiNetworkServerComponent>(args.ServerEnt, out var server))
            return;

        var binaryOnly = new HashSet<ProtoId<RadioChannelPrototype>> { "Binary" };

        // Reset AI brain channels to Binary only.
        if (server.LinkedCore != null && TryGetBrainFromCore(server.LinkedCore.Value, out var brainUid))
            SetEntityChannels(brainUid, binaryOnly);

        // Reset all paired Boris borgs to Binary only.
        SyncBorisBorgsOnServer(args.ServerEnt, binaryOnly);
    }

    private void OnServerCoreLinkChanged(EntityUid uid, AiNetworkServerComponent comp, ref AiServerCoreLinkChangedEvent args)
    {
        if (!TryFindAuthModule(comp, out var authModuleUid))
            return;

        SyncRadioFromAuthModule(authModuleUid);
    }

    private void OnBrainInserted(EntityUid uid, StationAiHeldComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != StationAiCoreComponent.Container)
            return;

        var coreUid = args.Container.Owner;
        if (!HasComp<StationAiCoreComponent>(coreUid))
            return;

        if (!TryFindServerForCore(coreUid, out _, out var serverComp))
            return;

        if (!TryFindAuthModule(serverComp!, out var authModuleUid))
            return;

        SyncRadioFromAuthModule(authModuleUid);
    }

    // --- Boris Borg Radio ---

    private void OnBorisRadioSyncNeeded(BorisRadioSyncNeededEvent args)
    {
        if (!TryComp<AiNetworkServerComponent>(args.ServerUid, out var server))
            return;

        if (!TryFindAuthModule(server, out var authModuleUid))
            return;

        SyncBorisRadioFromAuthModule(authModuleUid, server);
    }

    private void OnBorisRadioResetNeeded(BorisRadioResetNeededEvent args)
    {
        // Reset borg radio channels to just Binary (borg default).
        SetEntityChannels(args.BorgUid, new HashSet<ProtoId<RadioChannelPrototype>> { "Binary" });
    }

    // --- Core Sync ---

    private void SyncRadioFromAuthModule(EntityUid authModuleUid)
    {
        if (!TryComp<AiServerModuleComponent>(authModuleUid, out var module) || module.InstalledServer == null)
            return;

        if (!TryComp<AiNetworkServerComponent>(module.InstalledServer, out var server))
            return;

        if (!TryComp<EncryptionKeyHolderComponent>(authModuleUid, out var keyHolder))
            return;

        var channels = new HashSet<ProtoId<RadioChannelPrototype>>(keyHolder.Channels);
        channels.Add("Binary");

        // Sync to AI brain.
        if (server.LinkedCore != null && TryGetBrainFromCore(server.LinkedCore.Value, out var brainUid))
            SetEntityChannels(brainUid, channels);

        // Sync to all paired Boris borgs on this server.
        SyncBorisBorgsOnServer(module.InstalledServer.Value, channels);
    }

    /// <summary>
    /// Syncs radio channels from the Auth module to all Boris borgs paired to the given server.
    /// </summary>
    private void SyncBorisRadioFromAuthModule(EntityUid authModuleUid, AiNetworkServerComponent server)
    {
        if (!TryComp<EncryptionKeyHolderComponent>(authModuleUid, out var keyHolder))
            return;

        var channels = new HashSet<ProtoId<RadioChannelPrototype>>(keyHolder.Channels);
        channels.Add("Binary");

        if (!TryComp<AiServerModuleComponent>(authModuleUid, out var module) || module.InstalledServer == null)
            return;

        SyncBorisBorgsOnServer(module.InstalledServer.Value, channels);
    }

    /// <summary>
    /// Finds all Boris Control Modules on a server and syncs channels to their paired borgs.
    /// </summary>
    private void SyncBorisBorgsOnServer(EntityUid serverUid, HashSet<ProtoId<RadioChannelPrototype>> channels)
    {
        if (!TryComp<AiNetworkServerComponent>(serverUid, out var server))
            return;

        // Find Boris Control Module(s) on this server.
        foreach (var moduleEnt in server.ModuleContainer.ContainedEntities)
        {
            if (!TryComp<BorisControlModuleComponent>(moduleEnt, out var borisControl))
                continue;

            foreach (var borgUid in borisControl.PairedBorgs)
            {
                if (Exists(borgUid))
                    SetEntityChannels(borgUid, new HashSet<ProtoId<RadioChannelPrototype>>(channels));
            }
        }
    }

    private void SetEntityChannels(EntityUid uid, HashSet<ProtoId<RadioChannelPrototype>> channels)
    {
        if (TryComp<ActiveRadioComponent>(uid, out var activeRadio))
            activeRadio.Channels = channels;

        if (TryComp<IntrinsicRadioTransmitterComponent>(uid, out var transmitter))
            transmitter.Channels = channels;
    }

    private bool TryFindServerForCore(EntityUid coreUid, out EntityUid serverUid, out AiNetworkServerComponent? serverComp)
    {
        serverUid = EntityUid.Invalid;
        serverComp = null;

        var query = EntityQueryEnumerator<AiNetworkServerComponent>();
        while (query.MoveNext(out var uid, out var server))
        {
            if (server.LinkedCore == coreUid)
            {
                serverUid = uid;
                serverComp = server;
                return true;
            }
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

    private bool TryGetBrainFromCore(EntityUid coreUid, out EntityUid brainUid)
    {
        brainUid = EntityUid.Invalid;

        if (!_container.TryGetContainer(coreUid, StationAiCoreComponent.Container, out var container))
            return false;

        if (container.ContainedEntities.Count == 0)
            return false;

        brainUid = container.ContainedEntities[0];
        return true;
    }
}
