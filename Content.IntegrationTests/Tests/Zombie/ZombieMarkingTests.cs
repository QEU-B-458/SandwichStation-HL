using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Zombies;
using Content.Shared.Humanoid;
using Content.Shared.Zombies;

namespace Content.IntegrationTests.Tests.Zombie;

[TestOf(typeof(ZombieSystem))]
public sealed class ZombieAppearanceTests : InteractionTest
{
    protected override string PlayerPrototype => "MobVulpkanin";

    [Test]
    public async Task AppearanceApplication()
    {
        await Server.WaitAssertion(() =>
        {
            var zombie = SEntMan.System<ZombieSystem>();
            var humanoidSystem = SEntMan.System<SharedHumanoidAppearanceSystem>();
            zombie.ZombifyEntity(SPlayer);
            var comp = SEntMan.GetComponent<ZombieComponent>(SPlayer);

            var appearance = humanoidSystem.GetCharacterAppearance(SPlayer);
            Assert.That(appearance, Is.Not.Null, $"Failed to get appearance for {SEntMan.ToPrettyString(SPlayer):SPlayer}");
            Assert.That(appearance!.SkinColor, Is.EqualTo(comp.SkinColor), "Zombified skin color mismatch");
            Assert.That(appearance.EyeColor, Is.EqualTo(comp.EyeColor), "Zombified eye color mismatch");
        });
    }

    [Test]
    public async Task MarkingApplication()
    {
        await Server.WaitAssertion(() =>
        {
            var humanoidSystem = SEntMan.System<SharedHumanoidAppearanceSystem>();

            var preAppearance = humanoidSystem.GetCharacterAppearance(SPlayer);
            Assert.That(preAppearance, Is.Not.Null, $"Failed to get pre-zombie appearance for {SEntMan.ToPrettyString(SPlayer):SPlayer}");

            var zombie = SEntMan.System<ZombieSystem>();
            zombie.ZombifyEntity(SPlayer);
            var comp = SEntMan.GetComponent<ZombieComponent>(SPlayer);

            var postAppearance = humanoidSystem.GetCharacterAppearance(SPlayer);
            Assert.That(postAppearance, Is.Not.Null, $"Failed to get post-zombie appearance for {SEntMan.ToPrettyString(SPlayer):SPlayer}");

            foreach (var (organ, layers) in postAppearance!.Markings)
            {
                Assert.That(preAppearance!.Markings, Does.ContainKey(organ), "Zombification added organs (it shouldn't)");
                Assert.That(preAppearance.Markings[organ], Is.Not.SameAs(layers), "Zombification shouldn't mutate the existing data structures");

                foreach (var (layer, markingSet) in layers)
                {
                    Assert.That(preAppearance.Markings[organ], Does.ContainKey(layer), "Zombification added layers (it shouldn't)");
                    Assert.That(preAppearance.Markings[organ][layer], Is.Not.SameAs(markingSet), "Zombification shouldn't mutate the existing data structures");
                    Assert.That(preAppearance.Markings[organ][layer], Has.Count.EqualTo(markingSet.Count), "Zombification shouldn't change the amount of markings");

                    if (!ZombieSystem.AdditionalZombieLayers.Contains(layer))
                        continue;

                    foreach (var (preMarking, postMarking) in preAppearance.Markings[organ][layer].Zip(markingSet))
                    {
                        Assert.That(preMarking, Is.Not.EqualTo(postMarking), $"Zombification should change marking {postMarking.MarkingId} on layer {layer}");
                        foreach (var color in postMarking.MarkingColors)
                        {
                            Assert.That(color, Is.EqualTo(comp.SkinColor), $"Zombification should change {postMarking.MarkingId} on layer {layer} to the skin color");
                        }
                    }
                }
            }
        });
    }
}
