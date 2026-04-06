using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Body;

/// <summary>
/// Defines the coloration, sex, etc. of organs. Kept for compatibility with the marking modifier UI.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public partial record struct OrganProfileData
{
    [DataField]
    public Sex Sex;

    [DataField]
    public Color EyeColor = Color.White;

    [DataField]
    public Color SkinColor = Color.White;
}

/// <summary>
/// Defines the layers and group an organ takes markings for. Kept for compatibility with the marking modifier UI.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public partial record struct OrganMarkingData
{
    [DataField(required: true)]
    public HashSet<HumanoidVisualLayers> Layers = default!;

    [DataField(required: true)]
    public ProtoId<MarkingsGroupPrototype> Group = default!;
}
