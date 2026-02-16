using System;
using System.Collections.Generic;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Rules;
using QDND.Tools.AutoBattler;
using TargetSnapshot = QDND.Tools.AutoBattler.TargetSnapshot;

namespace QDND.Tests.Unit
{
    public class ActionDetailCollectorTests
    {
        [Fact]
        public void Collect_DamageSpell_IncludesDamageDealtAndTotal()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "fireball",
                SourceId = "wizard1",
                TargetIds = new List<string> { "goblin1" }
            };

            result.EffectResults.Add(new EffectResult
            {
                Success = true,
                EffectType = "damage",
                SourceId = "wizard1",
                TargetId = "goblin1",
                Value = 28, // Pre-mitigation damage
                Data = new Dictionary<string, object>
                {
                    { "damageType", "fire" },
                    { "actualDamageDealt", 24 },
                    { "wasCritical", false }
                }
            });

            var action = new ActionDefinition
            {
                Id = "fireball",
                Name = "Fireball",
                SpellLevel = 3,
                School = SpellSchool.Evocation,
                Range = 30f
            };

            var targetSnapshots = new List<TargetSnapshot>
            {
                new TargetSnapshot
                {
                    Id = "goblin1",
                    Position = new float[] { 5, 0, 3 },
                    CurrentHP = 4,
                    MaxHP = 28
                }
            };

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 35,
                sourceMaxHp: 45,
                targetSnapshots);

            // Assert
            Assert.True((bool)details["success"]);
            Assert.Equal("fireball", details["action_id"]);
            Assert.Equal(3, details["spell_level"]);
            Assert.Equal("Evocation", details["school"]);
            Assert.Equal(30f, details["range"]);
            Assert.Equal(35, details["source_hp"]);
            Assert.Equal(45, details["source_max_hp"]);

            var damageDealt = (List<Dictionary<string, object>>)details["damage_dealt"];
            Assert.Single(damageDealt);
            Assert.Equal("goblin1", damageDealt[0]["target"]);
            Assert.Equal(24, damageDealt[0]["amount"]);
            Assert.Equal("fire", damageDealt[0]["type"]);
            Assert.False((bool)damageDealt[0]["was_critical"]);

            Assert.Equal(24, details["total_damage"]);

            var targetStates = (List<Dictionary<string, object>>)details["target_states"];
            Assert.Single(targetStates);
            Assert.Equal("goblin1", targetStates[0]["id"]);
            Assert.Equal(4, targetStates[0]["hp"]);
            Assert.Equal(28, targetStates[0]["max_hp"]);
        }

        [Fact]
        public void Collect_HealEffect_IncludesHealingDone()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "cure_wounds",
                SourceId = "cleric1",
                TargetIds = new List<string> { "fighter1" }
            };

            result.EffectResults.Add(new EffectResult
            {
                Success = true,
                EffectType = "heal",
                SourceId = "cleric1",
                TargetId = "fighter1",
                Value = 12
            });

            var action = new ActionDefinition
            {
                Id = "cure_wounds",
                Name = "Cure Wounds",
                SpellLevel = 1
            };

            var targetSnapshots = new List<TargetSnapshot>
            {
                new TargetSnapshot
                {
                    Id = "fighter1",
                    Position = new float[] { 1, 0, 0 },
                    CurrentHP = 32,
                    MaxHP = 40
                }
            };

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 25,
                sourceMaxHp: 30,
                targetSnapshots);

            // Assert
            Assert.True((bool)details["success"]);
            var healingDone = (List<Dictionary<string, object>>)details["healing_done"];
            Assert.Single(healingDone);
            Assert.Equal("fighter1", healingDone[0]["target"]);
            Assert.Equal(12, healingDone[0]["amount"]);

            Assert.Equal(12, details["total_healing"]);
        }

        [Fact]
        public void Collect_StatusApply_IncludesStatusAndDuration()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "hold_person",
                SourceId = "wizard1",
                TargetIds = new List<string> { "bandit1" }
            };

            result.EffectResults.Add(new EffectResult
            {
                Success = true,
                EffectType = "apply_status",
                SourceId = "wizard1",
                TargetId = "bandit1",
                Value = 0,
                Data = new Dictionary<string, object>
                {
                    { "statusId", "paralyzed" },
                    { "duration", 10 }
                }
            });

            var action = new ActionDefinition
            {
                Id = "hold_person",
                Name = "Hold Person",
                SaveType = "wisdom"
            };

            var targetSnapshots = new List<TargetSnapshot>
            {
                new TargetSnapshot { Id = "bandit1", Position = new float[] { 5, 0, 0 }, CurrentHP = 18, MaxHP = 18 }
            };

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 30,
                sourceMaxHp: 30,
                targetSnapshots);

            // Assert
            var statusesApplied = (List<Dictionary<string, object>>)details["statuses_applied"];
            Assert.Single(statusesApplied);
            Assert.Equal("bandit1", statusesApplied[0]["target"]);
            Assert.Equal("paralyzed", statusesApplied[0]["status_id"]);
            Assert.Equal(10, statusesApplied[0]["duration"]);
        }

        [Fact]
        public void Collect_AttackRoll_IncludesRollDetails()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "longsword",
                SourceId = "fighter1",
                TargetIds = new List<string> { "goblin1" },
                AttackResult = new QueryResult
                {
                    NaturalRoll = 15,
                    FinalValue = 20,
                    IsSuccess = true,
                    IsCritical = false,
                    AdvantageState = 0,
                    Input = new QueryInput
                    {
                        Target = null // We don't need full combatant for this test
                    }
                }
            };

            var action = new ActionDefinition
            {
                Id = "longsword",
                AttackType = AttackType.MeleeWeapon
            };

            var targetSnapshots = new List<TargetSnapshot>
            {
                new TargetSnapshot { Id = "goblin1", Position = new float[] { 1, 0, 0 }, CurrentHP = 5, MaxHP = 7 }
            };

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 40,
                sourceMaxHp: 40,
                targetSnapshots);

            // Assert
            Assert.True(details.ContainsKey("attack_roll"));
            var attackRoll = (Dictionary<string, object>)details["attack_roll"];
            Assert.Equal(15, attackRoll["natural_roll"]);
            Assert.Equal(20, attackRoll["total"]);
            Assert.True((bool)attackRoll["hit"]);
            Assert.False((bool)attackRoll["critical"]);
            Assert.Equal("normal", attackRoll["advantage"]);
        }

        [Fact]
        public void Collect_SavingThrow_IncludesDCAndResult()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "fireball",
                SourceId = "wizard1",
                TargetIds = new List<string> { "goblin1" },
                SaveResult = new QueryResult
                {
                    NaturalRoll = 12,
                    FinalValue = 14,
                    IsSuccess = false,
                    Input = new QueryInput
                    {
                        DC = 15
                    }
                }
            };

            var action = new ActionDefinition
            {
                Id = "fireball",
                SaveType = "dexterity"
            };

            var targetSnapshots = new List<TargetSnapshot>
            {
                new TargetSnapshot { Id = "goblin1", Position = new float[] { 5, 0, 0 }, CurrentHP = 0, MaxHP = 7 }
            };

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 30,
                sourceMaxHp: 30,
                targetSnapshots);

            // Assert
            Assert.True(details.ContainsKey("saving_throw"));
            var savingThrow = (Dictionary<string, object>)details["saving_throw"];
            Assert.Equal("dexterity", savingThrow["type"]);
            Assert.Equal(15, savingThrow["dc"]);
            Assert.Equal(12, savingThrow["natural_roll"]);
            Assert.Equal(14, savingThrow["total"]);
            Assert.False((bool)savingThrow["passed"]);
        }

        [Fact]
        public void Collect_FailedAction_IncludesError()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = false,
                ActionId = "jump",
                SourceId = "fighter1",
                ErrorMessage = "Insufficient movement"
            };

            var action = new ActionDefinition
            {
                Id = "jump"
            };

            var targetSnapshots = new List<TargetSnapshot>();

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 40,
                sourceMaxHp: 40,
                targetSnapshots);

            // Assert
            Assert.False((bool)details["success"]);
            Assert.Equal("Insufficient movement", details["error"]);
        }

        [Fact]
        public void Collect_MultipleEffects_GroupsByCategory()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "inflict_wounds",
                SourceId = "cleric1",
                TargetIds = new List<string> { "skeleton1" }
            };

            // Add damage effect
            result.EffectResults.Add(new EffectResult
            {
                Success = true,
                EffectType = "damage",
                TargetId = "skeleton1",
                Value = 20,
                Data = new Dictionary<string, object>
                {
                    { "damageType", "necrotic" },
                    { "actualDamageDealt", 20 }
                }
            });

            // Add status effect
            result.EffectResults.Add(new EffectResult
            {
                Success = true,
                EffectType = "apply_status",
                TargetId = "skeleton1",
                Data = new Dictionary<string, object>
                {
                    { "statusId", "cursed" },
                    { "duration", 3 }
                }
            });

            var action = new ActionDefinition { Id = "inflict_wounds" };
            var targetSnapshots = new List<TargetSnapshot>
            {
                new TargetSnapshot { Id = "skeleton1", Position = new float[] { 1, 0, 0 }, CurrentHP = 0, MaxHP = 20 }
            };

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 25,
                sourceMaxHp: 30,
                targetSnapshots);

            // Assert
            Assert.True(details.ContainsKey("damage_dealt"));
            Assert.True(details.ContainsKey("statuses_applied"));

            var damageDealt = (List<Dictionary<string, object>>)details["damage_dealt"];
            Assert.Single(damageDealt);

            var statusesApplied = (List<Dictionary<string, object>>)details["statuses_applied"];
            Assert.Single(statusesApplied);
        }

        [Fact]
        public void Collect_EmptyEffects_OmitsNullFields()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "dash",
                SourceId = "rogue1",
                TargetIds = new List<string>()
            };

            var action = new ActionDefinition { Id = "dash", TargetType = TargetType.Self };
            var targetSnapshots = new List<TargetSnapshot>();

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 30,
                sourceMaxHp: 35,
                targetSnapshots);

            // Assert
            Assert.False(details.ContainsKey("damage_dealt"));
            Assert.False(details.ContainsKey("healing_done"));
            Assert.False(details.ContainsKey("statuses_applied"));
            Assert.False(details.ContainsKey("attack_roll"));
            Assert.False(details.ContainsKey("saving_throw"));
        }

        [Fact]
        public void Collect_SpellLevel_IncludedWhenPositive()
        {
            // Arrange - Cantrip (level 0)
            var result1 = new ActionExecutionResult { Success = true, ActionId = "firebolt", SourceId = "wizard1" };
            var action1 = new ActionDefinition { Id = "firebolt", SpellLevel = 0 };

            // Arrange - Level 1 spell
            var result2 = new ActionExecutionResult { Success = true, ActionId = "magic_missile", SourceId = "wizard1" };
            var action2 = new ActionDefinition { Id = "magic_missile", SpellLevel = 1 };

            var targetSnapshots = new List<TargetSnapshot>();

            // Act
            var details1 = ActionDetailCollector.Collect(result1, action1, new float[] { 0, 0, 0 }, 30, 30, targetSnapshots);
            var details2 = ActionDetailCollector.Collect(result2, action2, new float[] { 0, 0, 0 }, 30, 30, targetSnapshots);

            // Assert - Cantrip should not include spell_level
            Assert.False(details1.ContainsKey("spell_level"));

            // Assert - Level 1 spell should include spell_level
            Assert.True(details2.ContainsKey("spell_level"));
            Assert.Equal(1, details2["spell_level"]);
        }

        [Fact]
        public void Collect_TargetStates_IncludedForAllTargets()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "burning_hands",
                SourceId = "wizard1",
                TargetIds = new List<string> { "goblin1", "goblin2", "goblin3" }
            };

            var action = new ActionDefinition { Id = "burning_hands" };
            var targetSnapshots = new List<TargetSnapshot>
            {
                new TargetSnapshot { Id = "goblin1", Position = new float[] { 1, 0, 0 }, CurrentHP = 5, MaxHP = 7 },
                new TargetSnapshot { Id = "goblin2", Position = new float[] { 2, 0, 1 }, CurrentHP = 0, MaxHP = 7 },
                new TargetSnapshot { Id = "goblin3", Position = new float[] { 1, 0, -1 }, CurrentHP = 7, MaxHP = 7 }
            };

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 28,
                sourceMaxHp: 30,
                targetSnapshots);

            // Assert
            Assert.True(details.ContainsKey("target_states"));
            var targetStates = (List<Dictionary<string, object>>)details["target_states"];
            Assert.Equal(3, targetStates.Count);

            Assert.Equal("goblin1", targetStates[0]["id"]);
            Assert.Equal(5, targetStates[0]["hp"]);
            Assert.Equal(7, targetStates[0]["max_hp"]);

            Assert.Equal("goblin2", targetStates[1]["id"]);
            Assert.Equal(0, targetStates[1]["hp"]);
            Assert.Equal(7, targetStates[1]["max_hp"]);

            Assert.Equal("goblin3", targetStates[2]["id"]);
            Assert.Equal(7, targetStates[2]["hp"]);
            Assert.Equal(7, targetStates[2]["max_hp"]);
        }

        [Fact]
        public void Collect_NullResult_ReturnsEmptyDictionary()
        {
            // Arrange
            var action = new ActionDefinition { Id = "test" };

            // Act
            var details = ActionDetailCollector.Collect(
                result: null,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 30,
                sourceMaxHp: 30,
                targetSnapshots: new List<TargetSnapshot>());

            // Assert
            Assert.Empty(details);
        }

        [Fact]
        public void Collect_NullAction_ReturnsEmptyDictionary()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "test",
                SourceId = "unit1"
            };

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action: null,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 30,
                sourceMaxHp: 30,
                targetSnapshots: new List<TargetSnapshot>());

            // Assert
            Assert.Empty(details);
        }

        [Fact]
        public void Collect_NullDataOnEffectResult_DoesNotCrash()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "fireball",
                SourceId = "wizard1",
                TargetIds = new List<string> { "goblin1" }
            };

            result.EffectResults.Add(new EffectResult
            {
                Success = true,
                EffectType = "damage",
                SourceId = "wizard1",
                TargetId = "goblin1",
                Value = 20,
                Data = null  // Null data
            });

            var action = new ActionDefinition { Id = "fireball" };
            var targetSnapshots = new List<TargetSnapshot>();

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 30,
                sourceMaxHp: 30,
                targetSnapshots);

            // Assert - Should not crash and include basic damage data
            Assert.True((bool)details["success"]);
            var damageDealt = (List<Dictionary<string, object>>)details["damage_dealt"];
            Assert.Single(damageDealt);
            Assert.Equal(20, damageDealt[0]["amount"]);
            Assert.Equal("Unknown", damageDealt[0]["type"]);
        }

        [Fact]
        public void Collect_MultipleTeleportEffects_CapturedAsList()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "dimension_door",
                SourceId = "wizard1",
                TargetIds = new List<string> { "wizard1", "fighter1" }
            };

            result.EffectResults.Add(new EffectResult
            {
                Success = true,
                EffectType = "teleport",
                TargetId = "wizard1",
                Data = new Dictionary<string, object>
                {
                    { "from", new float[] { 0, 0, 0 } },
                    { "to", new float[] { 10, 0, 0 } }
                }
            });

            result.EffectResults.Add(new EffectResult
            {
                Success = true,
                EffectType = "teleport",
                TargetId = "fighter1",
                Data = new Dictionary<string, object>
                {
                    { "from", new float[] { 1, 0, 0 } },
                    { "to", new float[] { 11, 0, 0 } }
                }
            });

            var action = new ActionDefinition { Id = "dimension_door" };
            var targetSnapshots = new List<TargetSnapshot>();

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 30,
                sourceMaxHp: 30,
                targetSnapshots);

            // Assert
            Assert.True(details.ContainsKey("teleports"));
            var teleports = (List<Dictionary<string, object>>)details["teleports"];
            Assert.Equal(2, teleports.Count);
            Assert.Equal("wizard1", teleports[0]["target"]);
            Assert.Equal("fighter1", teleports[1]["target"]);
        }

        [Fact]
        public void Collect_MultipleForcedMovementEffects_CapturedAsList()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "thunderwave",
                SourceId = "wizard1",
                TargetIds = new List<string> { "goblin1", "goblin2" }
            };

            result.EffectResults.Add(new EffectResult
            {
                Success = true,
                EffectType = "push",
                TargetId = "goblin1",
                Data = new Dictionary<string, object>
                {
                    { "distance", 3.0f },
                    { "from", new float[] { 1, 0, 0 } },
                    { "to", new float[] { 4, 0, 0 } },
                    { "collisionDamage", 5 }
                }
            });

            result.EffectResults.Add(new EffectResult
            {
                Success = true,
                EffectType = "push",
                TargetId = "goblin2",
                Data = new Dictionary<string, object>
                {
                    { "distance", 3.0f },
                    { "from", new float[] { 2, 0, 0 } },
                    { "to", new float[] { 5, 0, 0 } }
                }
            });

            var action = new ActionDefinition { Id = "thunderwave" };
            var targetSnapshots = new List<TargetSnapshot>();

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 30,
                sourceMaxHp: 30,
                targetSnapshots);

            // Assert
            Assert.True(details.ContainsKey("forced_movements"));
            var movements = (List<Dictionary<string, object>>)details["forced_movements"];
            Assert.Equal(2, movements.Count);
            Assert.Equal("goblin1", movements[0]["target"]);
            Assert.Equal(5, movements[0]["collision_damage"]);
            Assert.Equal("goblin2", movements[1]["target"]);
            Assert.False(movements[1].ContainsKey("collision_damage"));
        }

        [Fact]
        public void Collect_MultipleSurfaceEffects_CapturedAsList()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "grease",
                SourceId = "wizard1",
                TargetIds = new List<string>()
            };

            result.EffectResults.Add(new EffectResult
            {
                Success = true,
                EffectType = "spawn_surface",
                TargetId = null,
                Data = new Dictionary<string, object>
                {
                    { "surfaceType", "grease" },
                    { "radius", 2.0f },
                    { "position", new float[] { 5, 0, 5 } }
                }
            });

            result.EffectResults.Add(new EffectResult
            {
                Success = true,
                EffectType = "spawn_surface",
                TargetId = null,
                Data = new Dictionary<string, object>
                {
                    { "surfaceType", "fire" },
                    { "radius", 1.5f },
                    { "position", new float[] { 10, 0, 10 } }
                }
            });

            var action = new ActionDefinition { Id = "grease" };
            var targetSnapshots = new List<TargetSnapshot>();

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 30,
                sourceMaxHp: 30,
                targetSnapshots);

            // Assert
            Assert.True(details.ContainsKey("surfaces_created"));
            var surfaces = (List<Dictionary<string, object>>)details["surfaces_created"];
            Assert.Equal(2, surfaces.Count);
            Assert.Equal("grease", surfaces[0]["type"]);
            Assert.Equal(2.0f, surfaces[0]["radius"]);
            Assert.Equal("fire", surfaces[1]["type"]);
        }

        [Fact]
        public void Collect_MultipleSummonEffects_CapturedAsList()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "conjure_animals",
                SourceId = "druid1",
                TargetIds = new List<string>()
            };

            result.EffectResults.Add(new EffectResult
            {
                Success = true,
                EffectType = "summon",
                TargetId = null,
                Message = "Wolf",
                Data = new Dictionary<string, object>
                {
                    { "templateId", "wolf_1" }
                }
            });

            result.EffectResults.Add(new EffectResult
            {
                Success = true,
                EffectType = "summon",
                TargetId = null,
                Message = "Wolf",
                Data = new Dictionary<string, object>
                {
                    { "templateId", "wolf_2" }
                }
            });

            var action = new ActionDefinition { Id = "conjure_animals" };
            var targetSnapshots = new List<TargetSnapshot>();

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 30,
                sourceMaxHp: 30,
                targetSnapshots);

            // Assert
            Assert.True(details.ContainsKey("summons"));
            var summons = (List<Dictionary<string, object>>)details["summons"];
            Assert.Equal(2, summons.Count);
            Assert.Equal("wolf_1", summons[0]["unit_id"]);
            Assert.Equal("Wolf", summons[0]["unit_name"]);
            Assert.Equal("wolf_2", summons[1]["unit_id"]);
        }

        [Fact]
        public void Collect_SourcePosition_IncludedInOutput()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "fireball",
                SourceId = "wizard1"
            };

            var action = new ActionDefinition { Id = "fireball" };
            var sourcePos = new float[] { 5, 1, 10 };
            var targetSnapshots = new List<TargetSnapshot>();

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: sourcePos,
                sourceHp: 30,
                sourceMaxHp: 30,
                targetSnapshots);

            // Assert
            Assert.True(details.ContainsKey("source_position"));
            var position = (float[])details["source_position"];
            Assert.Equal(5, position[0]);
            Assert.Equal(1, position[1]);
            Assert.Equal(10, position[2]);
        }

        [Fact]
        public void Collect_TargetPosition_IncludedInTargetStates()
        {
            // Arrange
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "magic_missile",
                SourceId = "wizard1",
                TargetIds = new List<string> { "goblin1" }
            };

            var action = new ActionDefinition { Id = "magic_missile" };
            var targetSnapshots = new List<TargetSnapshot>
            {
                new TargetSnapshot
                {
                    Id = "goblin1",
                    Position = new float[] { 15, 0, 20 },
                    CurrentHP = 5,
                    MaxHP = 10
                }
            };

            // Act
            var details = ActionDetailCollector.Collect(
                result,
                action,
                sourcePosition: new float[] { 0, 0, 0 },
                sourceHp: 30,
                sourceMaxHp: 30,
                targetSnapshots);

            // Assert
            var targetStates = (List<Dictionary<string, object>>)details["target_states"];
            Assert.Single(targetStates);
            Assert.True(targetStates[0].ContainsKey("position"));
            var position = (float[])targetStates[0]["position"];
            Assert.Equal(15, position[0]);
            Assert.Equal(0, position[1]);
            Assert.Equal(20, position[2]);
        }

        [Fact]
        public void Collect_WithPositionsBefore_IncludesSourcePosition()
        {
            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = "fireball",
                SourceId = "wizard1",
                TargetIds = new List<string> { "goblin1" },
                EffectResults = new List<EffectResult>(),
                SourcePositionBefore = new float[] { 1.0f, 0f, 2.0f },
                TargetPositionsBefore = new Dictionary<string, float[]>
                {
                    { "goblin1", new float[] { 5.0f, 0f, 5.0f } }
                }
            };
            var action = new ActionDefinition { Id = "fireball", Name = "Fireball" };

            var details = ActionDetailCollector.Collect(result, action, 
                result.SourcePositionBefore, 50, 50,
                new List<TargetSnapshot>
                {
                    new TargetSnapshot { Id = "goblin1", CurrentHP = 10, MaxHP = 30, Position = new float[] { 5.0f, 0f, 5.0f } }
                });

            Assert.True(details.ContainsKey("source_position"));
            var pos = details["source_position"] as float[];
            Assert.NotNull(pos);
            Assert.Equal(1.0f, pos[0]);
        }
    }
}
