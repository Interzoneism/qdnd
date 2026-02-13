using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Data.CharacterModel;
using System.Collections.Generic;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for Wild Shape transformation system.
    /// </summary>
    public class WildShapeTransformationTests
    {
        private static StatusManager CreateStatusManager(RulesEngine rulesEngine)
        {
            var statusManager = new StatusManager(rulesEngine);
            statusManager.RegisterStatus(new StatusDefinition
            {
                Id = "wild_shape_active",
                Name = "Wild Shape Active",
                DurationType = DurationType.Permanent,
                DefaultDuration = 0
            });
            return statusManager;
        }

        [Fact]
        public void TransformEffect_AppliesBeastStats()
        {
            // Arrange
            var druid = CreateTestDruid();
            var originalStr = druid.Stats.Strength;
            var originalDex = druid.Stats.Dexterity;
            var originalCon = druid.Stats.Constitution;
            
            var beastForm = new BeastForm
            {
                Id = "wolf",
                Name = "Wolf",
                StrengthOverride = 12,
                DexterityOverride = 15,
                ConstitutionOverride = 12,
                BaseHP = 11,
                AC = 13,
                MovementSpeed = 40f,
                GrantedAbilities = new List<string> { "bite" }
            };

            var effectDef = new EffectDefinition
            {
                Type = "transform",
                Parameters = new Dictionary<string, object>
                {
                    { "beastForm", beastForm }
                }
            };

            var context = CreateEffectContext(druid);
            var effect = new TransformEffect();

            // Act
            var results = effect.Execute(effectDef, context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.Equal(12, druid.Stats.Strength);
            Assert.Equal(15, druid.Stats.Dexterity);
            Assert.Equal(12, druid.Stats.Constitution);
        }

        [Fact]
        public void TransformEffect_GrantsBeastTemporaryHP()
        {
            // Arrange
            var druid = CreateTestDruid();
            druid.Resources.TakeDamage(10); // Druid at 40/50 HP
            
            var beastForm = new BeastForm
            {
                Id = "bear",
                Name = "Bear",
                BaseHP = 19,
                StrengthOverride = 15,
                DexterityOverride = 10,
                ConstitutionOverride = 14
            };

            var effectDef = new EffectDefinition
            {
                Type = "transform",
                Parameters = new Dictionary<string, object>
                {
                    { "beastForm", beastForm }
                }
            };

            var context = CreateEffectContext(druid);
            var effect = new TransformEffect();

            // Act
            effect.Execute(effectDef, context);

            // Assert
            Assert.Equal(19, druid.Resources.TemporaryHP); // Beast HP becomes temp HP
            Assert.Equal(40, druid.Resources.CurrentHP); // Original HP unchanged
        }

        [Fact]
        public void TransformEffect_AppliesWildShapeActiveStatus()
        {
            // Arrange
            var druid = CreateTestDruid();
            var rulesEngine = new RulesEngine();
            var statusManager = CreateStatusManager(rulesEngine);
            
            var beastForm = new BeastForm
            {
                Id = "wolf",
                Name = "Wolf",
                BaseHP = 11,
                StrengthOverride = 12,
                DexterityOverride = 15,
                ConstitutionOverride = 12
            };

            var effectDef = new EffectDefinition
            {
                Type = "transform",
                Parameters = new Dictionary<string, object>
                {
                    { "beastForm", beastForm }
                }
            };

            var context = CreateEffectContext(druid);
            context.Statuses = statusManager;
            var effect = new TransformEffect();

            // Act
            effect.Execute(effectDef, context);

            // Assert
            Assert.True(statusManager.HasStatus(druid.Id, "wild_shape_active"));
        }

        [Fact]
        public void TransformEffect_GrantsBeastAbilities()
        {
            // Arrange
            var druid = CreateTestDruid();
            var originalAbilityCount = druid.KnownActions.Count;
            
            var beastForm = new BeastForm
            {
                Id = "wolf",
                Name = "Wolf",
                BaseHP = 11,
                StrengthOverride = 12,
                DexterityOverride = 15,
                ConstitutionOverride = 12,
                GrantedAbilities = new List<string> { "bite", "pounce" }
            };

            var effectDef = new EffectDefinition
            {
                Type = "transform",
                Parameters = new Dictionary<string, object>
                {
                    { "beastForm", beastForm }
                }
            };

            var context = CreateEffectContext(druid);
            var effect = new TransformEffect();

            // Act
            effect.Execute(effectDef, context);

            // Assert
            Assert.Contains("bite", druid.KnownActions);
            Assert.Contains("pounce", druid.KnownActions);
        }

        [Fact]
        public void RevertTransformEffect_RestoresOriginalStats()
        {
            // Arrange
            var druid = CreateTestDruid();
            var rulesEngine = new RulesEngine();
            var statusManager = CreateStatusManager(rulesEngine);
            var originalStr = druid.Stats.Strength;
            var originalDex = druid.Stats.Dexterity;
            var originalCon = druid.Stats.Constitution;

            // Transform first
            var beastForm = new BeastForm
            {
                Id = "wolf",
                Name = "Wolf",
                BaseHP = 11,
                StrengthOverride = 12,
                DexterityOverride = 15,
                ConstitutionOverride = 12,
                GrantedAbilities = new List<string> { "bite" }
            };

            var transformDef = new EffectDefinition
            {
                Type = "transform",
                Parameters = new Dictionary<string, object>
                {
                    { "beastForm", beastForm }
                }
            };

            var transformContext = CreateEffectContext(druid);
            transformContext.Statuses = statusManager;
            new TransformEffect().Execute(transformDef, transformContext);

            // Act - Revert
            var revertDef = new EffectDefinition
            {
                Type = "revert_transform"
            };
            var revertContext = CreateEffectContext(druid);
            revertContext.Statuses = statusManager;
            var revertEffect = new RevertTransformEffect();
            var results = revertEffect.Execute(revertDef, revertContext);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.Equal(originalStr, druid.Stats.Strength);
            Assert.Equal(originalDex, druid.Stats.Dexterity);
            Assert.Equal(originalCon, druid.Stats.Constitution);
            Assert.False(statusManager.HasStatus(druid.Id, "wild_shape_active"));
        }

        [Fact]
        public void RevertTransformEffect_RemovesBeastTemporaryHP()
        {
            // Arrange
            var druid = CreateTestDruid();
            var rulesEngine = new RulesEngine();
            var statusManager = CreateStatusManager(rulesEngine);

            // Transform first
            var beastForm = new BeastForm
            {
                Id = "bear",
                Name = "Bear",
                BaseHP = 19,
                StrengthOverride = 15,
                DexterityOverride = 10,
                ConstitutionOverride = 14
            };

            var transformDef = new EffectDefinition
            {
                Type = "transform",
                Parameters = new Dictionary<string, object>
                {
                    { "beastForm", beastForm }
                }
            };

            var transformContext = CreateEffectContext(druid);
            transformContext.Statuses = statusManager;
            new TransformEffect().Execute(transformDef, transformContext);

            // Act - Revert
            var revertDef = new EffectDefinition
            {
                Type = "revert_transform"
            };
            var revertContext = CreateEffectContext(druid);
            revertContext.Statuses = statusManager;
            new RevertTransformEffect().Execute(revertDef, revertContext);

            // Assert
            Assert.Equal(0, druid.Resources.TemporaryHP);
        }

        [Fact]
        public void RevertTransformEffect_CarriesOverExcessDamage()
        {
            // Arrange
            var druid = CreateTestDruid();
            var rulesEngine = new RulesEngine();
            var statusManager = CreateStatusManager(rulesEngine);
            druid.Resources.TakeDamage(10); // Start at 40/50 HP

            // Transform
            var beastForm = new BeastForm
            {
                Id = "wolf",
                Name = "Wolf",
                BaseHP = 11,
                StrengthOverride = 12,
                DexterityOverride = 15,
                ConstitutionOverride = 12
            };

            var transformDef = new EffectDefinition
            {
                Type = "transform",
                Parameters = new Dictionary<string, object>
                {
                    { "beastForm", beastForm }
                }
            };

            var transformContext = CreateEffectContext(druid);
            transformContext.Statuses = statusManager;
            new TransformEffect().Execute(transformDef, transformContext);

            // Take 15 damage in beast form (11 temp HP + 4 excess)
            druid.Resources.TakeDamage(15);

            // Act - Revert
            var revertDef = new EffectDefinition
            {
                Type = "revert_transform",
                Parameters = new Dictionary<string, object>
                {
                    { "excessDamage", 4 }
                }
            };
            var revertContext = CreateEffectContext(druid);
            revertContext.Statuses = statusManager;
            new RevertTransformEffect().Execute(revertDef, revertContext);

            // Assert
            Assert.Equal(32, druid.Resources.CurrentHP);
        }

        [Fact]
        public void WildShapeAbility_CostsWildShapeCharge()
        {
            // Arrange
            var druid = CreateTestDruid();
            druid.ResourcePool.SetMax("wild_shape", 2, true);

            var action = new ActionDefinition
            {
                Id = "wild_shape_wolf",
                Name = "Wild Shape: Wolf",
                Cost = new ActionCost
                {
                    UsesAction = true,
                    ResourceCosts = new Dictionary<string, int> { { "wild_shape", 1 } }
                }
            };

            var rulesEngine = new RulesEngine();
            var pipeline = new EffectPipeline
            {
                Rules = rulesEngine,
                Statuses = CreateStatusManager(rulesEngine),
                Rng = new System.Random(42)
            };
            pipeline.RegisterAction(action);

            // Act
            var (canUse, reason) = pipeline.CanUseAbility(action.Id, druid);

            // Assert
            Assert.True(canUse);
            Assert.Null(reason);
        }

        [Fact]
        public void WildShapeAbility_FailsWithoutCharges()
        {
            // Arrange
            var druid = CreateTestDruid();
            druid.ResourcePool.SetMax("wild_shape", 2, false);
            druid.ResourcePool.ModifyCurrent("wild_shape", -2);

            var action = new ActionDefinition
            {
                Id = "wild_shape_wolf",
                Name = "Wild Shape: Wolf",
                Cost = new ActionCost
                {
                    UsesAction = true,
                    ResourceCosts = new Dictionary<string, int> { { "wild_shape", 1 } }
                }
            };

            var rulesEngine = new RulesEngine();
            var pipeline = new EffectPipeline
            {
                Rules = rulesEngine,
                Statuses = CreateStatusManager(rulesEngine),
                Rng = new System.Random(42)
            };
            pipeline.RegisterAction(action);

            // Act
            var (canUse, reason) = pipeline.CanUseAbility(action.Id, druid);

            // Assert
            Assert.False(canUse);
            Assert.Contains("wild_shape", reason);
        }

        // Helper methods
        private Combatant CreateTestDruid()
        {
            var druid = new Combatant("druid", "Test Druid", Faction.Player, 50, 15);
            druid.Stats = new CombatantStats
            {
                Strength = 10,
                Dexterity = 14,
                Constitution = 12,
                Intelligence = 13,
                Wisdom = 16,
                Charisma = 8
            };
            druid.KnownActions = new List<string> { "spellcasting", "wild_companion" };
            return druid;
        }

        private EffectContext CreateEffectContext(Combatant source)
        {
            var rulesEngine = new RulesEngine();
            return new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { source },
                Rules = rulesEngine,
                Statuses = CreateStatusManager(rulesEngine),
                Rng = new System.Random(42)
            };
        }
    }
}
