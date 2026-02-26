using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Data;
using Godot;

namespace QDND.Tests.Integration
{
    public class ShoveActionTests
    {
        private static string ResolveDataPath()
        {
            // Try relative paths from test execution location
            string[] candidates = {
                "Data",
                Path.Combine("..", "..", "..", "..", "Data"),
                Path.Combine("..", "..", "..", "Data"),
            };
            foreach (var c in candidates)
            {
                if (Directory.Exists(c)) return c;
            }
            throw new DirectoryNotFoundException("Cannot find Data folder");
        }

        [Fact]
        public void ShoveAction_LoadsWithVariantsAndEffects()
        {
            // Arrange
            var path = Path.Combine(ResolveDataPath(), "Actions", "common_actions.json");
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true, 
                Converters = { new JsonStringEnumConverter() } 
            };
            var pack = JsonSerializer.Deserialize<ActionPack>(json, options);
            
            // Act
            var shove = pack.Actions.Find(a => a.Id == "shove");
            
            // Assert
            Assert.NotNull(shove);
            Assert.Equal("Shove", shove.Name);
            Assert.Equal("contest", shove.ResolutionType);
            Assert.Equal("athletics", shove.ContestAttackerSkill);
            Assert.Equal("athletics,acrobatics", shove.ContestDefenderSkills);
            Assert.Empty(shove.Effects); // Base effects should be empty
            Assert.NotNull(shove.Variants);
            Assert.Equal(2, shove.Variants.Count);
            
            // Check push variant
            var pushVariant = shove.Variants.Find(v => v.VariantId == "shove_push");
            Assert.NotNull(pushVariant);
            Assert.NotEmpty(pushVariant.AdditionalEffects);
            Assert.Equal("forced_move", pushVariant.AdditionalEffects[0].Type);
            Assert.Equal(3, pushVariant.AdditionalEffects[0].Value);
            Assert.Equal("on_save_fail", pushVariant.AdditionalEffects[0].Condition);
            
            // Check prone variant  
            var proneVariant = shove.Variants.Find(v => v.VariantId == "shove_prone");
            Assert.NotNull(proneVariant);
            Assert.NotEmpty(proneVariant.AdditionalEffects);
            Assert.Equal("apply_status", proneVariant.AdditionalEffects[0].Type);
            Assert.Equal("prone", proneVariant.AdditionalEffects[0].StatusId);
        }

        [Fact]
        public void ShoveAction_AutoSelectsFirstVariant_WhenNoVariantSpecified()
        {
            // Arrange
            var path = Path.Combine(ResolveDataPath(), "Actions", "common_actions.json");
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true, 
                Converters = { new JsonStringEnumConverter() } 
            };
            var pack = JsonSerializer.Deserialize<ActionPack>(json, options);
            var shove = pack.Actions.Find(a => a.Id == "shove");
            
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
