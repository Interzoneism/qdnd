using System;
using QDND.Combat.Actions;
using QDND.Data.Items;
using QDND.Data.Stats;
using Xunit;

namespace QDND.Tests.Unit
{
    public class BG3ConsumableResolverTests
    {
        [Theory]
        [InlineData("OBJ_Potion_Healing", "2d4+2")]
        [InlineData("OBJ_Potion_Healing_Greater", "4d4+4")]
        [InlineData("OBJ_Potion_Healing_Superior", "8d4+8")]
        [InlineData("OBJ_Potion_Healing_Supreme", "10d4+20")]
        public void Resolve_HealingPotion_SetsExpectedHealingFormula(string objectId, string expectedFormula)
        {
            var resolver = new BG3ConsumableResolver(new ActionRegistry());
            var obj = new BG3ObjectData { Name = objectId, ItemUseType = "Potion" };

            var item = resolver.Resolve(obj, ItemUseCategory.Potion);

            Assert.NotNull(item);
            Assert.Equal(expectedFormula, item.HealingFormula);
        }

        [Theory]
        [InlineData("OBJ_Potion_Of_Speed", "POTION_OF_SPEED")]
        [InlineData("OBJ_Potion_Of_Hill_Giant_Strength", "POTION_OF_STRENGTH_HILL_GIANT")]
        public void Resolve_StatusPotion_SetsExpectedLinkedStatus(string objectId, string expectedStatusId)
        {
            var resolver = new BG3ConsumableResolver(new ActionRegistry());
            var obj = new BG3ObjectData { Name = objectId, ItemUseType = "Potion" };

            var item = resolver.Resolve(obj, ItemUseCategory.Potion);

            Assert.NotNull(item);
            Assert.Equal(expectedStatusId, item.LinkedStatusId);
        }

        [Fact]
        public void Resolve_ResistancePotion_SetsExpectedLinkedStatus()
        {
            var resolver = new BG3ConsumableResolver(new ActionRegistry());
            var obj = new BG3ObjectData
            {
                Name = "OBJ_Potion_Of_Resistance_Fire",
                ItemUseType = "Potion",
            };

            var item = resolver.Resolve(obj, ItemUseCategory.Potion);

            Assert.NotNull(item);
            Assert.Equal("POTION_OF_RESISTANCE_FIRE", item.LinkedStatusId);
        }

        [Fact]
        public void Resolve_ScrollWithLinkedSpell_SetsLinkedSpellId()
        {
            var actions = new ActionRegistry();
            actions.RegisterAction(new ActionDefinition
            {
                Id = "acid_arrow",
                Name = "Acid Arrow",
                TargetType = TargetType.SingleUnit,
                TargetFilter = TargetFilter.Enemies,
            });

            var resolver = new BG3ConsumableResolver(actions);
            var obj = new BG3ObjectData { Name = "OBJ_Scroll_AcidArrow", ItemUseType = "Scroll" };

            var item = resolver.Resolve(obj, ItemUseCategory.Scroll);

            Assert.NotNull(item);
            Assert.Equal("acid_arrow", item.LinkedSpellId);
            Assert.Equal("scroll_acid_arrow", item.UseActionId);
            Assert.Equal("Scroll: Acid Arrow", item.DisplayName);
        }

        [Fact]
        public void Resolve_ScrollMissingSpell_DoesNotCrashAndReturnsItem()
        {
            var resolver = new BG3ConsumableResolver(new ActionRegistry());
            var obj = new BG3ObjectData { Name = "OBJ_Scroll_MissingSpell", ItemUseType = "Scroll" };

            var item = resolver.Resolve(obj, ItemUseCategory.Scroll);

            Assert.NotNull(item);
            Assert.Null(item.LinkedSpellId);
            Assert.Equal("scroll_missing_spell", item.UseActionId);
            Assert.Equal("Casts a spell from this scroll", item.Description);
        }

        [Fact]
        public void Resolve_GeneratesExpectedDisplayNames()
        {
            var actions = new ActionRegistry();
            actions.RegisterAction(new ActionDefinition
            {
                Id = "acid_arrow",
                Name = "Acid Arrow",
            });

            var resolver = new BG3ConsumableResolver(actions);

            var potion = resolver.Resolve(
                new BG3ObjectData { Name = "OBJ_Potion_Healing", ItemUseType = "Potion" },
                ItemUseCategory.Potion);
            var scroll = resolver.Resolve(
                new BG3ObjectData { Name = "OBJ_Scroll_AcidArrow", ItemUseType = "Scroll" },
                ItemUseCategory.Scroll);

            Assert.NotNull(potion);
            Assert.NotNull(scroll);

            Assert.Equal("Potion of Healing", potion.DisplayName);
            Assert.Contains("Acid Arrow", scroll.DisplayName, StringComparison.OrdinalIgnoreCase);
            Assert.True(
                scroll.DisplayName.StartsWith("Scroll of", StringComparison.OrdinalIgnoreCase) ||
                scroll.DisplayName.StartsWith("Scroll:", StringComparison.OrdinalIgnoreCase));
        }
    }
}
