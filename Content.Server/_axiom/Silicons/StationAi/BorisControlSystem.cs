using Content.Shared._axiom.Silicons.StationAi;
using Content.Shared._axiom.Silicons.StationAi.Components;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Lock;
using Robust.Shared.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._axiom.Silicons.StationAi;

/// <summary>
/// Handles the Boris system: 4-digit pairing codes on Boris Control Modules (AI server side)
/// and AI Remote Control Brains (borg brain slot). When paired, the borg's own access is stripped
/// and replaced with access from the AI's Auth module ID card. Radio channels are synced similarly.
/// Regular player borgs are completely unaffected.
/// </summary>
public sealed class BorisControlSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AiServerModuleSystem _aiModules = default!;
    [Dependency] private readonly SharedAccessSystem _access = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Boris Control Module (AI server side)
        SubscribeLocalEvent<BorisControlModuleComponent, MapInitEvent>(OnControlModuleMapInit);
        SubscribeLocalEvent<BorisControlModuleComponent, AiModuleUninstalledEvent>(OnControlModuleUninstalled);

        // AI Remote Control Brain (borg brain slot) — inserted/removed from borg chassis
        SubscribeLocalEvent<BorisModuleComponent, EntGotInsertedIntoContainerMessage>(OnBrainInserted);
        SubscribeLocalEvent<BorisModuleComponent, EntGotRemovedFromContainerMessage>(OnBrainRemoved);

        // Verb on borg chassis to open Boris pairing UI
        SubscribeLocalEvent<BorgChassisComponent, GetVerbsEvent<AlternativeVerb>>(OnBorgGetVerbs);

        // Boris pairing UI messages (on borg chassis)
        SubscribeLocalEvent<BorgChassisComponent, BorisSubmitCodeBuiMessage>(OnSubmitCode);
        SubscribeLocalEvent<BorgChassisComponent, BorisUnpairBuiMessage>(OnUnpair);
    }

    // --- Code Generation ---

    private void OnControlModuleMapInit(EntityUid uid, BorisControlModuleComponent comp, MapInitEvent args)
    {
        if (string.IsNullOrEmpty(comp.PairingCode))
        {
            comp.PairingCode = GenerateCode();
            Dirty(uid, comp);
        }
    }

    private string GenerateCode()
    {
        return _random.Next(1000, 9999).ToString("D4");
    }

    // --- Control Module Lifecycle ---

    private void OnControlModuleUninstalled(EntityUid uid, BorisControlModuleComponent comp, ref AiModuleUninstalledEvent args)
    {
        UnpairAllBorgs(uid, comp);
    }

    // --- Boris Brain Lifecycle ---

    private void OnBrainInserted(EntityUid uid, BorisModuleComponent comp, EntGotInsertedIntoContainerMessage args)
    {
        // Only care about insertion into the borg brain slot.
        if (args.Container.ID != "borg_brain")
            return;

        // If this brain was already paired (re-inserted into a borg), strip borg access again.
        if (comp.PairedServer != null)
            StripBorgAccess(args.Container.Owner, comp);
    }

    private void OnBrainRemoved(EntityUid uid, BorisModuleComponent comp, EntGotRemovedFromContainerMessage args)
    {
        if (args.Container.ID != "borg_brain")
            return;

        var chassisUid = args.Container.Owner;

        if (comp.PairedControlModule != null && TryComp<BorisControlModuleComponent>(comp.PairedControlModule, out var control))
        {
            control.PairedBorgs.Remove(chassisUid);
            Dirty(comp.PairedControlModule.Value, control);
        }

        // Restore borg's original access before removing.
        if (comp.PairedServer != null)
            RestoreBorgAccess(chassisUid, comp);

        comp.PairedServer = null;
        comp.PairedControlModule = null;
        Dirty(uid, comp);
    }

    // --- Verb on Borg Chassis ---

    private void OnBorgGetVerbs(EntityUid uid, BorgChassisComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Only show verb if this borg has a Boris brain in the brain slot.
        if (!TryFindBorisBrain(uid, comp, out _, out _))
            return;

        // Don't show pairing verb when borg is locked.
        if (TryComp<LockComponent>(uid, out var lockComp) && lockComp.Locked)
            return;

        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("boris-enter-code-verb"),
            Act = () =>
            {
                if (!TryComp<ActorComponent>(args.User, out var actor))
                    return;

                _ui.TryToggleUi(uid, BorisUiKey.Key, actor.PlayerSession);
                UpdateBorisUi(uid);
            }
        };
        args.Verbs.Add(verb);
    }

    // --- Pairing UI ---

    private void OnSubmitCode(EntityUid uid, BorgChassisComponent comp, BorisSubmitCodeBuiMessage args)
    {
        if (!TryFindBorisBrain(uid, comp, out var brainUid, out var borisMod))
            return;

        if (borisMod!.PairedServer != null)
            return; // Already paired

        var code = args.Code.Trim();
        if (code.Length != 4)
            return;

        if (!TryFindControlModuleByCode(code, out var controlUid, out var controlComp, out var serverUid))
        {
            _popup.PopupEntity(Loc.GetString("boris-invalid-code"), uid, args.Actor);
            return;
        }

        // Pair the borg.
        borisMod.PairedServer = serverUid;
        borisMod.PairedControlModule = controlUid;
        controlComp!.PairedBorgs.Add(uid);
        Dirty(brainUid, borisMod);
        Dirty(controlUid, controlComp!);

        // Strip the borg's own access — it now gets access from the Auth module's ID card.
        StripBorgAccess(uid, borisMod);

        // Sync radio channels from Auth module.
        RaiseLocalEvent(new BorisRadioSyncNeededEvent(serverUid));

        _popup.PopupEntity(Loc.GetString("boris-paired"), uid, args.Actor);
        UpdateBorisUi(uid);

        // Update the AI server UI.
        if (TryComp<AiServerModuleComponent>(controlUid, out var module) && module.InstalledServer != null)
            _aiModules.UpdateUI(module.InstalledServer.Value);
    }

    private void OnUnpair(EntityUid uid, BorgChassisComponent comp, BorisUnpairBuiMessage args)
    {
        if (!TryFindBorisBrain(uid, comp, out var brainUid, out var borisMod))
            return;

        if (borisMod!.PairedServer == null)
            return;

        if (borisMod.PairedControlModule != null && TryComp<BorisControlModuleComponent>(borisMod.PairedControlModule, out var control))
        {
            control.PairedBorgs.Remove(uid);
            Dirty(borisMod.PairedControlModule.Value, control);
        }

        // Restore the borg's original access.
        RestoreBorgAccess(uid, borisMod);

        // Reset radio channels to borg defaults.
        ResetBorgRadio(uid);

        borisMod.PairedServer = null;
        borisMod.PairedControlModule = null;
        Dirty(brainUid, borisMod);

        _popup.PopupEntity(Loc.GetString("boris-unpaired"), uid, args.Actor);
        UpdateBorisUi(uid);
    }

    // --- Access Stripping / Restoring ---

    /// <summary>
    /// Backs up the borg's current access tags, then clears them.
    /// The borg will now only get access through GetAdditionalAccessEvent → Auth module ID card.
    /// Groups are expanded into tags at MapInit so we only need to back up tags.
    /// </summary>
    private void StripBorgAccess(EntityUid borgUid, BorisModuleComponent borisMod)
    {
        var tags = _access.TryGetTags(borgUid);
        if (tags == null)
            return;

        // Back up current tags (only if we haven't already — re-insert case).
        if (borisMod.OriginalAccessTags.Count == 0)
        {
            borisMod.OriginalAccessTags = new(tags);
        }

        // Clear the borg's own access. Keep the Borg tag so borg-specific doors still work.
        _access.TrySetTags(borgUid, new ProtoId<AccessLevelPrototype>[] { "Borg" });
    }

    /// <summary>
    /// Restores the borg's original access tags from the backup.
    /// </summary>
    private void RestoreBorgAccess(EntityUid borgUid, BorisModuleComponent borisMod)
    {
        if (borisMod.OriginalAccessTags.Count == 0)
            return;

        _access.TrySetTags(borgUid, borisMod.OriginalAccessTags);

        // Clear the backup.
        borisMod.OriginalAccessTags.Clear();
    }

    /// <summary>
    /// Resets a borg's radio channels back to defaults (clears any Auth module channels).
    /// The actual default channels are set by the borg's own IntrinsicRadioTransmitter/ActiveRadio.
    /// </summary>
    private void ResetBorgRadio(EntityUid borgUid)
    {
        // Radio reset is handled by AiAuthRadioSystem when it detects the borg is unpaired.
        // We raise a targeted event so it can clean up.
        RaiseLocalEvent(new BorisRadioResetNeededEvent(borgUid));
    }

    // --- Helpers ---

    /// <summary>
    /// Find the BorisModuleComponent on the brain in the borg's brain slot.
    /// </summary>
    private bool TryFindBorisBrain(EntityUid chassisUid, BorgChassisComponent? chassis, out EntityUid brainUid, out BorisModuleComponent? borisMod)
    {
        brainUid = EntityUid.Invalid;
        borisMod = null;

        if (!Resolve(chassisUid, ref chassis, false))
            return false;

        var brain = chassis.BrainEntity;
        if (brain == null)
            return false;

        if (!TryComp<BorisModuleComponent>(brain.Value, out var boris))
            return false;

        brainUid = brain.Value;
        borisMod = boris;
        return true;
    }

    private bool TryFindControlModuleByCode(
        string code,
        out EntityUid controlUid,
        out BorisControlModuleComponent? controlComp,
        out EntityUid serverUid)
    {
        controlUid = EntityUid.Invalid;
        controlComp = null;
        serverUid = EntityUid.Invalid;

        var query = EntityQueryEnumerator<BorisControlModuleComponent, AiServerModuleComponent>();
        while (query.MoveNext(out var uid, out var boris, out var module))
        {
            if (boris.PairingCode == code && module.InstalledServer != null)
            {
                controlUid = uid;
                controlComp = boris;
                serverUid = module.InstalledServer.Value;
                return true;
            }
        }

        return false;
    }

    private void UnpairAllBorgs(EntityUid controlUid, BorisControlModuleComponent comp)
    {
        foreach (var borgUid in comp.PairedBorgs)
        {
            if (!TryComp<BorgChassisComponent>(borgUid, out var chassis))
                continue;

            if (!TryFindBorisBrain(borgUid, chassis, out var brainUid, out var borisMod))
                continue;

            if (borisMod!.PairedControlModule != controlUid)
                continue;

            // Restore borg access before unpairing.
            RestoreBorgAccess(borgUid, borisMod);
            ResetBorgRadio(borgUid);

            borisMod.PairedServer = null;
            borisMod.PairedControlModule = null;
            Dirty(brainUid, borisMod);
        }

        comp.PairedBorgs.Clear();
        Dirty(controlUid, comp);
    }

    private void UpdateBorisUi(EntityUid chassisUid)
    {
        if (!TryFindBorisBrain(chassisUid, null, out _, out var borisMod))
            return;

        var isPaired = borisMod!.PairedServer != null;
        string? serverName = null;

        if (isPaired && Exists(borisMod.PairedServer!.Value))
            serverName = MetaData(borisMod.PairedServer.Value).EntityName;

        var state = new BorisBuiState(isPaired, serverName);
        _ui.SetUiState(chassisUid, BorisUiKey.Key, state);
    }
}

/// <summary>
/// Raised when a Boris borg pairs and needs its radio channels synced from the Auth module.
/// </summary>
public sealed class BorisRadioSyncNeededEvent : EntityEventArgs
{
    public EntityUid ServerUid;
    public BorisRadioSyncNeededEvent(EntityUid serverUid) { ServerUid = serverUid; }
}

/// <summary>
/// Raised when a Boris borg unpairs and needs its radio channels reset to defaults.
/// </summary>
public sealed class BorisRadioResetNeededEvent : EntityEventArgs
{
    public EntityUid BorgUid;
    public BorisRadioResetNeededEvent(EntityUid borgUid) { BorgUid = borgUid; }
}
