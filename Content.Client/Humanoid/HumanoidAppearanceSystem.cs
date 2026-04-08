using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using System.Linq;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Humanoid;

public sealed class HumanoidAppearanceSystem : SharedHumanoidAppearanceSystem
{
    [Dependency] private readonly HumanoidProfileSystem _humanoidProfile = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    private bool _loggedStartup;
    private bool _loggedFirstState;
    private readonly HashSet<string> _loggedSpeciesStates = new();
    private readonly HashSet<string> _loggedMarkingStates = new();

    public override void Initialize()
    {
        base.Initialize();

        if (!_loggedStartup)
        {
            Log.Info("HumanoidAppearanceSystem initialized on client.");
            _loggedStartup = true;
        }

        SubscribeLocalEvent<HumanoidAppearanceComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HumanoidAppearanceComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<HumanoidProfileComponent, ComponentStartup>(OnProfileStartup);
    }

    private void OnStartup(EntityUid uid, HumanoidAppearanceComponent component, ref ComponentStartup args)
    {
        if (TryComp(uid, out SpriteComponent? sprite))
            UpdateSprite(uid, component, sprite);
    }

    private void OnHandleState(EntityUid uid, HumanoidAppearanceComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (!_loggedFirstState)
        {
            Log.Info($"HumanoidAppearanceSystem received first humanoid appearance state for {ToPrettyString(uid)}.");
            _loggedFirstState = true;
        }

        if (TryComp(uid, out SpriteComponent? sprite))
            UpdateSprite(uid, component, sprite);
    }

    private void OnProfileStartup(EntityUid uid, HumanoidProfileComponent component, ComponentStartup args)
    {
        if (!IsClientSide(uid))
            return;

        var profile = HumanoidCharacterProfile.DefaultWithSpecies(component.Species, component.Sex);
        _humanoidProfile.ApplyProfileTo((uid, component), profile);
        RefreshHumanoid(uid);
    }

    private void UpdateSprite(EntityUid uid, HumanoidAppearanceComponent component, SpriteComponent sprite)
    {
        UpdateLayers(uid, component, sprite);
        ApplyMarkingSet(uid, component, sprite);

        if (_sprite.LayerMapTryGet((uid, sprite), HumanoidVisualLayers.Eyes, out var eyeIndex, false))
            _sprite.LayerSetColor((uid, sprite), eyeIndex, component.EyeColor);

        var speciesId = component.Species.ToString();
        if (_loggedSpeciesStates.Add(speciesId))
        {
            var chest = component.CustomBaseLayers.TryGetValue(HumanoidVisualLayers.Chest, out var customChest)
                ? customChest.Id?.ToString() ?? "<null>"
                : component.BaseLayers.TryGetValue(HumanoidVisualLayers.Chest, out var baseChest)
                    ? baseChest.ID.ToString()
                    : "<none>";

            var head = component.CustomBaseLayers.TryGetValue(HumanoidVisualLayers.Head, out var customHead)
                ? customHead.Id?.ToString() ?? "<null>"
                : component.BaseLayers.TryGetValue(HumanoidVisualLayers.Head, out var baseHead)
                    ? baseHead.ID.ToString()
                    : "<none>";

            Log.Info($"Humanoid appearance debug for {ToPrettyString(uid)}: species={component.Species}, chest={chest}, head={head}.");
        }
    }

    public void RefreshHumanoid(EntityUid uid)
    {
        if (!TryComp(uid, out HumanoidAppearanceComponent? appearance)
            || !TryComp(uid, out SpriteComponent? sprite))
            return;

        UpdateSprite(uid, appearance, sprite);
    }

    private static bool IsHidden(HumanoidAppearanceComponent humanoid, HumanoidVisualLayers layer)
        => humanoid.HiddenLayers.ContainsKey(layer) || humanoid.PermanentlyHidden.Contains(layer);

    private void ApplyMarkingSet(EntityUid uid, HumanoidAppearanceComponent component, SpriteComponent sprite)
    {
        ClearAllMarkings(uid, component, sprite);

        var layerOffsets = new Dictionary<HumanoidVisualLayers, int>();

        if (component.MarkingSet.Markings.Count > 0)
        {
            var chest = component.MarkingSet.Markings.TryGetValue(MarkingCategories.Chest, out var chestMarkings)
                ? string.Join(", ", chestMarkings.Select(x => x.MarkingId.Id))
                : "<none>";
            var key = $"{component.Species}:{uid}:{chest}";
            if (_loggedMarkingStates.Add(key))
                Log.Info($"Humanoid marking render debug for {ToPrettyString(uid)}: species={component.Species}, chestMarkings={chest}.");
        }

        foreach (var markingList in component.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (_markingManager.TryGetMarking(marking, out var prototype))
                    ApplyMarking(uid, component, sprite, prototype, marking, layerOffsets);
            }
        }
    }

    private void ClearAllMarkings(EntityUid uid, HumanoidAppearanceComponent component, SpriteComponent sprite)
    {
        foreach (var layerId in component.ClientMarkingLayers)
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), layerId, out var index, false))
                continue;

            _sprite.LayerMapRemove((uid, sprite), layerId);
            _sprite.RemoveLayer((uid, sprite), index);
        }

        component.ClientMarkingLayers.Clear();
    }

    private void ApplyMarking(
        EntityUid uid,
        HumanoidAppearanceComponent component,
        SpriteComponent sprite,
        MarkingPrototype prototype,
        Marking marking,
        Dictionary<HumanoidVisualLayers, int> layerOffsets)
    {
        var bodyLayer = prototype.BodyPart;

        if (IsHidden(component, bodyLayer))
        {
            Log.Info($"Humanoid marking skipped for {ToPrettyString(uid)}: species={component.Species}, marking={prototype.ID}, layer={bodyLayer}, reason=hidden.");
            return;
        }

        if (!component.BaseLayers.TryGetValue(bodyLayer, out var baseLayer) || !baseLayer.AllowsMarkings)
        {
            Log.Info($"Humanoid marking skipped for {ToPrettyString(uid)}: species={component.Species}, marking={prototype.ID}, layer={bodyLayer}, reason=base-layer-missing-or-disallowed.");
            return;
        }

        if (!_sprite.LayerMapTryGet((uid, sprite), bodyLayer, out var targetLayer, false))
        {
            Log.Info($"Humanoid marking skipped for {ToPrettyString(uid)}: species={component.Species}, marking={prototype.ID}, layer={bodyLayer}, reason=target-layer-missing.");
            return;
        }

        var offset = layerOffsets.GetValueOrDefault(bodyLayer);

        for (var i = 0; i < prototype.Sprites.Count; i++)
        {
            if (prototype.Sprites[i] is not SpriteSpecifier.Rsi rsi)
                continue;

            var layerId = $"{prototype.ID}-{rsi.RsiState}";
            if (_sprite.LayerMapTryGet((uid, sprite), layerId, out _, false))
                continue;

            var addedLayer = _sprite.AddLayer((uid, sprite), prototype.Sprites[i], targetLayer + offset + 1);
            _sprite.LayerMapSet((uid, sprite), layerId, addedLayer);
            _sprite.LayerSetVisible((uid, sprite), layerId, !component.HiddenMarkings.Contains(prototype.ID));

            var color = i < marking.MarkingColors.Count ? marking.MarkingColors[i] : Color.White;
            _sprite.LayerSetColor((uid, sprite), layerId, color);

            component.ClientMarkingLayers.Add(layerId);
            offset++;

            Log.Info($"Humanoid marking applied for {ToPrettyString(uid)}: species={component.Species}, marking={prototype.ID}, layer={bodyLayer}, state={rsi.RsiState}, layerId={layerId}, targetLayer={targetLayer}.");
        }

        layerOffsets[bodyLayer] = offset;
    }

    private void UpdateLayers(EntityUid uid, HumanoidAppearanceComponent component, SpriteComponent sprite)
    {
        var oldLayers = new HashSet<HumanoidVisualLayers>(component.BaseLayers.Keys);
        component.BaseLayers.Clear();

        if (_prototypeManager.TryIndex(component.Species, out SpeciesPrototype? speciesProto) &&
            speciesProto.SpriteSet is { } spriteSetId &&
            _prototypeManager.TryIndex(spriteSetId, out HumanoidSpeciesBaseSpritesPrototype? baseSprites))
        {
            foreach (var (layer, id) in baseSprites.Sprites)
            {
                oldLayers.Remove(layer);
                if (!component.CustomBaseLayers.ContainsKey(layer))
                    SetLayerData(uid, component, sprite, layer, id, sexMorph: true);
            }
        }

        foreach (var (layer, info) in component.CustomBaseLayers)
        {
            oldLayers.Remove(layer);
            SetLayerData(uid, component, sprite, layer, info.Id, sexMorph: false, color: info.Color, overrideSkin: true);
        }

        foreach (var layer in oldLayers)
        {
            if (_sprite.LayerMapTryGet((uid, sprite), layer, out var index, false))
                _sprite.LayerSetVisible((uid, sprite), index, false);
        }
    }

    private void SetLayerData(
        EntityUid uid,
        HumanoidAppearanceComponent component,
        SpriteComponent sprite,
        HumanoidVisualLayers layer,
        string? protoId,
        bool sexMorph = false,
        Color? color = null,
        bool overrideSkin = false)
    {
        var layerIndex = _sprite.LayerMapReserve((uid, sprite), layer);
        _sprite.LayerSetVisible((uid, sprite), layerIndex, !IsHidden(component, layer));

        if (color is { } explicitColor)
            _sprite.LayerSetColor((uid, sprite), layerIndex, explicitColor);

        if (string.IsNullOrWhiteSpace(protoId))
            return;

        if (sexMorph)
            protoId = HumanoidVisualLayersExtension.GetSexMorph(layer, component.Sex, protoId);

        if (!_prototypeManager.TryIndex(protoId, out HumanoidSpeciesSpriteLayer? proto))
            return;

        component.BaseLayers[layer] = proto;

        if (proto.MatchSkin && !overrideSkin)
            _sprite.LayerSetColor((uid, sprite), layerIndex, component.SkinColor.WithAlpha(proto.LayerAlpha));

        if (proto.BaseSprite != null)
            _sprite.LayerSetSprite((uid, sprite), layerIndex, proto.BaseSprite);
    }

    public override void SetLayerVisibility(
        Entity<HumanoidAppearanceComponent> ent,
        HumanoidVisualLayers layer,
        bool visible,
        SlotFlags? source,
        ref bool dirty)
    {
        base.SetLayerVisibility(ent, layer, visible, source, ref dirty);

        if (!TryComp(ent.Owner, out SpriteComponent? sprite))
            return;

        var index = _sprite.LayerMapReserve((ent.Owner, sprite), layer);
        _sprite.LayerSetVisible((ent.Owner, sprite), index, visible);
    }
}
