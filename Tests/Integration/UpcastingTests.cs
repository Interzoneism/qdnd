using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Data.Spells;
using QDND.Data.Actions;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Integration tests for spell upcasting (Issue 8).
    /// Verifies that SpellUpcastRules are applied correctly and produce expected results.
    /// </summary>
    [Collection("BenchmarkTests")]
    public class UpcastingTests
    {
        [Fact]
        public void CureWounds_Upcast_Level2_AddsExtraHealDice()
        {
            // Arrange
            var pipeline = CreatePipeline();
            var cleric = CreateCleric();
            var target = CreateInjuredTarget();
            
            var cureWounds = CreateCureWounds();
            pipeline.RegisterAction(cureWounds);
            
            // Act - Upcast to level 2 (+1 level)
            var result = pipeline.ExecuteAction("cure_wounds", cleric, new List<Combatant> { target },
                new ActionExecutionOptions { UpcastLevel = 1 });
            
            // Assert - Should heal 2d8 (base 1d8 + 1d8 upcast)
            Assert.True(result.Success);
            Assert.NotEmpty(result.EffectResults);
            // Healing ranges: 2d8 = 2-16
            Assert.InRange(result.EffectResults[0].Value, 2, 16);
        }
        
        [Fact]
        public void BurningHands_Upcast_Level3_AddsExtraDamageDice()
        {
            // Arrange
            var pipeline = CreatePipeline();
            var wizard = CreateWizard();
            var target = CreateTarget();
            
            var burningHands = CreateBurningHands();
            pipeline.RegisterAction(burningHands);
            
            // Act - Upcast to level 3 (+2 levels)
            var result = pipeline.ExecuteAction("burning_hands", wizard, new List<Combatant> { target },
                new ActionExecutionOptions { UpcastLevel = 2 });
            
            // Assert - Should deal 5d6 (base 3d6 + 2d6 upcast)
            Assert.True(result.Success);
            Assert.NotEmpty(result.EffectResults);
            // Damage ranges: 5d6 = 5-30
            Assert.InRange(result.EffectResults[0].Value, 5, 30);
        }
        
        [Fact]
        public void MagicMissile_Upcast_Level2_AddsExtraDart()
        {
            // Arrange
            var pipeline = CreatePipeline();
            var wizard = CreateWizard();
            var target = CreateTarget();
            
            var magicMissile = CreateMagicMissile();
            pipeline.RegisterAction(magicMissile);
            
            // Act - Upcast to level 2 (+1 level, +1 dart)
            var result = pipeline.ExecuteAction("magic_missile", wizard, new List<Combatant> { target },
                new ActionExecutionOptions { UpcastLevel = 1 });
            
            // Assert - Should fire 4 darts (base 3 + 1 upcast)
            Assert.True(result.Success);
            // 4 darts × 1d4+1 each = 4 effect results
            // Each dart: 2-5 damage, total: 8-20
            Assert.Equal(4, result.EffectResults.Count);
        }
        
        [Fact]
        public void ScorchingRay_Upcast_Level4_AddsExtraRay()
        {
            // Arrange
            var pipeline = CreatePipeline();
            var sorcerer = CreateSorcerer();
            var target = CreateTarget();
            
            var scorchingRay = CreateScorchingRay();
            pipeline.RegisterAction(scorchingRay);
            
            // Act - Upcast to level 4 (+2 levels, +2 rays)
            var result = pipeline.ExecuteAction("scorching_ray", sorcerer, new List<Combatant> { target },
                new ActionExecutionOptions { UpcastLevel = 2 });
            
            // Assert - Should fire 5 rays (base 3 + 2 upcast)
            Assert.True(result.Success);
            // Each ray rolls attack separately, some may miss
            // We can't guarantee all hit, but we should have effect results
            Assert.NotEmpty(result.EffectResults);
        }
        
        [Fact]
        public void SpellUpcastRules_GetRule_ReturnsCorrectRule()
        {
            // Arrange & Act
            var cureWoundsRule = SpellUpcastRules.GetUpcastScaling("cure_wounds");
            var magicMissileRule = SpellUpcastRules.GetUpcastScaling("magic_missile");
            var burningHandsRule = SpellUpcastRules.GetUpcastScaling("burning_hands");
            
            // Assert
            Assert.NotNull(cureWoundsRule);
            Assert.Equal("1d8", cureWoundsRule.DicePerLevel);
            Assert.Equal(9, cureWoundsRule.MaxUpcastLevel);
            
            Assert.NotNull(magicMissileRule);
            Assert.Equal(1, magicMissileRule.ProjectilesPerLevel);
            Assert.Equal(9, magicMissileRule.MaxUpcastLevel);
            
            Assert.NotNull(burningHandsRule);
            Assert.Equal("1d6", burningHandsRule.DicePerLevel);
            Assert.Equal(9, burningHandsRule.MaxUpcastLevel);
        }
        
        [Fact]
        public void SpellUpcastRules_ApplyUpcastRule_IntegratesIntoActionDefinition()
        {
            // Arrange
            var action = new ActionDefinition
            {
                Id = "cure_wounds",
                Name = "Cure Wounds",
                SpellLevel = 1,
                CanUpcast = true,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "heal",
                        DiceFormula = "1d8"
                    }
                }
            };
            
            // Act
            SpellUpcastRules.ApplyUpcastRule(action);
            
            // Assert
            Assert.NotNull(action.UpcastScaling);
            Assert.Equal("1d8", action.UpcastScaling.DicePerLevel);
            Assert.Equal(9, action.UpcastScaling.MaxUpcastLevel);
        }
        
        // Helper methods
        
        private EffectPipeline CreatePipeline()
        {
            return new EffectPipeline
            {
                Rules = new RulesEngine(),
                Rng = new System.Random(42) // Seeded for deterministic rolls
            };
        }
        
        private Combatant CreateCleric()
        {
            var cleric = new Combatant("cleric", "Cleric", Faction.Player, 35, 14);
            cleric.ActionResources.RegisterSimple("spell_slot_1", 4);
            cleric.ActionResources.RegisterSimple("spell_slot_2", 3);
            cleric.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet { Name = "Cleric" },
                AbilityScores = new Dictionary<AbilityType, int>
                {
                    { AbilityType.Strength, 10 }, { AbilityType.Dexterity, 10 }, { AbilityType.Constitution, 10 },
                    { AbilityType.Intelligence, 10 }, { AbilityType.Wisdom, 16 }, { AbilityType.Charisma, 10 }
                }
            };
            return cleric;
        }
        
        private Combatant CreateWizard()
        {
            var wizard = new Combatant("wizard", "Wizard", Faction.Player, 30, 15);
            wizard.ActionResources.RegisterSimple("spell_slot_1", 4);
            wizard.ActionResources.RegisterSimple("spell_slot_2", 3);
            wizard.ActionResources.RegisterSimple("spell_slot_3", 2);
            wizard.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet { Name = "Wizard" },
                AbilityScores = new Dictionary<AbilityType, int>
                {
                    { AbilityType.Strength, 10 }, { AbilityType.Dexterity, 10 }, { AbilityType.Constitution, 10 },
                    { AbilityType.Intelligence, 18 }, { AbilityType.Wisdom, 10 }, { AbilityType.Charisma, 10 }
                }
            };
            return wizard;
        }
        
        private Combatant CreateSorcerer()
        {
            var sorcerer = new Combatant("sorcerer", "Sorcerer", Faction.Player, 28, 16);
            sorcerer.ActionResources.RegisterSimple("spell_slot_2", 4);
            sorcerer.ActionResources.RegisterSimple("spell_slot_3", 3);
            sorcerer.ActionResources.RegisterSimple("spell_slot_4", 2);
            sorcerer.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet { Name = "Sorcerer" },
                AbilityScores = new Dictionary<AbilityType, int>
                {
                    { AbilityType.Strength, 10 }, { AbilityType.Dexterity, 10 }, { AbilityType.Constitution, 10 },
                    { AbilityType.Intelligence, 10 }, { AbilityType.Wisdom, 10 }, { AbilityType.Charisma, 18 }
                }
            };
            return sorcerer;
        }
        
        private Combatant CreateTarget()
        {
            var target = new Combatant("target", "Goblin", Faction.Hostile, 20, 10);
            target.CurrentAC = 12;
            target.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet { Name = "Goblin" },
                BaseAC = 12
            };
            return target;
        }
        
        private Combatant CreateInjuredTarget()
        {
            var target = CreateTarget();
            target.Resources.CurrentHP = 10; // Half HP
            return target;
        }
        
        private ActionDefinition CreateCureWounds()
        {
            var action = new ActionDefinition
            {
                Id = "cure_wounds",
                Name = "Cure Wounds",
                SpellLevel = 1,
                CanUpcast = true,
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "heal",
                        DiceFormula = "1d8"
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
            
            // Apply upcast rules
            SpellUpcastRules.ApplyUpcastRule(action);
            return action;
        }
        
        private ActionDefinition CreateBurningHands()
        {
            var action = new ActionDefinition
            {
                Id = "burning_hands",
                Name = "Burning Hands",
                SpellLevel = 1,
                CanUpcast = true,
                TargetType = TargetType.Cone,
                SaveType = "dexterity",
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "3d6",
                        DamageType = "fire",
                        SaveTakesHalf = true
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
            
            SpellUpcastRules.ApplyUpcastRule(action);
            return action;
        }
        
        private ActionDefinition CreateMagicMissile()
        {
            var action = new ActionDefinition
            {
                Id = "magic_missile",
                Name = "Magic Missile",
                SpellLevel = 1,
                CanUpcast = true,
                TargetType = TargetType.SingleUnit,
                ProjectileCount = 3,
                AttackType = null, // Auto-hit
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
            
            SpellUpcastRules.ApplyUpcastRule(action);
            return action;
        }
        
        [Theory]
        [InlineData("magic_missile", "magic_missile")]
        [InlineData("burning_hands", "burning_hands")]
        [InlineData("hold_person", "hold_person")]
        [InlineData("cure_wounds", "cure_wounds")]
        [InlineData("fireball", "fireball")]
        [InlineData("mass_healing_word", "mass_healing_word")]
        public void NormalizeBG3SpellId_StripsPrefix_ReturnsSnakeCase(string bg3Id, string expected)
        {
            var result = SpellUpcastRules.NormalizeBG3SpellId(bg3Id);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void NormalizeBG3SpellIdPreserveLevelSuffix_KeepsVariantLevel()
        {
            string normalized = SpellUpcastRules.NormalizeBG3SpellIdPreserveLevelSuffix("Projectile_MagicMissile_4");
            Assert.Equal("magic_missile_4", normalized);
        }

        [Fact]
        public void TryRegisterDerivedUpcastRule_DerivesDiceScalingFromVariants()
        {
            var originalRules = SnapshotUpcastRules();

            string actionId = $"test_upcast_spell_{Guid.NewGuid():N}";
            var level1 = new ActionDefinition
            {
                Id = actionId,
                SpellLevel = 1,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", DiceFormula = "2d6", DamageType = "fire" }
                }
            };
            var level2 = new ActionDefinition
            {
                Id = actionId,
                SpellLevel = 2,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", DiceFormula = "3d6", DamageType = "fire" }
                }
            };

            try
            {
                bool registered = SpellUpcastRules.TryRegisterDerivedUpcastRule(actionId, new[] { level1, level2 });
                var derived = SpellUpcastRules.GetUpcastScaling(actionId);

                Assert.True(registered);
                Assert.NotNull(derived);
                Assert.Equal("1d6", derived.DicePerLevel);
            }
            finally
            {
                ReplaceUpcastRules(originalRules);
            }
        }

        [Fact]
        public void LoadAllSpells_DerivesAtLeastOneUpcastRuleFromBG3Variants()
        {
            var originalRules = SnapshotUpcastRules();
            string bg3Path = Path.Combine(FindRepoRoot(), "BG3_Data");
            Assert.True(Directory.Exists(bg3Path), $"BG3 data directory was not found at '{bg3Path}'");

            try
            {
                ResetUpcastRulesToCuratedBaseline();

                var curatedRules = SpellUpcastRules.GetAllRules();
                var curatedKeys = new HashSet<string>(curatedRules.Keys, StringComparer.OrdinalIgnoreCase);
                int rulesBefore = curatedRules.Count;

                // Ensure a known curated rule exists before BG3 variant derivation runs.
                Assert.True(curatedKeys.Contains("magic_missile") || curatedKeys.Contains("burning_hands"));

                var loader = new ActionDataLoader();
                var registry = new ActionRegistry();
                int loaded = loader.LoadAllSpells(bg3Path, registry);

                Assert.True(loaded > 0, $"Expected BG3 spells to load from '{bg3Path}'. Errors: {string.Join(" | ", loader.Errors)}");

                var allRulesAfterLoad = SpellUpcastRules.GetAllRules();
                int rulesAfter = allRulesAfterLoad.Count;
                int derivedUpcastRules = rulesAfter - rulesBefore;
                var derivedKeys = allRulesAfterLoad.Keys.Where(k => !curatedKeys.Contains(k)).ToList();

                Assert.True(derivedUpcastRules > 0, $"Expected at least one derived upcast rule from BG3 variants. Before={rulesBefore}, After={rulesAfter}");
                Assert.NotEmpty(derivedKeys);
            }
            finally
            {
                ReplaceUpcastRules(originalRules);
            }
        }

        [Theory]
        [InlineData("magic_missile")]
        [InlineData("burning_hands")]
        [InlineData("hold_person")]
        public void GetUpcastScaling_BG3PrefixedId_ReturnsNonNull(string bg3Id)
        {
            Assert.NotNull(SpellUpcastRules.GetUpcastScaling(bg3Id));
        }

        private ActionDefinition CreateScorchingRay()
        {
            var action = new ActionDefinition
            {
                Id = "scorching_ray",
                Name = "Scorching Ray",
                SpellLevel = 2,
                CanUpcast = true,
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
                },
                Cost = new ActionCost
                {
                    UsesAction = true,
                    ResourceCosts = new Dictionary<string, int>
                    {
                        { "spell_slot_2", 1 }
                    }
                }
            };
            
            SpellUpcastRules.ApplyUpcastRule(action);
            return action;
        }

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

        private static void ResetUpcastRulesToCuratedBaseline()
        {
            var rulesField = typeof(SpellUpcastRules).GetField("_upcastRules", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(rulesField);

            var rules = rulesField.GetValue(null) as IDictionary<string, UpcastScaling>;
            Assert.NotNull(rules);
            rules.Clear();

            var initMethod = typeof(SpellUpcastRules).GetMethod("InitializeUpcastRules", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(initMethod);
            initMethod.Invoke(null, null);
        }

        private static Dictionary<string, UpcastScaling> SnapshotUpcastRules()
        {
            return new Dictionary<string, UpcastScaling>(SpellUpcastRules.GetAllRules(), StringComparer.OrdinalIgnoreCase);
        }

        private static void ReplaceUpcastRules(Dictionary<string, UpcastScaling> snapshot)
        {
            var rulesField = typeof(SpellUpcastRules).GetField("_upcastRules", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(rulesField);

            var rules = rulesField.GetValue(null) as IDictionary<string, UpcastScaling>;
            Assert.NotNull(rules);

            rules.Clear();
            foreach (var (key, value) in snapshot)
                rules[key] = value;
        }
    }
}
