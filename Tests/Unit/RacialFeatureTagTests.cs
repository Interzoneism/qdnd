using System.Collections.Generic;
using System.Linq;
using Xunit;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests that racial/class feature Tags are preserved through CharacterResolver so
    /// ScenarioLoader can copy them onto the Combatant.
    /// </summary>
    public class RacialFeatureTagTests
    {
        private static CharacterDataRegistry BuildMinimalRegistry(RaceDefinition race = null, ClassDefinition cls = null)
        {
            var registry = new CharacterDataRegistry();

            if (race != null)
                registry.RegisterRace(race);

            if (cls != null)
                registry.RegisterClass(cls);
            else
                registry.RegisterClass(new ClassDefinition
                {
                    Id = "fighter",
                    Name = "Fighter",
                    HitDie = 10,
                    LevelTable = new Dictionary<string, LevelProgression>
                    {
                        ["1"] = new LevelProgression()
                    }
                });

            return registry;
        }

        [Fact]
        public void ResolvedFeatures_ContainRacialFeatureTags()
        {
            var luckyFeature = new Feature
            {
                Id = "lucky",
                Name = "Lucky",
                IsPassive = true,
                Source = "Halfling",
                Tags = new List<string> { "lucky_reroll" }
            };

            var race = new RaceDefinition
            {
                Id = "halfling",
                Name = "Halfling",
                Speed = 25,
                Features = new List<Feature> { luckyFeature }
            };

            var registry = BuildMinimalRegistry(race: race);
            var resolver = new CharacterResolver(registry);

            var sheet = new CharacterSheet
            {
                Name = "TestHalfling",
                RaceId = "halfling",
                ClassLevels = new List<ClassLevel> { new ClassLevel("fighter") }
            };

            var resolved = resolver.Resolve(sheet);

            // The resolved Features list must include the Lucky feature
            Assert.Contains(resolved.Features, f => f.Id == "lucky");

            // The Lucky feature must have the tag
            var lucky = resolved.Features.First(f => f.Id == "lucky");
            Assert.NotNull(lucky.Tags);
            Assert.Contains("lucky_reroll", lucky.Tags);
        }

        [Fact]
        public void ResolvedFeatures_FeyAncestry_HasAdvantageTag()
        {
            var feyAncestry = new Feature
            {
                Id = "fey_ancestry",
                Name = "Fey Ancestry",
                IsPassive = true,
                Source = "Elf",
                ConditionImmunities = new List<string> { "Sleep" },
                Tags = new List<string> { "advantage_vs_charmed" }
            };

            var race = new RaceDefinition
            {
                Id = "elf",
                Name = "Elf",
                Speed = 30,
                Features = new List<Feature> { feyAncestry }
            };

            var registry = BuildMinimalRegistry(race: race);
            var resolver = new CharacterResolver(registry);

            var sheet = new CharacterSheet
            {
                Name = "TestElf",
                RaceId = "elf",
                ClassLevels = new List<ClassLevel> { new ClassLevel("fighter") }
            };

            var resolved = resolver.Resolve(sheet);

            var feature = resolved.Features.FirstOrDefault(f => f.Id == "fey_ancestry");
            Assert.NotNull(feature);
            Assert.Contains("advantage_vs_charmed", feature.Tags);
        }

        [Fact]
        public void CombatantTags_TagsFromFeaturesFlow_WhenCopied()
        {
            // Simulate what ScenarioLoader does when copying feature tags to combatant.Tags
            var features = new List<Feature>
            {
                new Feature { Id = "lucky",       Tags = new List<string> { "lucky_reroll" } },
                new Feature { Id = "brave",        Tags = new List<string> { "advantage_vs_frightened" } },
                new Feature { Id = "proficiency",  Tags = null }  // should not crash
            };

            var combatantTags = new List<string> { "melee" }; // pre-existing tag

            // This mirrors the ScenarioLoader logic
            foreach (var feat in features)
            {
                if (feat.Tags == null) continue;
                foreach (var tag in feat.Tags)
                {
                    if (!combatantTags.Contains(tag))
                        combatantTags.Add(tag);
                }
            }

            Assert.Contains("melee", combatantTags);
            Assert.Contains("lucky_reroll", combatantTags);
            Assert.Contains("advantage_vs_frightened", combatantTags);
            Assert.Equal(3, combatantTags.Count); // no duplicates, no nulls
        }
    }
}
