using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Data.Actions;
using Godot;

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
            
            // Assert
            Assert.NotNull(shove);
            Assert.Equal("Shove", shove.Name);
            Assert.Equal("contest", shove.ResolutionType);
            Assert.Equal("athletics", shove.ContestAttackerSkill);
            Assert.Equal("athletics,acrobatics", shove.ContestDefenderSkills);
            Assert.Empty(shove.Effects); // Base effects should be empty
            Assert.NotNull(shove.Variants);
            Assert.True(shove.Variants.Count >= 1);

            Assert.Contains(
                shove.Variants,
                variant => variant.AdditionalEffects != null
                    && variant.AdditionalEffects.Exists(effect =>
                        effect.Type == "forced_move" && effect.Value >= 2));

            Assert.Contains(
                shove.Variants,
                variant => variant.AdditionalEffects != null
                    && variant.AdditionalEffects.Exists(effect =>
                        effect.Type == "apply_status" && effect.StatusId == "prone"));
        }

        [Fact]
        public void ShoveAction_AutoSelectsFirstVariant_WhenNoVariantSpecified()
        {
            var shove = LoadShoveAction();
            
            var pipeline = new EffectPipeline();
            pipeline.RegisterAction(shove);
            pipeline.Rules = new RulesEngine(42);
            
            var source = new Combatant("attacker", "Attacker", Faction.Hostile, 50, 10)
            {
                Position = Vector3.Zero
            };
            var target = new Combatant("target", "Target", Faction.Player, 50, 10)
            {
                Position = new Vector3(1, 0, 0)
            };
            
            // Act - execute with no variant specified
            var result = pipeline.ExecuteAction("shove", source, new List<Combatant> { target }, new ActionExecutionOptions());
            
            // Assert
            Assert.True(result.Success, $"Shove should succeed but got: {result.ErrorMessage}");
            // Should have at least one effect result (either success or failure due to save)
            Assert.NotEmpty(result.EffectResults);
        }
    }
}
