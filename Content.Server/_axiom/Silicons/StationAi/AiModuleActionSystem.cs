using Content.Shared._axiom.Silicons.StationAi.Components;
using Content.Shared.Actions;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Containers;

namespace Content.Server._axiom.Silicons.StationAi;

/// <summary>
/// Grants/revokes action buttons on the AI brain when modules with GrantedAction
/// are installed or removed from the AI server.
/// Handles all orderings: module before brain, brain before module, server link changes.
/// </summary>
public sealed class AiModuleActionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Module installed/removed from server.
        SubscribeLocalEvent<AiServerModuleComponent, AiModuleInstalledEvent>(OnModuleInstalled);
        SubscribeLocalEvent<AiServerModuleComponent, AiModuleUninstalledEvent>(OnModuleUninstalled);

        // Brain inserted into core — grant actions from any existing modules.
        // Uses EntGotInsertedIntoContainerMessage (raised on the brain) to avoid
        // duplicate subscription conflicts with SharedStationAiSystem and AiAuthRadioSystem.
        SubscribeLocalEvent<StationAiHeldComponent, EntGotInsertedIntoContainerMessage>(OnBrainInsertedIntoCore);
    }

    private void OnModuleInstalled(EntityUid uid, AiServerModuleComponent comp, ref AiModuleInstalledEvent args)
    {
        if (comp.GrantedAction == null)
            return;

        if (!TryGetBrainFromServer(args.ServerEnt, out var brainUid))
            return;

        GrantActionToBrain(brainUid, uid, comp);
    }

    private void OnModuleUninstalled(EntityUid uid, AiServerModuleComponent comp, ref AiModuleUninstalledEvent args)
    {
        if (comp.GrantedAction == null)
            return;

        if (!TryGetBrainFromServer(args.ServerEnt, out var brainUid))
            return;

        RevokeActionFromBrain(brainUid, uid, comp);
    }

    private void OnBrainInsertedIntoCore(EntityUid uid, StationAiHeldComponent comp, EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != StationAiCoreComponent.Container)
            return;

        // uid is the brain, container owner is the core.
        var coreUid = args.Container.Owner;
        if (!HasComp<StationAiCoreComponent>(coreUid))
            return;

        GrantAllModuleActions(uid, coreUid);
    }

    // --- Helpers ---

    private void GrantActionToBrain(EntityUid brainUid, EntityUid moduleUid, AiServerModuleComponent module)
    {
        if (module.GrantedAction == null || module.GrantedActionEntity != null)
            return;

        _actions.AddAction(brainUid, ref module.GrantedActionEntity, module.GrantedAction, moduleUid);
    }

    private void RevokeActionFromBrain(EntityUid brainUid, EntityUid moduleUid, AiServerModuleComponent module)
    {
        _actions.RemoveProvidedActions(brainUid, moduleUid);
        module.GrantedActionEntity = null;
    }

    /// <summary>
    /// Grants actions from all modules on all servers linked to the core containing this brain.
    /// </summary>
    private void GrantAllModuleActions(EntityUid brainUid, EntityUid coreUid)
    {
        var query = EntityQueryEnumerator<AiNetworkServerComponent>();
        while (query.MoveNext(out _, out var server))
        {
            if (server.LinkedCore != coreUid)
                continue;

            foreach (var moduleEnt in server.ModuleContainer.ContainedEntities)
            {
                if (TryComp<AiServerModuleComponent>(moduleEnt, out var module) && module.GrantedAction != null)
                    GrantActionToBrain(brainUid, moduleEnt, module);
            }
        }
    }

    private bool TryGetBrainFromServer(EntityUid serverUid, out EntityUid brainUid)
    {
        brainUid = EntityUid.Invalid;

        if (!TryComp<AiNetworkServerComponent>(serverUid, out var server) || server.LinkedCore == null)
            return false;

        return TryGetBrainFromCore(server.LinkedCore.Value, out brainUid);
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
