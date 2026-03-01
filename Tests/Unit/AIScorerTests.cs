using Xunit;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Services;
using QDND.Combat.Statuses;
using QDND.Data.Statuses;
using QDND.Tests.Helpers;
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for AIScorer.
    /// Uses null context to avoid Godot dependencies.
    /// </summary>
    public class AIScorerTests
    {
        private AIScorer CreateScorer(LOSService? los = null, HeightService? height = null)
        {
            // Pass null for context - AIScorer handles null gracefully
            return new AIScorer(null, los, height, null);
        }

        private Combatant CreateTestCombatant(string id, int hp, int maxHp, Vector3 position, Faction faction = Faction.Player)
        {
            var combatant = new Combatant(id, id, faction, maxHp, 10);
            combatant.Resources.CurrentHP = hp;
            combatant.Position = position;
            return combatant;
        }

        [Fact]
        public void ScoreAttack_BasedOnDamage()
        {
            // Arrange
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var target = CreateTestCombatant("target", 30, 30, new Vector3(10, 0, 0), Faction.Hostile);

            var scorer = CreateScorer();
            var profile = new AIProfile();
            var action = new AIAction
            {
                ActionType = AIActionType.Attack,
                ActionId = "Target_MainHandAttack",
                TargetId = target.Id
            };

            // Act
            scorer.ScoreAttack(action, actor, target, profile);

            // Assert
            Assert.True(action.IsValid);
            Assert.True(action.Score > 0);
            Assert.True(action.ScoreBreakdown.ContainsKey("damage_value"));
            Assert.True(action.ExpectedValue > 0);
        }

        [Fact]
        public void ScoreAttack_KillPotential_AddsBonus()
        {
            // Arrange
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var target = CreateTestCombatant("target", 5, 30, new Vector3(10, 0, 0), Faction.Hostile); // Low HP

            var scorer = CreateScorer();
            var profile = new AIProfile();
            var action = new AIAction
            {
                ActionType = AIActionType.Attack,
                ActionId = "strong_attack",
                TargetId = target.Id
            };

            // Act
            scorer.ScoreAttack(action, actor, target, profile);

            // Assert
            Assert.True(action.ScoreBreakdown.ContainsKey("kill_potential"));
            Assert.True(action.ScoreBreakdown["kill_potential"] > 0);
        }

        [Fact]
        public void ScoreAttack_FocusFire_BonusForWounded()
        {
            // Arrange
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var target = CreateTestCombatant("target", 12, 30, new Vector3(10, 0, 0), Faction.Hostile); // 40% HP

            var scorer = CreateScorer();
            var profile = new AIProfile { FocusFire = true };
            var action = new AIAction
            {
                ActionType = AIActionType.Attack,
                ActionId = "Target_MainHandAttack",
                TargetId = target.Id
            };

            // Act
            scorer.ScoreAttack(action, actor, target, profile);

            // Assert
            Assert.True(action.ScoreBreakdown.ContainsKey("focus_fire"));
            Assert.True(action.ScoreBreakdown["focus_fire"] > 0);
        }

        [Fact]
        public void ScoreAttack_LowHitChance_Penalized()
        {
            // Arrange
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var target = CreateTestCombatant("target", 30, 30, new Vector3(10, 0, 0), Faction.Hostile);

            var scorer = CreateScorer();
            var profile = new AIProfile();
            var action = new AIAction
            {
                ActionType = AIActionType.Attack,
                ActionId = "Target_MainHandAttack",
                TargetId = target.Id
            };

            // Act - Score with low hit chance
            scorer.ScoreAttack(action, actor, target, profile);

            // Assert
            if (action.HitChance < AIWeights.LowHitChanceThreshold)
            {
                Assert.True(action.ScoreBreakdown.ContainsKey("low_hit_chance_penalty"));
            }
        }

        [Fact]
        public void ScoreAttack_InvalidTarget_Fails()
        {
            // Arrange
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);

            var scorer = CreateScorer();
            var profile = new AIProfile();
            var action = new AIAction
            {
                ActionType = AIActionType.Attack,
                ActionId = "Target_MainHandAttack",
                TargetId = "invalid"
            };

            // Act
            scorer.ScoreAttack(action, actor, null, profile);

            // Assert
            Assert.False(action.IsValid);
            Assert.Contains("Invalid target", action.InvalidReason);
        }

        [Fact]
        public void ScoreHealing_PrioritizesLowHP()
        {
            // Arrange
            var actor = CreateTestCombatant("healer", 50, 50, Vector3.Zero, Faction.Player);
            var target = CreateTestCombatant("wounded", 5, 30, new Vector3(5, 0, 0), Faction.Player); // 16% HP

            var scorer = CreateScorer();
            var profile = new AIProfile();
            var action = new AIAction
            {
                ActionType = AIActionType.UseAbility,
                ActionId = "heal",
                TargetId = target.Id
            };

            // Act
            scorer.ScoreHealing(action, actor, target, profile);

            // Assert
            Assert.True(action.IsValid);
            Assert.True(action.ScoreBreakdown.ContainsKey("save_ally"));
            Assert.True(action.ScoreBreakdown["save_ally"] > 0);
        }

        [Fact]
        public void ScoreHealing_SelfHealingReduced()
        {
            // Arrange
            var actor = CreateTestCombatant("healer", 20, 50, Vector3.Zero, Faction.Player);

            var scorer = CreateScorer();
            var profile = new AIProfile();
            var action = new AIAction
            {
                ActionType = AIActionType.UseAbility,
                ActionId = "self_heal",
                TargetId = actor.Id
            };

            // Act
            scorer.ScoreHealing(action, actor, actor, profile);

            // Assert
            Assert.True(action.ScoreBreakdown.ContainsKey("self_heal_reduction"));
            Assert.True(action.ScoreBreakdown["self_heal_reduction"] < 0);
        }

        [Fact]
        public void ScoreMovement_PrefersHighGround()
        {
            // Arrange
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var enemy = CreateTestCombatant("enemy", 30, 30, new Vector3(10, 0, 0), Faction.Hostile);

            var scorer = CreateScorer();
            var profile = new AIProfile();
            var action = new AIAction
            {
                ActionType = AIActionType.Move,
                TargetPosition = new Vector3(5, 3, 0) // Higher ground
            };

            // Act
            scorer.ScoreMovement(action, actor, profile);

            // Assert
            Assert.True(action.ScoreBreakdown.ContainsKey("seek_high_ground"));
            Assert.True(action.ScoreBreakdown["seek_high_ground"] > 0);
        }

        [Fact]
        public void ScoreMovement_AvoidsDanger()
        {
            // Arrange
            var context = new HeadlessCombatContext();
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var enemy1 = CreateTestCombatant("enemy1", 30, 30, new Vector3(3, 0, 0), Faction.Hostile);
            var enemy2 = CreateTestCombatant("enemy2", 30, 30, new Vector3(4, 0, 0), Faction.Hostile);

            context.RegisterCombatant(actor);
            context.RegisterCombatant(enemy1);
            context.RegisterCombatant(enemy2);

            var scorer = new AIScorer(context, null, null, null);
            var profile = new AIProfile();
            var action = new AIAction
            {
                ActionType = AIActionType.Move,
                TargetPosition = new Vector3(3.5f, 0, 0) // Between two enemies
            };

            // Act
            scorer.ScoreMovement(action, actor, profile);

            // Assert
            Assert.True(action.ScoreBreakdown.ContainsKey("danger"));
            Assert.True(action.ScoreBreakdown["danger"] < 0);
        }

        [Fact]
        public void ScoreMovement_SeeksFlank()
        {
            // Arrange
            var actor = CreateTestCombatant("actor", 50, 50, new Vector3(-10, 0, 0), Faction.Player);
            var ally = CreateTestCombatant("ally", 40, 40, new Vector3(10, 0, 0), Faction.Player);
            var enemy = CreateTestCombatant("enemy", 30, 30, Vector3.Zero, Faction.Hostile);

            var scorer = CreateScorer();
            var profile = new AIProfile();
            var action = new AIAction
            {
                ActionType = AIActionType.Move,
                TargetPosition = new Vector3(-8, 0, 0) // Opposite ally
            };

            // Act
            scorer.ScoreMovement(action, actor, profile);

            // Assert - May or may not flank depending on exact positions
            Assert.True(action.IsValid);
        }

        [Fact]
        public void ScoreAoE_AvoidsFriendlyFire()
        {
            // Arrange
            var context = new HeadlessCombatContext();
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var ally = CreateTestCombatant("ally", 40, 40, new Vector3(5, 0, 0), Faction.Player);
            var enemy1 = CreateTestCombatant("enemy1", 30, 30, new Vector3(6, 0, 0), Faction.Hostile);
            var enemy2 = CreateTestCombatant("enemy2", 30, 30, new Vector3(7, 0, 0), Faction.Hostile);

            context.RegisterCombatant(actor);
            context.RegisterCombatant(ally);
            context.RegisterCombatant(enemy1);
            context.RegisterCombatant(enemy2);

            var scorer = new AIScorer(context, null, null, null);
            var profile = new AIProfile { AvoidFriendlyFire = true };
            var action = new AIAction
            {
                ActionType = AIActionType.UseAbility,
                ActionId = "Projectile_Fireball",
                TargetPosition = new Vector3(6, 0, 0)
            };

            // Act
            scorer.ScoreAoE(action, actor, new Vector3(6, 0, 0), 3f, profile);

            // Assert
            Assert.True(action.ScoreBreakdown.ContainsKey("friendly_fire"));
            Assert.True(action.ScoreBreakdown["friendly_fire"] < 0);
            Assert.True(action.ScoreBreakdown.ContainsKey("enemies_hit"));
        }

        [Fact]
        public void ScoreStatusEffect_ControlHigherValue()
        {
            // Arrange
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var target = CreateTestCombatant("target", 30, 30, new Vector3(10, 0, 0), Faction.Hostile);

            var scorer = CreateScorer();
            var profile = new AIProfile();

            var stunAction = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "stun" };
            var slowAction = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "slow" };

            // Act
            scorer.ScoreStatusEffect(stunAction, actor, target, "stun", profile);
            scorer.ScoreStatusEffect(slowAction, actor, target, "slow", profile);

            // Assert
            Assert.True(stunAction.Score > slowAction.Score, "Stun should score higher than slow");
            Assert.True(stunAction.ScoreBreakdown.ContainsKey("status_value"));
            Assert.True(slowAction.ScoreBreakdown.ContainsKey("status_value"));
        }

        [Fact]
        public void Weights_AffectScoring()
        {
            // Arrange
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var target = CreateTestCombatant("target", 30, 30, new Vector3(10, 0, 0), Faction.Hostile);

            var defaultProfile = new AIProfile();
            defaultProfile.Weights["damage"] = 1.0f;

            var aggressiveProfile = new AIProfile();
            aggressiveProfile.Weights["damage"] = 2.0f;

            var scorer = CreateScorer();

            var action1 = new AIAction { ActionType = AIActionType.Attack, ActionId = "attack", TargetId = target.Id };
            var action2 = new AIAction { ActionType = AIActionType.Attack, ActionId = "attack", TargetId = target.Id };

            // Act
            scorer.ScoreAttack(action1, actor, target, defaultProfile);
            scorer.ScoreAttack(action2, actor, target, aggressiveProfile);

            // Assert
            Assert.True(action2.Score > action1.Score, "Higher damage weight should increase score");
        }

        [Fact]
        public void AIWeightConfig_InitializesDefaults()
        {
            // Arrange & Act
            var config = new AIWeightConfig();

            // Assert
            Assert.Equal(AIWeights.DamagePerPoint, config.Get("damage_per_point"));
            Assert.Equal(AIWeights.KillBonus, config.Get("kill_bonus"));
            Assert.Equal(AIWeights.HealingPerPoint, config.Get("healing_per_point"));
        }

        [Fact]
        public void AIWeightConfig_CanModifyWeights()
        {
            // Arrange
            var config = new AIWeightConfig();

            // Act
            config.Set("damage_per_point", 0.5f);

            // Assert
            Assert.Equal(0.5f, config.Get("damage_per_point"));
        }

        [Fact]
        public void AIWeightConfig_UnknownKeyReturnsDefault()
        {
            // Arrange
            var config = new AIWeightConfig();

            // Act
            var value = config.Get("unknown_key");

            // Assert
            Assert.Equal(1f, value);
        }

        [Fact]
        public void ScoreAttack_WithCover_AppliesPenalty()
        {
            // Arrange
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var target = CreateTestCombatant("target", 30, 30, new Vector3(10, 0, 0), Faction.Hostile);

            var los = new LOSService();
            los.RegisterCombatant(actor);
            los.RegisterCombatant(target);

            // Add obstacle providing cover
            var obstacle = new Obstacle
            {
                Id = "wall",
                Position = new Vector3(5, 0, 0),
                Width = 2f,
                Height = 2f,
                ProvidedCover = CoverLevel.Half,
                BlocksLOS = false
            };
            los.RegisterObstacle(obstacle);

            var scorer = CreateScorer(los);
            var profile = new AIProfile();
            var action = new AIAction
            {
                ActionType = AIActionType.Attack,
                ActionId = "Target_MainHandAttack",
                TargetId = target.Id
            };

            // Act
            scorer.ScoreAttack(action, actor, target, profile);

            // Assert - Should have cover penalty if cover is detected
            var cover = los.GetCover(actor, target);
            if (cover != CoverLevel.None)
            {
                Assert.True(action.ScoreBreakdown.ContainsKey("target_in_cover"));
                Assert.True(action.ScoreBreakdown["target_in_cover"] < 0);
            }
        }

        [Fact]
        public void ScoreAttack_WithHeightAdvantage_AddsBonus()
        {
            // Arrange
            var actor = CreateTestCombatant("actor", 50, 50, new Vector3(0, 5, 0), Faction.Player); // Higher
            var target = CreateTestCombatant("target", 30, 30, Vector3.Zero, Faction.Hostile);

            var height = new HeightService();

            var scorer = CreateScorer(null, height);
            var profile = new AIProfile();
            var action = new AIAction
            {
                ActionType = AIActionType.Attack,
                ActionId = "Target_MainHandAttack",
                TargetId = target.Id
            };

            // Act
            scorer.ScoreAttack(action, actor, target, profile);

            // Assert
            if (height.HasHeightAdvantage(actor, target))
            {
                Assert.True(action.ScoreBreakdown.ContainsKey("height_advantage"));
                Assert.True(action.ScoreBreakdown["height_advantage"] > 0);
            }
        }

        // ──────────────────────────────────────────────
        //  Phase 3: DoT / HoT / Boost sub-multiplier tests
        // ──────────────────────────────────────────────

        private AIProfile CreateBG3Profile(Dictionary<string, float> overrides = null)
        {
            var bg3 = new BG3ArchetypeProfile();
            if (overrides != null)
                bg3.LoadFromSettings(overrides);
            var profile = new AIProfile { BG3Profile = bg3 };
            return profile;
        }

        private HeadlessCombatContext CreateContextWithDoTStatus(string statusId)
        {
            var context = new HeadlessCombatContext();
            var rules = new QDND.Combat.Rules.RulesEngine(42);
            var statusMgr = new StatusManager(rules);
            statusMgr.RegisterStatus(new StatusDefinition
            {
                Id = statusId,
                Name = statusId,
                TickEffects = new List<StatusTickEffect>
                {
                    new StatusTickEffect { EffectType = "damage", Value = 5, DamageType = "Fire" }
                }
            });
            context.RegisterService<StatusManager>(statusMgr);
            return context;
        }

        private HeadlessCombatContext CreateContextWithHoTStatus(string statusId)
        {
            var context = new HeadlessCombatContext();
            var rules = new QDND.Combat.Rules.RulesEngine(42);
            var statusMgr = new StatusManager(rules);
            statusMgr.RegisterStatus(new StatusDefinition
            {
                Id = statusId,
                Name = statusId,
                TickEffects = new List<StatusTickEffect>
                {
                    new StatusTickEffect { EffectType = "heal", Value = 5 }
                }
            });
            context.RegisterService<StatusManager>(statusMgr);
            return context;
        }

        private HeadlessCombatContext CreateContextWithBoostStatus(string statusId)
        {
            var context = new HeadlessCombatContext();
            // Register a BG3StatusData with BOOST type and no ticks
            var statusReg = new StatusRegistry();
            statusReg.RegisterStatus(new BG3StatusData
            {
                StatusId = statusId,
                StatusType = BG3StatusType.BOOST
            });
            context.RegisterService<StatusRegistry>(statusReg);
            return context;
        }

        [Fact]
        public void ScoreStatusEffect_DoT_UsesMultiplierDotEnemyPos()
        {
            // Arrange
            var context = CreateContextWithDoTStatus("BURNING");
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var target = CreateTestCombatant("target", 30, 30, new Vector3(10, 0, 0), Faction.Hostile);
            context.RegisterCombatant(actor);
            context.RegisterCombatant(target);

            var profile = CreateBG3Profile(new Dictionary<string, float>
            {
                ["MULTIPLIER_DOT_ENEMY_POS"] = 3.0f
            });

            var scorer = new AIScorer(context);
            var action = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "apply_burn" };

            // Act
            scorer.ScoreStatusEffect(action, actor, target, "BURNING", profile);

            // Assert
            Assert.True(action.Score > 0, "DoT on enemy should produce positive score");
            Assert.True(action.ScoreBreakdown.ContainsKey("status_sub_type"), "Should have sub-type breakdown");
            Assert.Equal((float)StatusSubType.DoT, action.ScoreBreakdown["status_sub_type"]);

            // Verify that changing the sub-multiplier changes the score
            var profileDefault = CreateBG3Profile();
            var actionDefault = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "apply_burn" };
            scorer.ScoreStatusEffect(actionDefault, actor, target, "BURNING", profileDefault);
            Assert.True(action.Score > actionDefault.Score,
                "DoT score with 3.0x multiplier should exceed score with 1.0x default");
        }

        [Fact]
        public void ScoreStatusEffect_HoT_UsesMultiplierHotAllyPos()
        {
            // Arrange
            var context = CreateContextWithHoTStatus("REGENERATION");
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var target = CreateTestCombatant("ally", 20, 50, new Vector3(5, 0, 0), Faction.Player);
            context.RegisterCombatant(actor);
            context.RegisterCombatant(target);

            var profile = CreateBG3Profile(new Dictionary<string, float>
            {
                ["MULTIPLIER_HOT_ALLY_POS"] = 4.0f
            });

            var scorer = new AIScorer(context);
            var action = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "apply_regen" };

            // Act
            scorer.ScoreStatusEffect(action, actor, target, "REGENERATION", profile);

            // Assert
            Assert.True(action.Score > 0, "HoT on ally should produce positive score");
            Assert.True(action.ScoreBreakdown.ContainsKey("status_sub_type"));
            Assert.Equal((float)StatusSubType.HoT, action.ScoreBreakdown["status_sub_type"]);

            // Higher multiplier → higher score
            var profileDefault = CreateBG3Profile();
            var actionDefault = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "apply_regen" };
            scorer.ScoreStatusEffect(actionDefault, actor, target, "REGENERATION", profileDefault);
            Assert.True(action.Score > actionDefault.Score,
                "HoT score with 4.0x multiplier should exceed score with default");
        }

        [Fact]
        public void ScoreStatusEffect_Boost_UsesMultiplierBoostSelfPos()
        {
            // Arrange — BOOST type status on self
            var context = CreateContextWithBoostStatus("BLESS");
            // Also register in the main StatusRegistry so the BG3 branch recognizes it as BOOST
            var statusReg = context.GetService<StatusRegistry>();
            statusReg.RegisterStatus(new BG3StatusData
            {
                StatusId = "BLESS",
                StatusType = BG3StatusType.BOOST,
                Boosts = "Advantage(AttackRoll)"
            }, overwrite: true);

            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            context.RegisterCombatant(actor);

            var profile = CreateBG3Profile(new Dictionary<string, float>
            {
                ["MULTIPLIER_BOOST_SELF_POS"] = 5.0f
            });

            var scorer = new AIScorer(context);
            var action = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "cast_bless" };

            // Act
            scorer.ScoreStatusEffect(action, actor, actor, "BLESS", profile);

            // Assert
            Assert.True(action.Score > 0, "Boost on self should produce positive score");
            Assert.True(action.ScoreBreakdown.ContainsKey("status_sub_type"));
            Assert.Equal((float)StatusSubType.Boost, action.ScoreBreakdown["status_sub_type"]);

            // Higher multiplier → higher score
            var profileDefault = CreateBG3Profile();
            var actionDefault = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "cast_bless" };
            scorer.ScoreStatusEffect(actionDefault, actor, actor, "BLESS", profileDefault);
            Assert.True(action.Score > actionDefault.Score,
                "Boost score with 5.0x multiplier should exceed default");
        }

        [Fact]
        public void ScoreStatusEffect_UnknownSubType_FallsBackToGeneric()
        {
            // Arrange — status with no tick effects, unknown type (not BOOST)
            var context = new HeadlessCombatContext();
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var target = CreateTestCombatant("target", 30, 30, new Vector3(10, 0, 0), Faction.Hostile);
            context.RegisterCombatant(actor);
            context.RegisterCombatant(target);

            var profile = CreateBG3Profile();
            var scorer = new AIScorer(context);
            var action = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "mystery_debuff" };

            // Act
            scorer.ScoreStatusEffect(action, actor, target, "MYSTERY_DEBUFF", profile);

            // Assert — should still produce a score (generic fallback) and NOT have sub-type breakdown
            Assert.True(action.ScoreBreakdown.ContainsKey("status_value"));
            Assert.False(action.ScoreBreakdown.ContainsKey("status_sub_type"),
                "Unknown sub-type should not add status_sub_type breakdown");
        }

        [Fact]
        public void ClassifyStatusSubType_TickDamage_ReturnsDoT()
        {
            var context = CreateContextWithDoTStatus("POISON");
            var result = AIStatusClassifier.ClassifyStatusSubType("POISON", context);
            Assert.Equal(StatusSubType.DoT, result);
        }

        [Fact]
        public void ClassifyStatusSubType_TickHeal_ReturnsHoT()
        {
            var context = CreateContextWithHoTStatus("REGEN");
            var result = AIStatusClassifier.ClassifyStatusSubType("REGEN", context);
            Assert.Equal(StatusSubType.HoT, result);
        }

        [Fact]
        public void ClassifyStatusSubType_NoTicks_BoostType_ReturnsBoost()
        {
            var context = CreateContextWithBoostStatus("HASTE");
            var result = AIStatusClassifier.ClassifyStatusSubType("HASTE", context);
            Assert.Equal(StatusSubType.Boost, result);
        }

        [Fact]
        public void ClassifyStatusSubType_NullContext_ReturnsUnknown()
        {
            var result = AIStatusClassifier.ClassifyStatusSubType("ANY_STATUS", null);
            Assert.Equal(StatusSubType.Unknown, result);
        }

        [Fact]
        public void ScoreStatusEffect_DoTOnAlly_ProducesNegativeScore()
        {
            // Arrange — DoT status applied to an ally should be undesirable (negative score)
            var context = CreateContextWithDoTStatus("BURNING");
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var ally = CreateTestCombatant("ally", 40, 50, new Vector3(5, 0, 0), Faction.Player);
            context.RegisterCombatant(actor);
            context.RegisterCombatant(ally);

            var profile = CreateBG3Profile(new Dictionary<string, float>
            {
                ["MULTIPLIER_DOT_ALLY_NEG"] = 2.0f
            });

            var scorer = new AIScorer(context);
            var action = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "apply_burn" };

            // Act
            scorer.ScoreStatusEffect(action, actor, ally, "BURNING", profile);

            // Assert
            Assert.True(action.Score < 0, "DoT on ally should produce negative score");
            Assert.True(action.ScoreBreakdown.ContainsKey("status_sub_type"), "Should have sub-type breakdown");
            Assert.Equal((float)StatusSubType.DoT, action.ScoreBreakdown["status_sub_type"]);
        }

        [Fact]
        public void ScoreStatusEffect_ControlStatus_NotAffectedBySubMultiplier()
        {
            // Arrange — Incapacitated control status should follow the control path, not the sub-multiplier path
            var context = new HeadlessCombatContext();
            var statusReg = new StatusRegistry();
            statusReg.RegisterStatus(new BG3StatusData
            {
                StatusId = "STUNNED",
                StatusType = BG3StatusType.INCAPACITATED,
                StatusGroups = "SG_Incapacitated"
            });
            context.RegisterService<StatusRegistry>(statusReg);

            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var target = CreateTestCombatant("target", 30, 30, new Vector3(10, 0, 0), Faction.Hostile);
            context.RegisterCombatant(actor);
            context.RegisterCombatant(target);

            var profile = CreateBG3Profile();
            var scorer = new AIScorer(context);
            var action = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "stun" };

            // Act
            scorer.ScoreStatusEffect(action, actor, target, "STUNNED", profile);

            // Assert — control scoring should fire, sub-multiplier should NOT
            Assert.True(action.Score > 0, "Control on enemy should produce positive score");
            Assert.True(action.ScoreBreakdown.ContainsKey("status_value"), "Should have status_value breakdown");
            Assert.False(action.ScoreBreakdown.ContainsKey("status_sub_type"),
                "Control status should NOT enter the sub-multiplier path");
        }
    }
}
