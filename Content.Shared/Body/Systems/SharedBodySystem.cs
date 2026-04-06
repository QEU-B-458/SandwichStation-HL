using Content.Shared.Body.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Analyzers;

namespace Content.Shared.Body.Systems;

[Virtual]
public partial class SharedBodySystem : EntitySystem
{
    public const string PartSlotContainerIdPrefix = "body_part_slot_";
    public const string BodyRootContainerId = "body_root_part";
    public const string OrganSlotContainerIdPrefix = "body_organ_slot_";

    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IPrototypeManager Prototypes = default!;
    [Dependency] protected readonly DamageableSystem Damageable = default!;
    [Dependency] protected readonly MovementSpeedModifierSystem Movement = default!;
    [Dependency] protected readonly SharedContainerSystem Containers = default!;
    [Dependency] protected readonly SharedTransformSystem SharedTransform = default!;
    [Dependency] protected readonly StandingStateSystem Standing = default!;
    [Dependency] protected readonly InventorySystem Inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeBody();
        InitializeParts();
        InitializeOrgans();
        InitializeIntegrityQueue();
        InitializePartAppearances();
    }

    protected static string? GetPartSlotContainerIdFromContainer(string containerSlotId)
    {
        var slotIndex = containerSlotId.IndexOf(PartSlotContainerIdPrefix, StringComparison.Ordinal);
        if (slotIndex < 0)
            return null;

        return containerSlotId.Remove(slotIndex, PartSlotContainerIdPrefix.Length);
    }

    public static string GetPartSlotContainerId(string slotId) => PartSlotContainerIdPrefix + slotId;
    public static string GetOrganContainerId(string slotId) => OrganSlotContainerIdPrefix + slotId;
}
