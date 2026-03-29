using Content.Shared._Sandwich.Silicons.StationAi.Components;
using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Containers;

namespace Content.Shared._Sandwich.Silicons.StationAi;

/// <summary>
/// Provides access from the Auth module's ID card to the AI brain.
/// This must be in shared code so client-side door prediction works correctly.
/// Chain: brain → core → server → Auth module → ID card.
/// </summary>
public sealed class SharedAiAuthAccessSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAiHeldComponent, GetAdditionalAccessEvent>(OnAiGetAdditionalAccess);

        // Boris borgs: get access from the same Auth module ID card as the AI.
        // Only fires when the borg has a paired BorisModule — regular borgs are untouched.
        SubscribeLocalEvent<BorgChassisComponent, GetAdditionalAccessEvent>(OnBorgGetAdditionalAccess);
    }

    private void OnAiGetAdditionalAccess(
        EntityUid uid,
        StationAiHeldComponent comp,
        ref GetAdditionalAccessEvent args)
    {
        if (!TryGetAuthIdCard(uid, out var idCardUid))
            return;

        args.Entities.Add(idCardUid);
    }

    private void OnBorgGetAdditionalAccess(
        EntityUid uid,
        BorgChassisComponent comp,
        ref GetAdditionalAccessEvent args)
    {
        // Only respond if this borg has a paired Boris module.
        if (!TryFindPairedBorisModule(uid, comp, out var pairedServer))
            return;

        // Trace: paired server → Auth module → ID card.
        if (!TryComp<AiNetworkServerComponent>(pairedServer, out var server))
            return;

        if (!TryGetIdCardFromServer(server, out var idCard))
            return;

        args.Entities.Add(idCard);
    }

    /// <summary>
    /// Checks the borg's brain slot for a BorisModuleComponent that is paired to a server.
    /// Returns the paired server entity if found. Regular borgs (no Boris brain or unpaired) return false.
    /// </summary>
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

    /// <summary>
    /// Traces from an AI brain entity through the core → server → Auth module
    /// chain to find the ID card in the Auth module's slot.
    /// </summary>
    public bool TryGetAuthIdCard(EntityUid brainUid, out EntityUid idCard)
    {
        idCard = EntityUid.Invalid;

        // brain → core (brain is inside the core's container)
        if (!_container.TryGetContainingContainer(brainUid, out var brainContainer))
            return false;

        var coreUid = brainContainer.Owner;
        if (!HasComp<StationAiCoreComponent>(coreUid))
            return false;

        // core → server (find server linked to this core)
        if (!TryFindServerForCore(coreUid, out _, out var serverComp))
            return false;

        // server → Auth module → ID card
        return TryGetIdCardFromServer(serverComp!, out idCard);
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

    private bool TryGetIdCardFromServer(AiNetworkServerComponent server, out EntityUid idCard)
    {
        idCard = EntityUid.Invalid;

        if (!TryFindAuthModule(server, out var authModuleUid))
            return false;

        if (!_itemSlots.TryGetSlot(authModuleUid, AiAuthModuleComponent.IdCardSlotId, out var slot))
            return false;

        if (slot.Item == null)
            return false;

        idCard = slot.Item.Value;
        return true;
    }
}
