using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Body;
using System.Linq;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid;

public abstract class SharedHumanoidAppearanceSystem : EntitySystem
{
    [Dependency] private readonly MarkingManager _markings = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public HumanoidCharacterAppearance? GetCharacterAppearance(
        EntityUid uid,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid, false))
            return null;

        var markingData = _markings.GetMarkingData(humanoid.Species);
        var organMarkings = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>();

        foreach (var (organ, data) in markingData)
        {
            var layers = new Dictionary<HumanoidVisualLayers, List<Marking>>();

            foreach (var layer in data.Layers)
            {
                var category = MarkingCategoriesConversion.FromHumanoidVisualLayers(layer);
                if (!humanoid.MarkingSet.Markings.TryGetValue(category, out var markings) || markings.Count == 0)
                    continue;

                layers[layer] = markings
                    .Select(m => new Marking(m.MarkingId, m.MarkingColors) { Forced = m.Forced })
                    .ToList();
            }

            organMarkings[organ] = layers;
        }

        return new HumanoidCharacterAppearance(
            humanoid.EyeColor,
            humanoid.SkinColor,
            organMarkings);
    }

    public HumanoidCharacterProfile? GetCharacterProfile(
        EntityUid uid,
        HumanoidProfileComponent? profile = null,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref profile, false) || !Resolve(uid, ref humanoid, false))
            return null;

        var appearance = GetCharacterAppearance(uid, humanoid);
        if (appearance == null)
            return null;

        return new HumanoidCharacterProfile()
        {
            Species = humanoid.Species,
            Age = humanoid.Age,
            Appearance = appearance,
        }.WithSex(humanoid.Sex).WithGender(humanoid.Gender);
    }

    public void ApplyProfileData(
        EntityUid uid,
        HumanoidCharacterProfile profile,
        bool sync = true,
        HumanoidAppearanceComponent? humanoid = null)
    {
        ApplyAppearanceData(uid,
            profile.Species,
            profile.Sex,
            profile.Gender,
            profile.Age,
            profile.Appearance,
            sync,
            humanoid);
    }

    public void ApplyAppearanceData(
        EntityUid uid,
        ProtoId<SpeciesPrototype> species,
        Sex sex,
        Gender gender,
        int age,
        HumanoidCharacterAppearance appearance,
        bool sync = true,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid, false))
            return;

        humanoid.Species = species;
        humanoid.Sex = sex;
        humanoid.Gender = gender;
        humanoid.Age = age;
        humanoid.SkinColor = appearance.SkinColor;
        humanoid.EyeColor = appearance.EyeColor;
        humanoid.MarkingSet.Markings.Clear();

        foreach (var (_, organMarkings) in appearance.Markings)
        {
            foreach (var (layer, markings) in organMarkings)
            {
                var category = MarkingCategoriesConversion.FromHumanoidVisualLayers(layer);

                if (!humanoid.MarkingSet.Markings.TryGetValue(category, out var bucket))
                {
                    bucket = new List<Marking>();
                    humanoid.MarkingSet.Markings[category] = bucket;
                }

                bucket.AddRange(markings);
            }
        }

        if (humanoid.MarkingSet.Markings.Count > 0)
        {
            var total = humanoid.MarkingSet.Markings.Sum(x => x.Value.Count);
            var chest = humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Chest, out var chestMarkings)
                ? string.Join(", ", chestMarkings.Select(x => x.MarkingId.Id))
                : "<none>";
            Log.Info($"Humanoid MarkingSet debug for {ToPrettyString(uid)}: species={species.Id}, total={total}, chest={chest}.");
        }

        if (sync)
            Dirty(uid, humanoid);
    }

    public void SetLayerVisibility(Entity<HumanoidAppearanceComponent?> ent,
        HumanoidVisualLayers layer,
        bool visible,
        SlotFlags? source = null)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        var dirty = false;
        SetLayerVisibility(ent!, layer, visible, source, ref dirty);
        if (dirty)
            Dirty(ent);
    }

    public virtual void SetLayerVisibility(
        Entity<HumanoidAppearanceComponent> ent,
        HumanoidVisualLayers layer,
        bool visible,
        SlotFlags? source,
        ref bool dirty)
    {
        if (visible)
        {
            if (source is not { } slot)
                dirty |= ent.Comp.PermanentlyHidden.Remove(layer);
            else if (ent.Comp.HiddenLayers.TryGetValue(layer, out var oldSlots))
            {
                ent.Comp.HiddenLayers[layer] = ~slot & oldSlots;
                if (ent.Comp.HiddenLayers[layer] == SlotFlags.NONE)
                    ent.Comp.HiddenLayers.Remove(layer);

                dirty |= (oldSlots & slot) != 0;
            }
        }
        else
        {
            if (source is not { } slot)
                dirty |= ent.Comp.PermanentlyHidden.Add(layer);
            else
            {
                var oldSlots = ent.Comp.HiddenLayers.GetValueOrDefault(layer);
                ent.Comp.HiddenLayers[layer] = slot | oldSlots;
                dirty |= (oldSlots & slot) != slot;
            }
        }
    }

    public void SetBaseLayerId(EntityUid uid, HumanoidVisualLayers layer, string? id, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid, false))
            return;

        ProtoId<HumanoidSpeciesSpriteLayer>? protoId = string.IsNullOrEmpty(id)
            ? default
            : new ProtoId<HumanoidSpeciesSpriteLayer>(id);
        if (humanoid.CustomBaseLayers.TryGetValue(layer, out var info))
            humanoid.CustomBaseLayers[layer] = info with { Id = protoId };
        else
            humanoid.CustomBaseLayers[layer] = new CustomBaseLayerInfo(protoId);

        if (sync)
            Dirty(uid, humanoid);
    }

    public void SetBaseLayerColor(EntityUid uid, HumanoidVisualLayers layer, Color? color, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid, false))
            return;

        if (humanoid.CustomBaseLayers.TryGetValue(layer, out var info))
            humanoid.CustomBaseLayers[layer] = info with { Color = color };
        else
            humanoid.CustomBaseLayers[layer] = new CustomBaseLayerInfo(default(ProtoId<HumanoidSpeciesSpriteLayer>?), color);

        if (sync)
            Dirty(uid, humanoid);
    }

    public void AddMarking(EntityUid uid, string marking, IReadOnlyList<Color> colors, bool isGlowing, bool sync = true, bool forced = false, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid, false)
            || !_prototypes.TryIndex<MarkingPrototype>(marking, out var prototype))
            return;

        var category = MarkingCategoriesConversion.FromHumanoidVisualLayers(prototype.BodyPart);
        humanoid.MarkingSet.AddBack(category, new Marking(marking, colors) { Forced = forced });
        if (sync)
            Dirty(uid, humanoid);
    }
}
