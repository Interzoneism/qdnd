using System;
using System.Collections.Generic;
using Xunit;  
using Xunit.Abstractions;
using QDND.Combat.Abilities;
using QDND.Combat.Abilities.Effects;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Unit
{
    public class SaveDebugTest
    {
        private readonly ITestOutputHelper _output;

        public SaveDebugTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void DebugPerTargetSave()
        {
            var rules = new RulesEngine(123);
            var statuses = new StatusManager(rules);
            var pipeline = new EffectPipeline
            {
                Rules = rules,
                Statuses = statuses,
                Rng = new Random(123)
            };
            
            pipeline.RegisterEffect(new DealDamageEffect());
            
            var source = new Combatant("source", "Source", Faction.Player, 100, 10);
            var target = new Combatant("target", "Target", Faction.Hostile, 100, 10)
            {
                Stats = new CombatantStats { Wisdom = 30 } // High wisdom - should pass
            };

            var ability = new AbilityDefinition
            {
                Id = "test",
                Name = "Test",
                TargetType = TargetType.SingleUnit,
                SaveType = "wisdom",
                SaveDC = 15,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        Value = 10,
                        DamageType = "fire",
                        Condition = "on_save_fail"
                    }
                },
                Tags = new HashSet<string> { "spell" }
            };
            pipeline.RegisterAbility(ability);

            var result = pipeline.ExecuteAbility("test", source, new List<Combatant> { target });
            
            _output.WriteLine($"Result Success: {result.Success}");
            _output.WriteLine($"Target HP: {target.Resources.CurrentHP}");
            _output.WriteLine($"Save Result: {result.SaveResult?.IsSuccess}");
            _output.WriteLine($"Effect Results Count: {result.EffectResults.Count}");
            
            foreach (var eff in result.EffectResults)
            {
                _output.WriteLine($"Effect: {eff.EffectType}, Success: {eff.Success}, Message: {eff.Message}");
            }

            Assert.Equal(100, target.Resources.CurrentHP); // Should NOT take damage (save succeeded)
        }
    }
}
