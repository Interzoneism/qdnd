using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Data.Actions;
using QDND.Data.Parsers;
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

        [Fact]
        public void BG3SpellParser_SpellTypeWall_ParsesAsWall()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile,
                    "new entry \"Test_WallSpell\"\n" +
                    "type \"SpellData\"\n" +
                    "data \"SpellType\" \"Wall\"\n");

                var parser = new BG3SpellParser();
                var spells = parser.ParseFile(tempFile);

                var spell = Assert.Single(spells);
                Assert.Equal(BG3SpellType.Wall, spell.SpellType);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void BG3SpellParser_ConcentrationSpellId_FlowsToActionDefinition()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile,
                    "new entry \"Test_ConcentrationSpell\"\n" +
                    "type \"SpellData\"\n" +
                    "data \"SpellType\" \"Target\"\n" +
                    "data \"ConcentrationSpellID\" \"CONCENTRATION_TEST_STATUS\"\n");

                var parser = new BG3SpellParser();
                var spells = parser.ParseFile(tempFile);
                var spell = Assert.Single(spells);

                Assert.Equal("CONCENTRATION_TEST_STATUS", spell.ConcentrationSpellID);

                var action = BG3ActionConverter.ConvertToAction(spell);
                Assert.Equal("CONCENTRATION_TEST_STATUS", action.ConcentrationStatusId);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void BG3SpellParser_PriorityMetadataFields_FlowToActionDefinition()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile,
                    "new entry \"Test_MetadataSpell\"\n" +
                    "type \"SpellData\"\n" +
                    "data \"SpellType\" \"Target\"\n" +
                    "data \"ContainerSpells\" \"Spell_A;Spell_B\"\n" +
                    "data \"SpellContainerID\" \"Container_Test\"\n" +
                    "data \"SurfaceType\" \"Fire\"\n" +
                    "data \"AoEConditions\" \"not Ally()\"\n" +
                    "data \"MaximumTotalTargetHP\" \"24\"\n");

                var parser = new BG3SpellParser();
                var spells = parser.ParseFile(tempFile);
                var spell = Assert.Single(spells);

                var action = BG3ActionConverter.ConvertToAction(spell);

                Assert.Equal("Spell_A;Spell_B", action.BG3ContainerSpells);
                Assert.Equal("Container_Test", action.BG3SpellContainerId);
                Assert.Equal("Fire", action.BG3SurfaceType);
                Assert.Equal("not Ally()", action.BG3AoEConditions);
                Assert.Equal("24", action.BG3MaximumTotalTargetHP);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void BG3ActionConverter_ShoutWithAreaRadius_UsesAreaRadiusAsRangeWhenRangeMissing()
        {
            var spell = new BG3SpellData
            {
                Id = "Shout_Test",
                SpellType = BG3SpellType.Shout,
                AreaRadius = "3"
            };

            var action = BG3ActionConverter.ConvertToAction(spell);

            Assert.Equal(TargetType.Circle, action.TargetType);
            Assert.Equal(3f, action.AreaRadius);
            Assert.Equal(3f, action.Range);
        }

        [Fact]
        public void BG3ActionConverter_ZoneSquare_MapsToAreaTargeting_NotLine()
        {
            var spell = new BG3SpellData
            {
                Id = "Thunderwave_Test",
                SpellType = BG3SpellType.Zone,
                ZoneShape = "Square",
                ZoneBase = "3",
                ZoneRange = "4"
            };

            var action = BG3ActionConverter.ConvertToAction(spell);

            Assert.Equal(TargetType.Cone, action.TargetType);
            Assert.NotEqual(TargetType.Line, action.TargetType);
            Assert.Equal(90f, action.ConeAngle);
            Assert.Equal(3f, action.AreaRadius);
            Assert.Equal(4f, action.Range);
        }

        [Fact]
        public void BG3SpellParser_AIFlags_AreParsedFromSpellData()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile,
                    "new entry \"Test_AIFlags\"\n" +
                    "type \"SpellData\"\n" +
                    "data \"SpellType\" \"Target\"\n" +
                    "data \"AIFlags\" \"CanNotUse;UseAsSupportingActionOnly\"\n");

                var parser = new BG3SpellParser();
                var spells = parser.ParseFile(tempFile);

                var spell = Assert.Single(spells);
                Assert.Equal("Test_AIFlags", spell.Id);
                Assert.Equal("CanNotUse;UseAsSupportingActionOnly", spell.AIFlags);
                Assert.True(spell.HasAIFlag("CanNotUse"));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void BG3ActionConverter_CanNotUseAIFlag_AddsAiNoUseTag()
        {
            var spell = new BG3SpellData
            {
                Id = "Target_Darkvision_Test",
                SpellType = BG3SpellType.Target,
                AIFlags = "CanNotUse",
                VerbalIntent = "Utility",
                SpellProperties = "ApplyStatus(DARKVISION,100,-1)",
                TargetConditions = "Character() and Ally()"
            };

            var action = BG3ActionConverter.ConvertToAction(spell);

            Assert.Contains("ai_no_use", action.Tags, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void BG3ActionConverter_UtilityCharacterSpell_TargetsAlliesAndSelf_NotAll()
        {
            var spell = new BG3SpellData
            {
                Id = "Target_Longstrider_Test",
                SpellType = BG3SpellType.Target,
                VerbalIntent = "Utility",
                SpellProperties = "ApplyStatus(LONGSTRIDER,100,-1)",
                TargetConditions = "Character()"
            };

            var action = BG3ActionConverter.ConvertToAction(spell);

            Assert.True(action.TargetFilter.HasFlag(TargetFilter.Allies));
            Assert.True(action.TargetFilter.HasFlag(TargetFilter.Self));
            Assert.False(action.TargetFilter.HasFlag(TargetFilter.Enemies));
        }
    }
}
