using System;
using System.Collections.Generic;
using QDND.Combat.Actions;
using QDND.Data.Items;
using QDND.Data.Stats;
using Xunit;

namespace QDND.Tests.Unit
{
    public class ItemDefinitionRegistryTests
    {
        private static (ItemDefinitionRegistry Registry, StatsRegistry Stats, ActionRegistry Actions) BuildInitializedRegistry()
        {
            var stats = new StatsRegistry();
            stats.RegisterObject(new BG3ObjectData
            {
                Name = "_Potion_Template",
                ItemUseType = "Potion",
            });
            stats.RegisterObject(new BG3ObjectData
            {
                Name = "OBJ_Potion_Healing",
                ItemUseType = "Potion",
                UseCosts = "BonusActionPoint:1",
            });
            stats.RegisterObject(new BG3ObjectData
            {
                Name = "OBJ_Scroll_AcidArrow",
                ItemUseType = "Scroll",
                UseCosts = "ActionPoint:1",
            });

            var actions = new ActionRegistry();
            actions.RegisterAction(new ActionDefinition
            {
                Id = "acid_arrow",
                Name = "Acid Arrow",
                TargetType = TargetType.SingleUnit,
                TargetFilter = TargetFilter.Enemies,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", DiceFormula = "4d4", DamageType = "acid" }
                },
            });

            var registry = new ItemDefinitionRegistry();
            registry.Initialize(stats, actions);
            return (registry, stats, actions);
        }

        [Fact]
        public void Initialize_ResolvesConsumablesFromStatsRegistry()
        {
            var (registry, _, _) = BuildInitializedRegistry();

            Assert.True(registry.Count >= 2);

            var healingPotion = registry.GetDefinition("OBJ_Potion_Healing");
            Assert.NotNull(healingPotion);
            Assert.True(healingPotion.IsConsumable);
            Assert.Equal(ItemUseCategory.Potion, healingPotion.UseCategory);

            var scroll = registry.GetDefinition("OBJ_Scroll_AcidArrow");
            Assert.NotNull(scroll);
            Assert.True(scroll.IsConsumable);
            Assert.Equal(ItemUseCategory.Scroll, scroll.UseCategory);
            Assert.Equal("acid_arrow", scroll.LinkedSpellId);
        }

        [Fact]
        public void GetDefinition_ReturnsKnownBg3Object()
        {
            var (registry, _, _) = BuildInitializedRegistry();

            var definition = registry.GetDefinition("OBJ_Potion_Healing");

            Assert.NotNull(definition);
            Assert.Equal("OBJ_Potion_Healing", definition.Id);
            Assert.Equal("Potion of Healing", definition.DisplayName);
            Assert.Equal("2d4+2", definition.HealingFormula);
        }

        [Fact]
        public void GetByCategory_ReturnsPotions()
        {
            var (registry, _, _) = BuildInitializedRegistry();

            var potions = registry.GetByCategory(ItemUseCategory.Potion);

            Assert.NotEmpty(potions);
            Assert.Contains(potions, p => string.Equals(p.Id, "OBJ_Potion_Healing", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void GetByCategory_ReturnsScrolls()
        {
            var (registry, _, _) = BuildInitializedRegistry();

            var scrolls = registry.GetByCategory(ItemUseCategory.Scroll);

            Assert.NotEmpty(scrolls);
            Assert.Contains(scrolls, s => string.Equals(s.Id, "OBJ_Scroll_AcidArrow", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Initialize_SkipsTemplateEntries()
        {
            var (registry, _, _) = BuildInitializedRegistry();

            Assert.Null(registry.GetDefinition("_Potion_Template"));
            Assert.DoesNotContain(registry.GetAllConsumables(),
                d => d.Id.StartsWith("_", StringComparison.Ordinal));
        }

        [Fact]
        public void GetAllConsumables_ReturnsNonEmptyList()
        {
            var (registry, _, _) = BuildInitializedRegistry();

            var consumables = registry.GetAllConsumables();

            Assert.NotEmpty(consumables);
            Assert.All(consumables, c => Assert.True(c.IsConsumable));
            Assert.Contains(consumables, c => c.UseCategory == ItemUseCategory.Potion);
            Assert.Contains(consumables, c => c.UseCategory == ItemUseCategory.Scroll);
        }
    }
}
