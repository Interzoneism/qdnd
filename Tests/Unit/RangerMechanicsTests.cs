using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Actions;
using QDND.Combat.Statuses;
using QDND.Combat.Rules;
using QDND.Data.CharacterModel;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using QDND.Data.Actions;
using QDND.Data.Statuses;
using DataBG3StatusIntegration = QDND.Data.Statuses.BG3StatusIntegration;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Ranger mechanics smoke tests against BG3-loaded action/status data.
    /// </summary>
    public class RangerMechanicsTests
    {
        private static string FindRepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "project.godot")))
                    return dir;
                dir = Directory.GetParent(dir)?.FullName;
            }

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        }

        private static string GetBG3DataPath() => Path.Combine(FindRepoRoot(), "BG3_Data");

        private static string GetSharedStatsPath() => Path.Combine(GetBG3DataPath(), "Shared", "Public", "Shared", "Stats", "Generated", "Data");

        private static string GetSharedDevStatsPath() => Path.Combine(GetBG3DataPath(), "Shared", "Public", "SharedDev", "Stats", "Generated", "Data");

        private static ActionRegistry CreateLoadedRegistry()
        {
            var registry = new ActionRegistry();
            var init = ActionRegistryInitializer.Initialize(registry, GetBG3DataPath(), verboseLogging: false);
            Assert.True(init.Success, init.ErrorMessage ?? "Action registry initialization failed");
            return registry;
        }

        private static StatusManager CreateLoadedStatusManager(RulesEngine rulesEngine)
        {
            var statuses = new StatusManager(rulesEngine);
            var statusRegistry = new StatusRegistry();
            statusRegistry.LoadStatuses(GetSharedStatsPath(), GetSharedDevStatsPath());
            DataBG3StatusIntegration.RegisterBG3Statuses(statuses, statusRegistry.GetAllStatuses());
            return statuses;
        }

        private Combatant CreateCombatant(string id, int hp = 100, int initiative = 10, string team = "player")
        {
            var c = new Combatant(id, id, Faction.Player, hp, initiative) { Team = team };
            c.ResolvedCharacter = new ResolvedCharacter
            {
                AbilityScores = new Dictionary<AbilityType, int>
                {
                    { AbilityType.Strength, 14 }, { AbilityType.Dexterity, 16 }, { AbilityType.Constitution, 12 },
                    { AbilityType.Intelligence, 10 }, { AbilityType.Wisdom, 14 }, { AbilityType.Charisma, 8 }
                }
            };
            return c;
        }

        [Fact]
        public void EnsnaringStrike_Exists_InAbilityRegistry()
        {
            var registry = CreateLoadedRegistry();
            var action = registry.GetAction("ensnaring_strike");

            Assert.NotNull(action);
            Assert.True(action.RequiresConcentration);
        }

        [Fact]
        public void HailOfThorns_Exists_InAbilityRegistry()
        {
            var registry = CreateLoadedRegistry();
            var action = registry.GetAction("hail_of_thorns");

            Assert.NotNull(action);
            Assert.True(action.RequiresConcentration);
        }

        [Fact]
        public void PrimevalAwareness_Exists_InAbilityRegistry()
        {
            var registry = CreateLoadedRegistry();
            var action = registry.GetAction("primeval_awareness");

            Assert.NotNull(action);
            Assert.Equal("primeval_awareness", action.Id);
        }

        [Fact]
        public void HuntersMark_Exists_InAbilityRegistry()
        {
            var registry = CreateLoadedRegistry();
            var action = registry.GetAction("hunters_mark");

            Assert.NotNull(action);
            Assert.True(action.RequiresConcentration);
        }

        [Fact]
        public void RangerFeatureActions_LoadSuccessfully()
        {
            var registry = CreateLoadedRegistry();
            var rangerAbilities = new[]
            {
                "hunters_mark",
                "ensnaring_strike",
                "hail_of_thorns",
                "primeval_awareness"
            };

            foreach (var actionId in rangerAbilities)
            {
                var action = registry.GetAction(actionId);
                Assert.NotNull(action);
            }
        }

        [Fact]
        public void EnsnaredVines_StatusAvailable_AndTagShapeLooksCorrect()
        {
            var rulesEngine = new RulesEngine(42);
            var statuses = CreateLoadedStatusManager(rulesEngine);
            var ranger = CreateCombatant("ranger");
            var enemy = CreateCombatant("enemy");

            statuses.ApplyStatus("ensnared_vines", ranger.Id, enemy.Id, duration: 3);
            var activeStatuses = statuses.GetStatuses(enemy.Id);

            Assert.Contains(activeStatuses, s => s.Definition.Id == "ensnared_vines");

            var definition = statuses.GetDefinition("ensnared_vines");
            Assert.NotNull(definition);
            Assert.Contains(definition.Tags, tag => tag.Contains("restrain", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void RangerCoreStatuses_AreLoadable_FromBg3Data()
        {
            var rulesEngine = new RulesEngine(42);
            var statuses = CreateLoadedStatusManager(rulesEngine);

            var expectedStatusIds = new[]
            {
                "hunters_mark",
                "ensnaring_strike",
                "ensnared_vines"
            };

            foreach (var statusId in expectedStatusIds)
            {
                Assert.NotNull(statuses.GetDefinition(statusId));
            }
        }
    }
}
