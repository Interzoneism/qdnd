using System.Collections.Generic;
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
    }
}
