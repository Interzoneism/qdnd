using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using QDND.Combat.Actions;
using QDND.Data.Actions;
using QDND.Data.Items;
using QDND.Data.Stats;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Phase 10 parity regression: verifies that ActionRegistry loads cleanly and
    /// that every registered action satisfies structural invariants.
    /// </summary>
    public class ActionRegistryCoverageTests
    {
        // -------------------------------------------------------------------
        //  Helpers
        // -------------------------------------------------------------------

        private static string FindRepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "project.godot")))
                    return dir;
                dir = Directory.GetParent(dir)?.FullName;
            }
            // Fallback: walk up four levels from the test binary output folder
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        }

        private static ActionRegistry BuildRegistry()
        {
            var bg3DataPath = Path.Combine(FindRepoRoot(), "BG3_Data");
            var registry = new ActionRegistry();
            ActionRegistryInitializer.Initialize(registry, bg3DataPath, verboseLogging: false);
            return registry;
        }

        private static (ActionRegistry Registry, ItemDefinitionRegistry ItemDefinitions) BuildRegistryWithConsumables()
        {
            var bg3DataPath = Path.Combine(FindRepoRoot(), "BG3_Data");
            var registry = BuildRegistry();

            var statsRegistry = new StatsRegistry();
            string sharedPath = Path.Combine(bg3DataPath, "Shared", "Public", "Shared", "Stats", "Generated", "Data");
            string sharedDevPath = Path.Combine(bg3DataPath, "Shared", "Public", "SharedDev", "Stats", "Generated", "Data");
            statsRegistry.LoadFromDirectories(sharedPath, sharedDevPath);

            var itemDefinitions = new ItemDefinitionRegistry();
            itemDefinitions.Initialize(statsRegistry, registry);
            ActionRegistryInitializer.RegisterConsumableActions(registry, itemDefinitions, overwrite: true);

            return (registry, itemDefinitions);
        }

        private static (ActionRegistry Registry, ItemDefinitionRegistry ItemDefinitions) BuildConsumableRegistryFromObjects(
            ActionRegistry registry,
            params BG3ObjectData[] objects)
        {
            var statsRegistry = new StatsRegistry();
            foreach (var obj in objects)
            {
                statsRegistry.RegisterObject(obj);
            }

            var itemDefinitions = new ItemDefinitionRegistry();
            itemDefinitions.Initialize(statsRegistry, registry);
            ActionRegistryInitializer.RegisterConsumableActions(registry, itemDefinitions, overwrite: true);

            return (registry, itemDefinitions);
        }

        private static BG3ObjectData CreatePotionObject(string name, string useCosts = null)
        {
            return new BG3ObjectData
            {
                Name = name,
                ItemUseType = "Potion",
                UseCosts = useCosts,
            };
        }

        private static BG3ObjectData CreateScrollObject(string name)
        {
            return new BG3ObjectData
            {
                Name = name,
                ItemUseType = "Scroll",
            };
        }

        // -------------------------------------------------------------------
        //  Tests
        // -------------------------------------------------------------------

        [Fact]
        public void LoadAllSpells_ProducesZeroErrors()
        {
            var bg3DataPath = Path.Combine(FindRepoRoot(), "BG3_Data");
            var registry = new ActionRegistry();
            var result = ActionRegistryInitializer.Initialize(registry, bg3DataPath, verboseLogging: false);

            Assert.True(result.Success, $"Initialization failed: {result.ErrorMessage}");
            Assert.Equal(0, result.ErrorCount);
            Assert.Empty(registry.Errors);
        }

        [Fact]
        public void AllActions_HaveNonEmptyId()
        {
            var registry = BuildRegistry();
            var actions = registry.GetAllActions();

            Assert.NotEmpty(actions);
            Assert.All(actions, a =>
                Assert.False(string.IsNullOrEmpty(a.Id),
                    $"Action '{a.Name}' has a null/empty Id"));
        }

        [Fact]
        public void AllActions_HaveNonEmptyName()
        {
            var registry = BuildRegistry();
            var actions = registry.GetAllActions();

            Assert.NotEmpty(actions);
            Assert.All(actions, a =>
                Assert.False(string.IsNullOrEmpty(a.Name),
                    $"Action '{a.Id}' has a null/empty Name"));
        }

        [Fact]
        public void AllActions_HaveValidSpellLevel()
        {
            var registry = BuildRegistry();
            var actions = registry.GetAllActions();

            Assert.NotEmpty(actions);
            Assert.All(actions, a =>
                Assert.True(a.SpellLevel >= 0,
                    $"Action '{a.Id}' has negative SpellLevel: {a.SpellLevel}"));
        }

        [Fact]
        public void GetConcentrationActions_AllFlagConcentration()
        {
            var registry = BuildRegistry();
            var concentrationActions = registry.GetConcentrationActions();

            // Every action returned by GetConcentrationActions must have the flag set
            Assert.All(concentrationActions, a =>
                Assert.True(a.RequiresConcentration,
                    $"Action '{a.Id}' returned by GetConcentrationActions but RequiresConcentration is false"));
        }

        [Fact]
        public void HealingPotionAction_RemovesBurning()
        {
            var (registry, _) = BuildRegistryWithConsumables();
            var action = registry.GetAction("use_potion_healing");

            Assert.NotNull(action);
            Assert.Equal(TargetType.SingleUnit, action.TargetType);
            Assert.True(action.TargetFilter.HasFlag(TargetFilter.Self));
            Assert.True(action.TargetFilter.HasFlag(TargetFilter.Allies));
            Assert.Equal(1.5f, action.Range);
            Assert.Contains(action.Effects, e =>
                string.Equals(e.Type, "heal", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.DiceFormula, "2d4+2", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(action.Effects, e =>
                string.Equals(e.Type, "remove_status", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.StatusId, "BURNING", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ScrollAction_PropagatesConcentrationFromLinkedSpell()
        {
            var (registry, itemDefinitions) = BuildRegistryWithConsumables();

            var concentrationScroll = itemDefinitions
                .GetAllConsumables()
                .Where(i => i.UseCategory == ItemUseCategory.Scroll)
                .FirstOrDefault(i =>
                    !string.IsNullOrWhiteSpace(i.LinkedSpellId) &&
                    registry.GetAction(i.LinkedSpellId)?.RequiresConcentration == true);

            Assert.NotNull(concentrationScroll);

            var linkedSpell = registry.GetAction(concentrationScroll.LinkedSpellId);
            var scrollAction = registry.GetAction(concentrationScroll.UseActionId);

            Assert.NotNull(linkedSpell);
            Assert.NotNull(scrollAction);
            Assert.True(linkedSpell.RequiresConcentration);
            Assert.Equal(linkedSpell.RequiresConcentration, scrollAction.RequiresConcentration);
            Assert.Equal(linkedSpell.ConcentrationStatusId, scrollAction.ConcentrationStatusId);
        }

        [Theory]
        [InlineData("BonusActionPoint:1", false, true, false)]
        [InlineData("ActionPoint:1", true, false, false)]
        [InlineData("", true, false, false)]
        [InlineData("BonusActionPoint:1;ActionPoint:1", true, true, false)]
        public void ConsumableAction_ParseUseCosts_MapsExpectedActionBudget(
            string useCosts,
            bool expectedUsesAction,
            bool expectedUsesBonusAction,
            bool expectedUsesReaction)
        {
            string objectId = $"OBJ_Potion_CostProbe_{Guid.NewGuid():N}";
            var (registry, itemDefinitions) = BuildConsumableRegistryFromObjects(
                new ActionRegistry(),
                CreatePotionObject(objectId, useCosts));

            var definition = itemDefinitions.GetDefinition(objectId);
            Assert.NotNull(definition);

            var action = registry.GetAction(definition.UseActionId);
            Assert.NotNull(action);
            Assert.NotNull(action.Cost);
            Assert.Equal(expectedUsesAction, action.Cost.UsesAction);
            Assert.Equal(expectedUsesBonusAction, action.Cost.UsesBonusAction);
            Assert.Equal(expectedUsesReaction, action.Cost.UsesReaction);
        }

        [Fact]
        public void ScrollAction_IncludesSpellTag()
        {
            var registry = new ActionRegistry();
            registry.RegisterAction(new ActionDefinition
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

            var (resolvedRegistry, itemDefinitions) = BuildConsumableRegistryFromObjects(
                registry,
                CreateScrollObject("OBJ_Scroll_AcidArrow"));

            var scrollDefinition = itemDefinitions.GetDefinition("OBJ_Scroll_AcidArrow");
            Assert.NotNull(scrollDefinition);

            var action = resolvedRegistry.GetAction(scrollDefinition.UseActionId);
            Assert.NotNull(action);
            Assert.Contains("spell", action.Tags, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("scroll", action.Tags, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void StatusPotionAction_HasSelfTargetType()
        {
            var (registry, itemDefinitions) = BuildConsumableRegistryFromObjects(
                new ActionRegistry(),
                CreatePotionObject("OBJ_Potion_Of_Speed", "BonusActionPoint:1"));

            var definition = itemDefinitions.GetDefinition("OBJ_Potion_Of_Speed");
            Assert.NotNull(definition);
            Assert.Equal("POTION_OF_SPEED", definition.LinkedStatusId);

            var action = registry.GetAction(definition.UseActionId);
            Assert.NotNull(action);
            Assert.Equal(TargetType.Self, action.TargetType);
            Assert.Equal(TargetFilter.Self, action.TargetFilter);
            Assert.Contains(action.Effects, e =>
                string.Equals(e.Type, "apply_status", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.StatusId, "POTION_OF_SPEED", StringComparison.OrdinalIgnoreCase));
        }
    }
}
