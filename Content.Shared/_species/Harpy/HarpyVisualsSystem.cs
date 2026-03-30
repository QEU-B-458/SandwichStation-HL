using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Shared._species.Harpy;

public sealed class HarpyVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedHideableHumanoidLayersSystem _hideableLayers = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HarpySingerComponent, DidEquipEvent>(OnDidEquipEvent);
        SubscribeLocalEvent<HarpySingerComponent, DidUnequipEvent>(OnDidUnequipEvent);
    }

    private void OnDidEquipEvent(EntityUid uid, HarpySingerComponent component, DidEquipEvent args)
    {
        if (args.Slot == "outerClothing" && HasComp<HarpyHideWingsComponent>(args.Equipment))
        {
            _hideableLayers.SetLayerOcclusion(uid, HumanoidVisualLayers.RArm, true, SlotFlags.OUTERCLOTHING);
            _hideableLayers.SetLayerOcclusion(uid, HumanoidVisualLayers.Tail, true, SlotFlags.OUTERCLOTHING);
        }
    }

    private void OnDidUnequipEvent(EntityUid uid, HarpySingerComponent component, DidUnequipEvent args)
    {
        if (args.Slot == "outerClothing" && HasComp<HarpyHideWingsComponent>(args.Equipment))
        {
            _hideableLayers.SetLayerOcclusion(uid, HumanoidVisualLayers.RArm, false, SlotFlags.OUTERCLOTHING);
            _hideableLayers.SetLayerOcclusion(uid, HumanoidVisualLayers.Tail, false, SlotFlags.OUTERCLOTHING);
        }
    }
}
