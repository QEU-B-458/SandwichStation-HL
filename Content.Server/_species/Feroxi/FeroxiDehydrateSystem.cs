using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Metabolism;
using Content.Shared.Nutrition.Components;

namespace Content.Server._species.Feroxi;

public sealed class FeroxiDehydrateSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<FeroxiDehydrateComponent, ThirstComponent>();

        while (query.MoveNext(out var uid, out var feroxiDehydrate, out var thirst))
        {
            var currentThirst = thirst.CurrentThirst;
            var shouldBeDehydrated = currentThirst <= feroxiDehydrate.DehydrationThreshold;

            if (feroxiDehydrate.Dehydrated != shouldBeDehydrated)
            {
                UpdateDehydrationStatus((uid, feroxiDehydrate), shouldBeDehydrated);
            }
        }
    }

    private void UpdateDehydrationStatus(Entity<FeroxiDehydrateComponent> ent, bool shouldBeDehydrated)
    {
        ent.Comp.Dehydrated = shouldBeDehydrated;

        if (!TryComp<BodyComponent>(ent.Owner, out var body) || body.Organs == null)
            return;

        foreach (var organUid in body.Organs.ContainedEntities)
        {
            if (!HasComp<LungComponent>(organUid))
                continue;

            if (!TryComp<MetabolizerComponent>(organUid, out var metabolizer) || metabolizer.MetabolizerTypes == null)
                continue;

            var newMetabolizer = shouldBeDehydrated ? ent.Comp.DehydratedMetabolizer : ent.Comp.HydratedMetabolizer;
            metabolizer.MetabolizerTypes.Clear();
            metabolizer.MetabolizerTypes.Add(newMetabolizer);
        }
    }
}
