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
    }
}
