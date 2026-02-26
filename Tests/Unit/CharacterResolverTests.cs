using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Data;
using Xunit;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Unit
{
    public class CharacterResolverTests
    {
        [Fact]
        public void Resolve_CollectsLevelResourcesAndSpellSlots()
        {
            // Arrange
            var registry = new CharacterDataRegistry();
            registry.RegisterClass(new ClassDefinition
            {
                Id = "wizard",
                Name = "Wizard",
                HitDie = 6,
                LevelTable = new Dictionary<string, LevelProgression>
                {
                    ["1"] = new LevelProgression
                    {
                        SpellSlots = new Dictionary<string, int>
                        {
                            ["1"] = 2
                        }
                    },
                    ["2"] = new LevelProgression
                    {
                        Resources = new Dictionary<string, int>
                        {
                            ["arcane_recovery"] = 1
                        },
                        SpellSlots = new Dictionary<string, int>
                        {
                            ["1"] = 3
                        }
                    },
                    ["3"] = new LevelProgression
                    {
                        SpellSlots = new Dictionary<string, int>
                        {
                            ["1"] = 4,
                            ["2"] = 2
                        }
                    }
                }
            });

            var sheet = new CharacterSheet
            {
                Name = "Test Wizard",
                ClassLevels = new List<ClassLevel>
                {
                    new ClassLevel("wizard"),
                    new ClassLevel("wizard"),
                    new ClassLevel("wizard")
                }
            };

            var resolver = new CharacterResolver(registry);

            // Act
            var resolved = resolver.Resolve(sheet);

            // Assert
            Assert.Equal(1, resolved.Resources["arcane_recovery"]);
            Assert.Equal(4, resolved.Resources["spell_slot_1"]);
            Assert.Equal(2, resolved.Resources["spell_slot_2"]);
        }

        [Fact]
        public void Resolve_WarlockSpellSlots_MapToPactResources()
        {
            // Arrange
            var registry = new CharacterDataRegistry();
            registry.RegisterClass(new ClassDefinition
            {
                Id = "warlock",
                Name = "Warlock",
                HitDie = 8,
                LevelTable = new Dictionary<string, LevelProgression>
                {
                    ["1"] = new LevelProgression
                    {
                        SpellSlots = new Dictionary<string, int>
                        {
                            ["1"] = 1
                        }
                    },
                    ["2"] = new LevelProgression
                    {
                        SpellSlots = new Dictionary<string, int>
                        {
                            ["1"] = 2
                        }
                    },
                    ["3"] = new LevelProgression
                    {
                        SpellSlots = new Dictionary<string, int>
                        {
                            ["2"] = 2
                        }
                    }
                }
            });

            var sheet = new CharacterSheet
            {
                Name = "Test Warlock",
                ClassLevels = new List<ClassLevel>
                {
                    new ClassLevel("warlock"),
                    new ClassLevel("warlock"),
                    new ClassLevel("warlock")
                }
            };

            var resolver = new CharacterResolver(registry);

            // Act
            var resolved = resolver.Resolve(sheet);

            // Assert
            Assert.Equal(2, resolved.Resources["spell_slot_2"]);
            Assert.Equal(2, resolved.Resources["pact_slots"]);
            Assert.Equal(2, resolved.Resources["pact_slot_level"]);
        }

        [Fact]
        public void Resolve_Multiclass_EldritchKnightContributesThirdCasterLevels()
        {
            var registry = new CharacterDataRegistry();
            registry.RegisterClass(new ClassDefinition
            {
                Id = "fighter",
                Name = "Fighter",
                HitDie = 10,
                SpellcasterModifier = 0,
                Subclasses = new List<SubclassDefinition>
                {
                    new SubclassDefinition
                    {
                        Id = "eldritch_knight",
                        Name = "Eldritch Knight",
                        SpellcasterModifier = 0.3333
                    }
                }
            });
            registry.RegisterClass(new ClassDefinition
            {
                Id = "wizard",
                Name = "Wizard",
                HitDie = 6,
                SpellcasterModifier = 1.0
            });

            var classLevels = new List<ClassLevel>();
            for (int i = 0; i < 6; i++)
                classLevels.Add(new ClassLevel("fighter", "eldritch_knight"));
            for (int i = 0; i < 6; i++)
                classLevels.Add(new ClassLevel("wizard"));

            var sheet = new CharacterSheet
            {
                Name = "EK Wizard",
                ClassLevels = classLevels
            };

            var resolver = new CharacterResolver(registry);
            var resolved = resolver.Resolve(sheet);

            Assert.Equal(4, resolved.Resources["spell_slot_1"]);
            Assert.Equal(3, resolved.Resources["spell_slot_2"]);
            Assert.Equal(3, resolved.Resources["spell_slot_3"]);
            Assert.Equal(2, resolved.Resources["spell_slot_4"]);
        }

        [Fact]
        public void Resolve_WarlockInvocations_GrantsSelectedInvocationFeatures()
        {
            var registry = new CharacterDataRegistry();
            registry.RegisterClass(new ClassDefinition
            {
                Id = "warlock",
                Name = "Warlock",
                HitDie = 8,
                LevelTable = new Dictionary<string, LevelProgression>
                {
                    ["2"] = new LevelProgression
                    {
                        Resources = new Dictionary<string, int>
                        {
                            ["invocations_known"] = 2
                        }
                    }
                }
            });
            registry.RegisterFeat(new FeatDefinition
            {
                Id = "agonizing_blast",
                Name = "Agonizing Blast",
                Features = new List<Feature>
                {
                    new Feature
                    {
                        Id = "agonizing_blast",
                        Name = "Agonizing Blast",
                        IsPassive = true
                    }
                }
            });

            var sheet = new CharacterSheet
            {
                Name = "Invocation Warlock",
                ClassLevels = new List<ClassLevel>
                {
                    new ClassLevel("warlock"),
                    new ClassLevel("warlock")
                },
                InvocationIds = new List<string> { "agonizing_blast" }
            };

            var resolver = new CharacterResolver(registry);
            var resolved = resolver.Resolve(sheet);

            Assert.Contains(resolved.Features, f => f.Id == "agonizing_blast");
        }

        [Fact]
        public void GenerateRandomUnit_SorcererLevel10_SelectsMetamagicPerLevelMilestones()
        {
            var registry = CreateRegistryWithBaseRace();
            registry.RegisterClass(new ClassDefinition
            {
                Id = "sorcerer",
                Name = "Sorcerer",
                HitDie = 6,
                PrimaryAbility = "Charisma",
                SpellcastingAbility = "Charisma",
                FeatLevels = new List<int>(),
                LevelTable = new Dictionary<string, LevelProgression>
                {
                    ["1"] = new LevelProgression(),
                    ["2"] = new LevelProgression
                    {
                        Features = new List<Feature> { new() { Id = "metamagic" } }
                    },
                    ["3"] = new LevelProgression
                    {
                        Features = new List<Feature> { new() { Id = "metamagic_3" } }
                    },
                    ["10"] = new LevelProgression
                    {
                        Features = new List<Feature> { new() { Id = "metamagic_10" } }
                    }
                }
            });

            var generator = new ScenarioGenerator(registry, seed: 1337);
            var unit = generator.GenerateRandomUnit(new CharacterGenerationOptions
            {
                UnitId = "unit_1",
                Faction = Faction.Player,
                Level = 10,
                ForcedRaceId = "test_race",
                ForcedClassId = "sorcerer"
            });

            Assert.NotNull(unit.MetamagicIds);
            Assert.Equal(4, unit.MetamagicIds.Count);
            Assert.Equal(4, unit.MetamagicIds.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void GenerateRandomUnit_WarlockLevel5_SelectsInvocationsFromProgressionCount()
        {
            var registry = CreateRegistryWithBaseRace();
            registry.RegisterClass(new ClassDefinition
            {
                Id = "warlock",
                Name = "Warlock",
                HitDie = 8,
                PrimaryAbility = "Charisma",
                SpellcastingAbility = "Charisma",
                FeatLevels = new List<int>(),
                LevelTable = new Dictionary<string, LevelProgression>
                {
                    ["1"] = new LevelProgression(),
                    ["2"] = new LevelProgression { InvocationsKnown = 2 },
                    ["5"] = new LevelProgression { InvocationsKnown = 3 }
                }
            });

            RegisterInvocationFeat(registry, "agonizing_blast");
            RegisterInvocationFeat(registry, "repelling_blast");
            RegisterInvocationFeat(registry, "devils_sight");

            var generator = new ScenarioGenerator(registry, seed: 2026);
            var unit = generator.GenerateRandomUnit(new CharacterGenerationOptions
            {
                UnitId = "unit_1",
                Faction = Faction.Player,
                Level = 5,
                ForcedRaceId = "test_race",
                ForcedClassId = "warlock"
            });

            Assert.NotNull(unit.InvocationIds);
            Assert.Equal(3, unit.InvocationIds.Count);
            Assert.Equal(3, unit.InvocationIds.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(unit.InvocationIds, id => Assert.Contains(id, new[] { "agonizing_blast", "repelling_blast", "devils_sight" }));
        }

        [Fact]
        public void GenerateRandomUnit_FeatWithChoice_PopulatesFeatChoices()
        {
            var registry = CreateRegistryWithBaseRace(
                new Feature
                {
                    Id = "test_race_ability_boost",
                    AbilityScoreIncreases = new Dictionary<string, int>
                    {
                        ["Strength"] = 20,
                        ["Constitution"] = 20
                    }
                });

            registry.RegisterClass(new ClassDefinition
            {
                Id = "fighter",
                Name = "Fighter",
                HitDie = 10,
                PrimaryAbility = "Strength",
                FeatLevels = new List<int> { 4 },
                LevelTable = new Dictionary<string, LevelProgression>
                {
                    ["1"] = new LevelProgression(),
                    ["2"] = new LevelProgression(),
                    ["3"] = new LevelProgression(),
                    ["4"] = new LevelProgression()
                }
            });

            registry.RegisterFeat(new FeatDefinition
            {
                Id = "resilient",
                Name = "Resilient",
                Features = new List<Feature> { new() { Id = "resilient_feature" } }
            });

            var generator = new ScenarioGenerator(registry, seed: 77);
            var unit = generator.GenerateRandomUnit(new CharacterGenerationOptions
            {
                UnitId = "unit_1",
                Faction = Faction.Player,
                Level = 4,
                ForcedRaceId = "test_race",
                ForcedClassId = "fighter"
            });

            Assert.Contains(unit.FeatIds, id => string.Equals(id, "resilient", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(unit.FeatChoices, kvp => string.Equals(kvp.Key, "resilient", StringComparison.OrdinalIgnoreCase));
            var resilientChoices = unit.FeatChoices.First(kvp => string.Equals(kvp.Key, "resilient", StringComparison.OrdinalIgnoreCase)).Value;
            Assert.Contains("ability", resilientChoices.Keys);
            var chosenAbility = resilientChoices["ability"];
            Assert.False(string.IsNullOrWhiteSpace(chosenAbility));
        }

        private static CharacterDataRegistry CreateRegistryWithBaseRace(params Feature[] extraRaceFeatures)
        {
            var registry = new CharacterDataRegistry();
            var features = new List<Feature>();
            if (extraRaceFeatures != null)
            {
                features.AddRange(extraRaceFeatures.Where(feature => feature != null));
            }

            registry.RegisterRace(new RaceDefinition
            {
                Id = "test_race",
                Name = "Test Race",
                Features = features
            });

            return registry;
        }

        private static void RegisterInvocationFeat(CharacterDataRegistry registry, string id)
        {
            registry.RegisterFeat(new FeatDefinition
            {
                Id = id,
                Name = id,
                Features = new List<Feature>
                {
                    new()
                    {
                        Id = id,
                        Tags = new List<string> { "invocation" },
                        IsPassive = true
                    }
                }
            });
        }
    }
}
