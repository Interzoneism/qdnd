using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Data.Actions;
using QDND.Data.Spells;
using Xunit;

namespace QDND.Tests.Unit
{
    public class Phase2RuntimeSemanticsTests
    {
        [Fact]
        public void DealDamage_OnMissCondition_DoesNotTriggerOnHit()
        {
            var rules = new RulesEngine(seed: 7);
            var statuses = new StatusManager(rules);
            var source = new Combatant("source", "Source", Faction.Player, 100, 10);
            var target = new Combatant("target", "Target", Faction.Hostile, 100, 10);

            var definition = new EffectDefinition
            {
                Type = "damage",
                Value = 10,
                DamageType = "fire",
                Condition = "on_miss"
            };

            var context = new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { target },
                Rules = rules,
                Statuses = statuses,
                Rng = new Random(7),
                AttackResult = new QueryResult { IsSuccess = true }
            };

            var results = new DealDamageEffect().Execute(definition, context);

            Assert.Single(results);
            Assert.False(results[0].Success);
            Assert.Equal(100, target.Resources.CurrentHP);
        }

        [Fact]
        public void DealDamage_OnMissCondition_TriggersOnMiss()
        {
            var rules = new RulesEngine(seed: 8);
            var statuses = new StatusManager(rules);
            var source = new Combatant("source", "Source", Faction.Player, 100, 10);
            var target = new Combatant("target", "Target", Faction.Hostile, 100, 10);

            var definition = new EffectDefinition
            {
                Type = "damage",
                Value = 10,
                DamageType = "fire",
                Condition = "on_miss"
            };

            var context = new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { target },
                Rules = rules,
                Statuses = statuses,
                Rng = new Random(8),
                AttackResult = new QueryResult { IsSuccess = false }
            };

            var results = new DealDamageEffect().Execute(definition, context);

            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.True(target.Resources.CurrentHP < 100);
        }

        [Fact]
        public void ApplyStatus_OnSaveSuccessCondition_AppliesOnlyOnSuccessfulSave()
        {
            var rules = new RulesEngine(seed: 9);
            var statuses = new StatusManager(rules);
            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "test_mark",
                Name = "Test Mark",
                DurationType = DurationType.Turns,
                DefaultDuration = 1
            });

            var source = new Combatant("source", "Source", Faction.Player, 100, 10);
            var target = new Combatant("target", "Target", Faction.Hostile, 100, 10);

            var definition = new EffectDefinition
            {
                Type = "apply_status",
                StatusId = "test_mark",
                StatusDuration = 1,
                Condition = "on_save_success"
            };

            var successContext = new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { target },
                Rules = rules,
                Statuses = statuses,
                Rng = new Random(9),
                SaveResult = new QueryResult { IsSuccess = true }
            };
            successContext.PerTargetSaveResults[target.Id] = new QueryResult { IsSuccess = true };

            var successResults = new ApplyStatusEffect().Execute(definition, successContext);

            Assert.Single(successResults);
            Assert.True(successResults[0].Success);
            Assert.True(statuses.HasStatus(target.Id, "test_mark"));

            statuses.RemoveStatus(target.Id, "test_mark");

            var failContext = new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { target },
                Rules = rules,
                Statuses = statuses,
                Rng = new Random(9),
                SaveResult = new QueryResult { IsSuccess = false }
            };
            failContext.PerTargetSaveResults[target.Id] = new QueryResult { IsSuccess = false };

            var failResults = new ApplyStatusEffect().Execute(definition, failContext);

            Assert.Single(failResults);
            Assert.False(failResults[0].Success);
            Assert.False(statuses.HasStatus(target.Id, "test_mark"));
        }

        [Fact]
        public void SpawnSurfaceEffect_PreservesZeroDuration()
        {
            var rules = new RulesEngine(seed: 10);
            var statuses = new StatusManager(rules);
            var source = new Combatant("source", "Source", Faction.Player, 100, 10);
            var target = new Combatant("target", "Target", Faction.Hostile, 100, 10);

            var definition = new EffectDefinition
            {
                Type = "spawn_surface",
                Value = 3,
                StatusDuration = 0,
                Parameters = new Dictionary<string, object>
                {
                    { "surface_type", "fire" }
                }
            };

            var context = new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { target },
                Rules = rules,
                Statuses = statuses,
                Rng = new Random(10)
            };

            var results = new SpawnSurfaceEffect().Execute(definition, context);

            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.Equal(0, Convert.ToInt32(results[0].Data["duration"]));
        }

        [Fact]
        public void SpawnSurfaceEffect_PreservesNegativeDuration()
        {
            var rules = new RulesEngine(seed: 11);
            var statuses = new StatusManager(rules);
            var source = new Combatant("source", "Source", Faction.Player, 100, 10);
            var target = new Combatant("target", "Target", Faction.Hostile, 100, 10);

            var definition = new EffectDefinition
            {
                Type = "spawn_surface",
                Value = 3,
                StatusDuration = -1,
                Parameters = new Dictionary<string, object>
                {
                    { "surface_type", "fire" }
                }
            };

            var context = new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { target },
                Rules = rules,
                Statuses = statuses,
                Rng = new Random(11)
            };

            var results = new SpawnSurfaceEffect().Execute(definition, context);

            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.Equal(-1, Convert.ToInt32(results[0].Data["duration"]));
        }

        [Fact]
        public void EffectPipeline_RegistersPhase2NoOpHandlers()
        {
            var pipeline = new EffectPipeline();
            var registered = pipeline.GetRegisteredEffectTypes();

            Assert.Contains("execute_weapon_functors", registered);
            Assert.Contains("surface_change", registered);
            Assert.Contains("stabilize", registered);
            Assert.Contains("resurrect", registered);
            Assert.Contains("remove_status_by_group", registered);
            Assert.Contains("switch_death_type", registered);
            Assert.Contains("set_advantage", registered);
            Assert.Contains("set_disadvantage", registered);
        }

        [Fact]
        public void BG3ActionConverter_SaveSpellFailEffects_MapToOnSaveSuccess()
        {
            var spell = new BG3SpellData
            {
                Id = "test_spell_fail_save",
                DisplayName = "Test",
                SpellSaveDC = "Dexterity",
                SpellFail = "ApplyStatus(CHILLED,100,1)"
            };

            var action = BG3ActionConverter.ConvertToAction(spell);
            var failEffect = action.Effects.First(e => e.Type == "apply_status" && e.StatusId == "chilled");

            Assert.Equal("on_save_success", failEffect.Condition);
        }
    }
}
