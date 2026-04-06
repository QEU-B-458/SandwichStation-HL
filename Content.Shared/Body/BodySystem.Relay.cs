using Content.Shared.Body.Events;
using Content.Shared.Gibbing;
using Content.Shared.Medical;
using JetBrains.Annotations;

namespace Content.Shared.Body;

public sealed partial class BodySystem
{
    private void InitializeRelay()
    {
        SubscribeLocalEvent<BodyComponent, ApplyMetabolicMultiplierEvent>(RefRelayBodyEvent);
        SubscribeLocalEvent<BodyComponent, TryVomitEvent>(RefRelayBodyEvent);
        SubscribeLocalEvent<BodyComponent, BeingGibbedEvent>(OnBodyBeingGibbed);
    }

    private void OnBodyBeingGibbed(EntityUid uid, BodyComponent component, ref BeingGibbedEvent args)
    {
        // Add all body parts so they drop as giblets.
        foreach (var (partId, _) in _sharedBody.GetBodyChildren(uid, component))
        {
            args.Giblets.Add(partId);
        }
        // Relay to organs so GibbableOrganComponent can add them too.
        RelayEvent((uid, component), ref args);
    }

    private void RefRelayBodyEvent<T>(EntityUid uid, BodyComponent component, ref T args) where T : struct
    {
        RelayEvent((uid, component), ref args);
    }

    /// <summary>
    /// Relays the given event to organs within a body.
    /// </summary>
    [PublicAPI]
    public void RelayEvent<T>(Entity<BodyComponent> ent, ref T args) where T : struct
    {
        var ev = new BodyRelayedEvent<T>(ent, args);
        foreach (var organ in ent.Comp.Organs?.ContainedEntities ?? [])
        {
            RaiseLocalEvent(organ, ref ev);
        }
        args = ev.Args;
    }

    /// <summary>
    /// Relays the given event to organs within a body.
    /// </summary>
    [PublicAPI]
    public void RelayEvent<T>(Entity<BodyComponent> ent, T args) where T : class
    {
        var ev = new BodyRelayedEvent<T>(ent, args);
        foreach (var organ in ent.Comp.Organs?.ContainedEntities ?? [])
        {
            RaiseLocalEvent(organ, ref ev);
        }
    }
}

/// <summary>
/// Event wrapper for events being relayed to organs within a body.
/// </summary>
[ByRefEvent]
public record struct BodyRelayedEvent<TEvent>(Entity<BodyComponent> Body, TEvent Args);
