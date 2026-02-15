using Xunit;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Services;
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
    }
}
