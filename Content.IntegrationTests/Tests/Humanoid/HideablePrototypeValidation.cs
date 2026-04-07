using System.Collections.Generic;
using System.Linq;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Humanoid;

[TestFixture]
public sealed class HideLayerClothingValidation
{
    [Test]
    public async Task NoDeprecatedSlotsUsage()
    {
        await using var pair = await PoolManager.GetServerClient();

        var deprecated = new List<EntProtoId>();
        foreach (var (proto, component) in pair.GetPrototypesWithComponent<HideLayerClothingComponent>())
        {
#pragma warning disable CS0618
            if (component.Slots is { Count: > 0 } && component.Layers.Count == 0)
                deprecated.Add(proto.ID);
#pragma warning restore CS0618
        }

        Assert.That(deprecated, Is.Empty,
            $"Clothing prototypes using deprecated Slots field instead of Layers: {string.Join(", ", deprecated.Select(it => it.Id))}");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NoNoneSlotFlags()
    {
        await using var pair = await PoolManager.GetServerClient();

        var invalid = new List<string>();
        foreach (var (proto, component) in pair.GetPrototypesWithComponent<HideLayerClothingComponent>())
        {
            foreach (var (layer, slot) in component.Layers)
            {
                if (slot == SlotFlags.NONE)
                    invalid.Add($"{proto.ID}.{layer}");
            }
        }

        Assert.That(invalid, Is.Empty,
            $"HideLayerClothing entries with NONE slot flags: {string.Join(", ", invalid)}");

        await pair.CleanReturnAsync();
    }
}
