using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Robust.Shared.Enums;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Humanoid;

[NetworkedComponent, RegisterComponent, AutoGenerateComponentState(true)]
public sealed partial class HumanoidAppearanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public MarkingSet MarkingSet = new();

    [DataField]
    public Dictionary<HumanoidVisualLayers, HumanoidSpeciesSpriteLayer> BaseLayers = new();

    [DataField, AutoNetworkedField]
    public HashSet<HumanoidVisualLayers> PermanentlyHidden = new();

    [DataField, AutoNetworkedField]
    public HashSet<string> HiddenMarkings = new();

    [DataField, AutoNetworkedField]
    public Gender Gender;

    [DataField, AutoNetworkedField]
    public int Age = 18;

    [DataField, AutoNetworkedField]
    public Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> CustomBaseLayers = new();

    [DataField, AutoNetworkedField]
    public ProtoId<SpeciesPrototype> Species { get; set; } = "Human";

    [DataField, AutoNetworkedField]
    public string? CustomSpecies { get; set; }

    [DataField]
    public ProtoId<HumanoidProfilePrototype>? Initial { get; private set; }

    [DataField, AutoNetworkedField]
    public Color SkinColor { get; set; } = Color.FromHex("#C0967F");

    [DataField, AutoNetworkedField]
    public Dictionary<HumanoidVisualLayers, SlotFlags> HiddenLayers = new();

    [DataField, AutoNetworkedField]
    public Sex Sex = Sex.Male;

    [DataField, AutoNetworkedField]
    public Color EyeColor = Color.Brown;

    /// <summary>
    /// Client-only bookkeeping for dynamically added marking layers.
    /// </summary>
    public HashSet<string> ClientMarkingLayers = new();
}

[DataDefinition]
[Serializable, NetSerializable]
public readonly partial record struct CustomBaseLayerInfo
{
    [DataField]
    public ProtoId<HumanoidSpeciesSpriteLayer>? Id { get; init; }

    [DataField]
    public Color? Color { get; init; }

    public CustomBaseLayerInfo(ProtoId<HumanoidSpeciesSpriteLayer>? id, Color? color = null)
    {
        Id = id;
        Color = color;
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class MarkingSet
{
    [DataField]
    public Dictionary<MarkingCategories, List<Marking>> Markings = new();

    public void AddBack(MarkingCategories category, Marking marking)
    {
        if (!Markings.TryGetValue(category, out var list))
        {
            list = new List<Marking>();
            Markings[category] = list;
        }

        list.Add(marking);
    }
}
