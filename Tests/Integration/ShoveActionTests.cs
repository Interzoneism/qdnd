using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Data.Actions;

namespace QDND.Tests.Integration
{
    public class ShoveActionTests
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

        private static ActionDefinition LoadShoveAction()
        {
            var registry = new ActionRegistry();
            var bg3DataPath = Path.Combine(FindRepoRoot(), "BG3_Data");
            var init = ActionRegistryInitializer.Initialize(registry, bg3DataPath, verboseLogging: false);
            Assert.True(init.Success, init.ErrorMessage ?? "Action registry initialization failed");

            var shove = registry.GetAction("shove");
            Assert.NotNull(shove);
            return shove;
        }

        [Fact]
        public void ShoveAction_LoadsWithVariantsAndEffects()
        {
            var shove = LoadShoveAction();

            Assert.NotNull(shove);
            Assert.Equal("shove", shove.Id);
            // BG3 Shove costs a bonus action (UseCosts "BonusActionPoint:1")
            Assert.True(shove.Cost?.UsesBonusAction == true, "BG3 Shove must cost a bonus action");
            // Has parsed effects (RemoveStatus from SpellProperties, Force from SpellSuccess, etc.)
            Assert.NotEmpty(shove.Effects);
            // Melee range — BG3 TargetRadius is 1.5m
            Assert.True(shove.Range <= 2f, $"Expected melee range ≤ 2m but got {shove.Range}");
        }

        [Fact]
        public void ShoveAction_ExecutesBaseEffects()
        {
            var shove = LoadShoveAction();

            var rules = new RulesEngine(42);
            var pipeline = new EffectPipeline
            {
                Rules = rules,
                Statuses = new StatusManager(rules),
                Rng = new Random(42)
            };
            pipeline.RegisterAction(shove);

            var source = new Combatant("attacker", "Attacker", Faction.Hostile, 50, 10)
            {
                Position = Vector3.Zero
            };
            var target = new Combatant("target", "Target", Faction.Player, 50, 10)
            {
                Position = new Vector3(1, 0, 0)
            };

            var result = pipeline.ExecuteAction("shove", source, new List<Combatant> { target });

            Assert.NotNull(result);
            // Execution completes — success or failure via contested check
        }
    }
}
