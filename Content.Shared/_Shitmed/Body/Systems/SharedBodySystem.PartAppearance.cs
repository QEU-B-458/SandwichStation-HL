using System.Diagnostics;
using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared._Shitmed.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Body.Systems;
public partial class SharedBodySystem
{
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    private void InitializePartAppearances()
    {
        SubscribeLocalEvent<BodyPartAppearanceComponent, ComponentStartup>(OnPartAppearanceStartup);
        SubscribeLocalEvent<BodyPartAppearanceComponent, AfterAutoHandleStateEvent>(HandleState);
        SubscribeLocalEvent<BodyComponent, BodyPartAddedEvent>(OnPartAttachedToBody);
        SubscribeLocalEvent<BodyComponent, BodyPartRemovedEvent>(OnPartDroppedFromBody);
    }

    private void OnPartAppearanceStartup(EntityUid uid, BodyPartAppearanceComponent component, ComponentStartup args)
    {
        if (!TryComp(uid, out BodyPartComponent? part)
            || ToHumanoidLayer(part) is not { } relevantLayer)
            return;

        if (part.BaseLayerId != null)
        {
            component.ID = part.BaseLayerId;
            component.Type = relevantLayer;
            return;
        }

        if (part.Body is not { Valid: true } body
            || !TryComp(body, out HumanoidAppearanceComponent? bodyAppearance))
            return;

        var customLayers = bodyAppearance.CustomBaseLayers;
        var spriteLayers = bodyAppearance.BaseLayers;
        component.Type = relevantLayer;

        part.Species = bodyAppearance.Species;

        if (customLayers.ContainsKey(component.Type))
        {
            component.ID = customLayers[component.Type].Id;
            component.Color = customLayers[component.Type].Color;
        }
        else if (spriteLayers.ContainsKey(component.Type))
        {
            component.ID = spriteLayers[component.Type].ID;
            component.Color = bodyAppearance.SkinColor;
        }
        else
        {
            component.ID = CreateIdFromPart(bodyAppearance, relevantLayer);
            component.Color = bodyAppearance.SkinColor;
        }

        // I HATE HARDCODED CHECKS I HATE HARDCODED CHECKS I HATE HARDCODED CHECKS
        if (part.PartType == BodyPartType.Head)
            component.EyeColor = bodyAppearance.EyeColor;

        var markingsByLayer = new Dictionary<HumanoidVisualLayers, List<Marking>>();

        foreach (var layer in HumanoidVisualLayersExtension.Sublayers(relevantLayer))
        {
            var category = MarkingCategoriesConversion.FromHumanoidVisualLayers(layer);
            if (bodyAppearance.MarkingSet.Markings.TryGetValue(category, out var markingList))
                markingsByLayer[layer] = markingList.Select(m => new Marking(m.MarkingId, m.MarkingColors.ToList()) { Forced = m.Forced }).ToList();
        }

        component.Markings = markingsByLayer;

        UpdateAppearance(body, component);
    }

    private string? CreateIdFromPart(HumanoidAppearanceComponent bodyAppearance, HumanoidVisualLayers part)
    {
        if (!Prototypes.TryIndex(bodyAppearance.Species, out SpeciesPrototype? speciesProto)
            || speciesProto.SpriteSet is not { } spriteSetId
            || !Prototypes.TryIndex(spriteSetId, out HumanoidSpeciesBaseSpritesPrototype? baseSprites)
            || !baseSprites.Sprites.TryGetValue(part, out var spriteId))
        {
            return null;
        }

        return HumanoidVisualLayersExtension.GetSexMorph(part, bodyAppearance.Sex, spriteId);
    }

    public void ModifyMarkings(EntityUid uid,
        Entity<BodyPartAppearanceComponent?> partAppearance,
        HumanoidAppearanceComponent bodyAppearance,
        HumanoidVisualLayers targetLayer,
        string markingId,
        bool remove = false)
    {
        // Floofstation - DO NOT TOUCH MARKINGS CLIENT-SIDE, YOU ARE DUPLICATING THEM!!!
        if (Net.IsClient && !IsClientSide(uid))
            return;

        if (!Resolve(partAppearance, ref partAppearance.Comp))
            return;

        if (!remove)
        {

            if (!Prototypes.TryIndex<MarkingPrototype>(markingId, out var prototype))
                return;

            var markingColors = MarkingColoring.GetMarkingLayerColors(
                    prototype,
                    bodyAppearance.SkinColor,
                    bodyAppearance.EyeColor,
                    partAppearance.Comp.Markings.GetValueOrDefault(targetLayer) ?? new List<Marking>()
                );

            var marking = new Marking(markingId, markingColors) { Forced = true };
            var dirty = false;

            _humanoid.SetLayerVisibility((uid, bodyAppearance), targetLayer, true, null, ref dirty);
            _humanoid.AddMarking(uid, markingId, markingColors, true, true, true, bodyAppearance);
            if (!partAppearance.Comp.Markings.ContainsKey(targetLayer))
                partAppearance.Comp.Markings[targetLayer] = new List<Marking>();

            partAppearance.Comp.Markings[targetLayer].Add(marking);

            if (dirty)
                Dirty(uid, bodyAppearance);
        }
        //else
            //RemovePartMarkings(uid, component, bodyAppearance);
    }

    private void HandleState(EntityUid uid, BodyPartAppearanceComponent component, ref AfterAutoHandleStateEvent args) =>
        ApplyPartMarkings(uid, component);

    private void OnPartAttachedToBody(EntityUid uid, BodyComponent component, ref BodyPartAddedEvent args)
    {
        if (!TryComp(args.Part, out BodyPartAppearanceComponent? partAppearance)
            || !TryComp(uid, out HumanoidAppearanceComponent? bodyAppearance))
            return;

        if (TryComp(args.Part, out BodyPartComponent? part)
            && ToHumanoidLayer(part) is { } relevantLayer)
        {
            partAppearance.Type = relevantLayer;
            partAppearance.ID = part.BaseLayerId ?? CreateIdFromPart(bodyAppearance, relevantLayer);
            partAppearance.Color = bodyAppearance.SkinColor;

            if (part.PartType == BodyPartType.Head)
                partAppearance.EyeColor = bodyAppearance.EyeColor;
        }

        if (partAppearance.ID != null)
            _humanoid.SetBaseLayerId(uid, partAppearance.Type, partAppearance.ID, sync: true, bodyAppearance);

        UpdateAppearance(uid, partAppearance);
    }

    private void OnPartDroppedFromBody(EntityUid uid, BodyComponent component, ref BodyPartRemovedEvent args)
    {
        if (TerminatingOrDeleted(uid)
            || TerminatingOrDeleted(args.Part)
            || !TryComp(uid, out HumanoidAppearanceComponent? bodyAppearance))
            return;

        // We check for this conditional here since some entities may not have a profile... If they dont
        // have one, and their part is gibbed, the markings will not be removed or applied properly.
        if (!HasComp<BodyPartAppearanceComponent>(args.Part))
            EnsureComp<BodyPartAppearanceComponent>(args.Part);

        if (TryComp<BodyPartAppearanceComponent>(args.Part, out var partAppearance))
            RemoveAppearance(uid, partAppearance, args.Part);
    }

    protected void UpdateAppearance(EntityUid target,
        BodyPartAppearanceComponent component,
        bool applyMarkings = true)
    {
        // Floofstation - DO NOT TOUCH MARKINGS CLIENT-SIDE, YOU ARE DUPLICATING THEM!!!
        if (Net.IsClient && !IsClientSide(target))
            return;

        if (!TryComp(target, out HumanoidAppearanceComponent? bodyAppearance))
            return;

        var dirty = false;

        if (component.EyeColor != null)
        {
            bodyAppearance.EyeColor = component.EyeColor.Value;
            _humanoid.SetLayerVisibility((target, bodyAppearance), HumanoidVisualLayers.Eyes, true, null, ref dirty);
        }

        if (component.Color != null)
            _humanoid.SetBaseLayerColor(target, component.Type, component.Color, true, bodyAppearance);

        _humanoid.SetLayerVisibility((target, bodyAppearance), component.Type, true, null, ref dirty);

        if (applyMarkings)
        {
            foreach (var (visualLayer, markingList) in component.Markings)
            {
                _humanoid.SetLayerVisibility((target, bodyAppearance), visualLayer, true, null, ref dirty);
                foreach (var marking in markingList)
                {
                    _humanoid.AddMarking(target, marking.MarkingId, marking.MarkingColors, true, true, true, bodyAppearance);
                }
            }
        }

        if (dirty)
            Dirty(target, bodyAppearance);
    }

    protected void RemoveAppearance(EntityUid entity, BodyPartAppearanceComponent component, EntityUid partEntity)
    {
        if (!TryComp(entity, out HumanoidAppearanceComponent? bodyAppearance))
            return;

        var dirty = false;

        foreach (var (visualLayer, markingList) in component.Markings)
        {
            _humanoid.SetLayerVisibility((entity, bodyAppearance), visualLayer, false, null, ref dirty);
            if (dirty)
                Dirty(entity, bodyAppearance);
        }
        RemoveBodyMarkings(entity, component, bodyAppearance);
    }

    /// <summary>
    /// Re-reads the body's <see cref="HumanoidAppearanceComponent"/> and pushes the result into
    /// every attached part's <see cref="BodyPartAppearanceComponent"/>, then re-applies the
    /// appearance to the body sprite.  Call this after bulk-updating a body's humanoid appearance
    /// (e.g. cloning, polymorph, changeling transform) so the part tree stays in sync.
    /// </summary>
    public void RefreshBodyPartAppearances(EntityUid bodyUid)
    {
        if (!TryComp(bodyUid, out HumanoidAppearanceComponent? bodyAppearance))
            return;

        foreach (var (partId, _) in GetBodyChildren(bodyUid))
        {
            if (!TryComp(partId, out BodyPartAppearanceComponent? partAppearance)
                || !TryComp(partId, out BodyPartComponent? part)
                || ToHumanoidLayer(part) is not { } relevantLayer)
                continue;

            partAppearance.Type = relevantLayer;
            part.Species = bodyAppearance.Species;

            var customLayers = bodyAppearance.CustomBaseLayers;
            var spriteLayers = bodyAppearance.BaseLayers;

            if (customLayers.ContainsKey(relevantLayer))
            {
                partAppearance.ID = customLayers[relevantLayer].Id;
                partAppearance.Color = customLayers[relevantLayer].Color;
            }
            else if (spriteLayers.ContainsKey(relevantLayer))
            {
                partAppearance.ID = spriteLayers[relevantLayer].ID;
                partAppearance.Color = bodyAppearance.SkinColor;
            }
            else
            {
                partAppearance.ID = CreateIdFromPart(bodyAppearance, relevantLayer);
                partAppearance.Color = bodyAppearance.SkinColor;
            }

            if (part.PartType == BodyPartType.Head)
                partAppearance.EyeColor = bodyAppearance.EyeColor;

            var markingsByLayer = new Dictionary<HumanoidVisualLayers, List<Marking>>();
            foreach (var layer in HumanoidVisualLayersExtension.Sublayers(relevantLayer))
            {
                var category = MarkingCategoriesConversion.FromHumanoidVisualLayers(layer);
                if (bodyAppearance.MarkingSet.Markings.TryGetValue(category, out var markingList))
                    markingsByLayer[layer] = markingList
                        .Select(m => new Marking(m.MarkingId, m.MarkingColors.ToList()) { Forced = m.Forced })
                        .ToList();
            }
            partAppearance.Markings = markingsByLayer;

            Dirty(partId, partAppearance);
            UpdateAppearance(bodyUid, partAppearance, applyMarkings: false);
        }
    }

    protected void ApplyPartMarkings(EntityUid target, BodyPartAppearanceComponent component)
    {
    }

    protected void RemoveBodyMarkings(EntityUid target, BodyPartAppearanceComponent partAppearance, HumanoidAppearanceComponent bodyAppearance)
    {
    }

    private static HumanoidVisualLayers? ToHumanoidLayer(BodyPartComponent part)
    {
        return (part.PartType, part.Symmetry) switch
        {
            (BodyPartType.Head, _) => HumanoidVisualLayers.Head,
            (BodyPartType.Torso, _) => HumanoidVisualLayers.Chest,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => HumanoidVisualLayers.LArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => HumanoidVisualLayers.RArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => HumanoidVisualLayers.LHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => HumanoidVisualLayers.RHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => HumanoidVisualLayers.LLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => HumanoidVisualLayers.RLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => HumanoidVisualLayers.LFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => HumanoidVisualLayers.RFoot,
            _ => null,
        };
    }
}
