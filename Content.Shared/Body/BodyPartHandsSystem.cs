using Content.Shared._Shitmed.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;

namespace Content.Shared.Body;

public sealed class BodyPartHandsSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyPartHandsComponent, BodyPartComponentsModifyEvent>(OnBodyPartHandsModify);
    }

    private void OnBodyPartHandsModify(Entity<BodyPartHandsComponent> ent, ref BodyPartComponentsModifyEvent args)
    {
        if (!TryComp<HandsComponent>(args.Body, out var hands))
            return;

        foreach (var (handName, hand) in ent.Comp.Hands)
        {
            if (args.Add)
                _hands.AddHand((args.Body, hands), handName, hand.Location, hand.EmptyLabel, hand.EmptyRepresentative, hand.Whitelist, hand.Blacklist);
            else
                _hands.RemoveHand((args.Body, hands), handName);
        }
    }
}
