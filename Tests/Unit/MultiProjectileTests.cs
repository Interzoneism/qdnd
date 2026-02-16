using System.Collections.Generic;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Rules;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for multi-projectile spell support (Magic Missile, Scorching Ray, Eldritch Blast).
    /// Verifies separate attack rolls and hit-miss logic for each projectile.
    /// </summary>
    public class MultiProjectileTests
    {
        [Fact]
        public void EffectPipeline_MagicMissile_Fires3Darts_AutoHit()
        {
            // Arrange
            var pipeline = CreatePipeline();
            var caster = CreateCaster();
            var target = CreateTarget();
            
            var magicMissile = new ActionDefinition
            {
                Id = "Projectile_MagicMissile",
                Name = "Magic Missile",
                TargetType = TargetType.SingleUnit,
                ProjectileCount = 3,  // Magic Missile fires 3 darts at level 1
                AttackType = null,  // Auto-hit (no attack roll)
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "1d4+1",
                        DamageType = "force"
                    }
                }
            };
            
            pipeline.RegisterAction(magicMissile);
            
            // Act
            var result = pipeline.ExecuteAction("Projectile_MagicMissile", caster, new List<Combatant> { target });
            
            // Assert
            Assert.True(result.Success);
            // Each dart creates a separate effect result
            Assert.Equal(3, result.EffectResults.Count);
            // Each dart deals 1d4+1 (2-5 damage)
            foreach (var effectResult in result.EffectResults)
            {
                Assert.Equal("damage", effectResult.EffectType);
                Assert.InRange(effectResult.Value, 2, 5);
            }
            // Attack result should be null (auto-hit, no roll)
            Assert.Null(result.AttackResult);
        }
        
        [Fact]
        public void EffectPipeline_ScorchingRay_Fires3Rays_EachWithAttackRoll()
        {
            // Arrange — use seeded RNG for deterministic results
            var pipeline = CreateSeededPipeline(seed: 42);
            var caster = CreateCaster();
            var target = CreateTarget();
            
            var scorchingRay = new ActionDefinition
            {
                Id = "Projectile_ScorchingRay",
                Name = "Scorching Ray",
                TargetType = TargetType.SingleUnit,
                ProjectileCount = 3,  // Scorching Ray fires 3 rays at level 2
                AttackType = AttackType.RangedSpell,  // Each ray requires attack roll
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "2d6",
                        DamageType = "fire",
                        Condition = "on_hit"
                    }
                }
            };
            
            pipeline.RegisterAction(scorchingRay);
            
            // Act
            var result = pipeline.ExecuteAction("Projectile_ScorchingRay", caster, new List<Combatant> { target });
            
            // Assert
            Assert.True(result.Success);
            // Each ray produces an effect result (hit or miss), so we should get 3 total
            Assert.Equal(3, result.EffectResults.Count);
            // All should be damage type
            foreach (var effectResult in result.EffectResults)
            {
                Assert.Equal("damage", effectResult.EffectType);
            }
            // Successful hits deal 2d6 (2-12 damage); misses have Value=0
            foreach (var effectResult in result.EffectResults)
            {
                if (effectResult.Success)
                    Assert.InRange(effectResult.Value, 2, 12);
                else
                    Assert.Equal(0, effectResult.Value);
            }
        }
        
        [Fact]
        public void ActionDefinition_ProjectileCount_DefaultsToOne()
        {
            // Arrange & Act
            var action = new ActionDefinition
            {
                Id = "Test_SingleTarget",
                Name = "Single Target"
            };
            
            // Assert
            Assert.Equal(1, action.ProjectileCount);
        }
        
        [Fact]
        public void UpcastScaling_ProjectilesPerLevel_CanBeSet()
        {
            // Arrange & Act
            var upcastScaling = new UpcastScaling
            {
                ProjectilesPerLevel = 1,
                MaxUpcastLevel = 9
            };
            
            var magicMissile = new ActionDefinition
            {
                Id = "Projectile_MagicMissile",
                Name = "Magic Missile",
                ProjectileCount = 3,
                UpcastScaling = upcastScaling
            };
            
            // Assert - verify the property exists and can be set
            Assert.Equal(1, magicMissile.UpcastScaling.ProjectilesPerLevel);
            Assert.Equal(9, magicMissile.UpcastScaling.MaxUpcastLevel);
            Assert.Equal(3, magicMissile.ProjectileCount);
        }
        
        [Fact]
        public void EffectPipeline_MagicMissile_UpcastLevel4_Fires4Darts()
        {
            // Arrange
            var pipeline = CreatePipeline();
            var caster = CreateCaster();
            caster.ResourcePool.SetMax("spell_slot_2", 3);
            
            var target = CreateTarget();
            
            var magicMissile = new ActionDefinition
            {
                Id = "Projectile_MagicMissile",
                Name = "Magic Missile",
                SpellLevel = 1,
                CanUpcast = true,
                TargetType = TargetType.SingleUnit,
                ProjectileCount = 3,  // Base 3 darts at level 1
                AttackType = null,  // Auto-hit
                UpcastScaling = new UpcastScaling
                {
                    ProjectilesPerLevel = 1,  // +1 dart per level
                    MaxUpcastLevel = 9
                },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "1d4+1",
                        DamageType = "force"
                    }
                },
                Cost = new ActionCost
                {
                    UsesAction = true,
                    ResourceCosts = new Dictionary<string, int>
                    {
                        { "spell_slot_1", 1 }
                    }
                }
            };
            
            pipeline.RegisterAction(magicMissile);
            
            // Act - Upcast to level 2 (+1 level = +1 dart)
            var result = pipeline.ExecuteAction("Projectile_MagicMissile", caster, new List<Combatant> { target },
                new ActionExecutionOptions { UpcastLevel = 1 });
            
            // Assert
            Assert.True(result.Success);
            Assert.Equal(4, result.EffectResults.Count); // 3 base + 1 upcast
        }
        
        [Fact]
        public void EffectPipeline_ScorchingRay_PerBeamIndependentRolls_CanHaveVariedResults()
        {
            // Arrange — Run many seeds to find a case where NOT all beams have the same
            // hit/miss outcome, proving each beam rolls independently.
            // The RulesEngine uses its own internal dice, so we just need enough seeds
            // to demonstrate variance.
            var caster = CreateCaster();
            var target = CreateTarget();
            
            var scorchingRay = new ActionDefinition
            {
                Id = "Projectile_ScorchingRay",
                Name = "Scorching Ray",
                TargetType = TargetType.SingleUnit,
                ProjectileCount = 3,
                AttackType = AttackType.RangedSpell,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "2d6",
                        DamageType = "fire",
                        Condition = "on_hit"
                    }
                }
            };
            
            // Act — Try different pipelines (each creates a fresh RulesEngine with fresh dice)
            bool foundMixed = false;
            for (int i = 0; i < 200; i++)
            {
                var pipeline = CreatePipeline();
                pipeline.RegisterAction(scorchingRay);
                
                var result = pipeline.ExecuteAction("Projectile_ScorchingRay", caster, new List<Combatant> { target });
                
                // Count hits vs misses
                int hits = result.EffectResults.Count(r => r.Success);
                int misses = result.EffectResults.Count(r => !r.Success);
                
                // If we have a mix of hits and misses, beams are independent
                if (hits > 0 && misses > 0)
                {
                    foundMixed = true;
                    break;
                }
            }
            
            // Assert — With 200 trials, we should find at least one mixed result
            Assert.True(foundMixed, "Expected to find at least one case with mixed hit/miss results across 200 trials");
        }
        
        // Helper methods
        
        private EffectPipeline CreatePipeline()
        {
            return new EffectPipeline
            {
                Rules = new RulesEngine()
            };
        }
        
        private EffectPipeline CreateSeededPipeline(int seed)
        {
            return new EffectPipeline
            {
                Rules = new RulesEngine(),
                Rng = new System.Random(seed)
            };
        }
        
        private Combatant CreateCaster()
        {
            var caster = new Combatant("caster", "Wizard", Faction.Player, 30, 14);
            caster.Stats = new CombatantStats
            {
                Intelligence = 16
            };
            caster.ResolvedCharacter = new Data.CharacterModel.ResolvedCharacter
            {
                Sheet = new Data.CharacterModel.CharacterSheet { Name = "Wizard" }
            };
            return caster;
        }
        
        private Combatant CreateTarget()
        {
            var target = new Combatant("target", "Goblin", Faction.Hostile, 20, 10);
            target.Stats = new CombatantStats
            {
                BaseAC = 12
            };
            target.ResolvedCharacter = new Data.CharacterModel.ResolvedCharacter
            {
                Sheet = new Data.CharacterModel.CharacterSheet { Name = "Goblin" }
            };
            return target;
        }
    }
}
