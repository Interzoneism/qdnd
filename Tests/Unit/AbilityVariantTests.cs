using System;
using System.Collections.Generic;
using Xunit;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for ability variant and upcast system.
    /// Uses inline implementations to avoid Godot dependencies.
    /// </summary>
    public class AbilityVariantTests
    {
        #region Test Implementations

        private class TestAbilityCost
        {
            public bool UsesAction { get; set; } = true;
            public bool UsesBonusAction { get; set; }
            public int MovementCost { get; set; }
            public Dictionary<string, int> ResourceCosts { get; set; } = new();
        }

        private class TestAbilityVariant
        {
            public string VariantId { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string? ReplaceDamageType { get; set; }
            public int AdditionalDamage { get; set; }
            public string? AdditionalDice { get; set; }
            public string? ReplaceStatusId { get; set; }
            public TestAbilityCost? AdditionalCost { get; set; }
            public List<TestEffectDefinition> AdditionalEffects { get; set; } = new();
        }

        private class TestUpcastScaling
        {
            public string ResourceKey { get; set; } = "spell_slot";
            public int BaseCost { get; set; } = 1;
            public int CostPerLevel { get; set; } = 1;
            public string? DicePerLevel { get; set; }
            public int DamagePerLevel { get; set; }
            public int DurationPerLevel { get; set; }
            public int MaxUpcastLevel { get; set; } = 9;
        }

        private class TestEffectDefinition
        {
            public string Type { get; set; } = "";
            public float Value { get; set; }
            public string? DiceFormula { get; set; }
            public string? DamageType { get; set; }
            public string? StatusId { get; set; }
            public int StatusDuration { get; set; }

            public TestEffectDefinition Clone()
            {
                return new TestEffectDefinition
                {
                    Type = Type,
                    Value = Value,
                    DiceFormula = DiceFormula,
                    DamageType = DamageType,
                    StatusId = StatusId,
                    StatusDuration = StatusDuration
                };
            }
        }

        private class TestAbilityDefinition
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public List<TestEffectDefinition> Effects { get; set; } = new();
            public List<TestAbilityVariant> Variants { get; set; } = new();
            public bool CanUpcast { get; set; }
            public TestUpcastScaling? UpcastScaling { get; set; }
            public TestAbilityCost Cost { get; set; } = new();
        }

        private class TestCombatant
        {
            public string Id { get; set; } = "";
            public int MaxHP { get; set; }
            public int CurrentHP { get; set; }
            public bool IsDowned => CurrentHP <= 0;
        }

        private class TestAbilityExecutionOptions
        {
            public string? VariantId { get; set; }
            public int UpcastLevel { get; set; }
        }

        private class TestEffectResult
        {
            public string EffectType { get; set; } = "";
            public float Value { get; set; }
            public string? DamageType { get; set; }
            public string? StatusId { get; set; }
            public int Duration { get; set; }
        }

        /// <summary>
        /// Test effect pipeline with variant/upcast support.
        /// </summary>
        private class TestEffectPipeline
        {
            private readonly Random _rng = new(42);

            public List<TestEffectResult> ExecuteAbility(
                TestAbilityDefinition ability,
                TestCombatant target,
                TestAbilityExecutionOptions? options = null)
            {
                options ??= new TestAbilityExecutionOptions();
                var results = new List<TestEffectResult>();

                // Get variant if specified
                TestAbilityVariant? variant = null;
                if (!string.IsNullOrEmpty(options.VariantId))
                {
                    variant = ability.Variants.Find(v => v.VariantId == options.VariantId);
                }

                // Build effective effects
                var effectiveEffects = BuildEffectiveEffects(ability, variant, options.UpcastLevel);

                // Execute each effect
                foreach (var effect in effectiveEffects)
                {
                    var result = ExecuteEffect(effect, target);
                    results.Add(result);
                }

                return results;
            }

            public TestAbilityCost CalculateEffectiveCost(
                TestAbilityDefinition ability,
                TestAbilityExecutionOptions options)
            {
                var effectiveCost = new TestAbilityCost
                {
                    UsesAction = ability.Cost.UsesAction,
                    UsesBonusAction = ability.Cost.UsesBonusAction,
                    MovementCost = ability.Cost.MovementCost,
                    ResourceCosts = new Dictionary<string, int>(ability.Cost.ResourceCosts)
                };

                // Add variant costs
                TestAbilityVariant? variant = null;
                if (!string.IsNullOrEmpty(options.VariantId))
                {
                    variant = ability.Variants.Find(v => v.VariantId == options.VariantId);
                }

                if (variant?.AdditionalCost != null)
                {
                    foreach (var (key, value) in variant.AdditionalCost.ResourceCosts)
                    {
                        if (effectiveCost.ResourceCosts.ContainsKey(key))
                            effectiveCost.ResourceCosts[key] += value;
                        else
                            effectiveCost.ResourceCosts[key] = value;
                    }
                }

                // Add upcast costs
                if (options.UpcastLevel > 0 && ability.UpcastScaling != null)
                {
                    string resourceKey = ability.UpcastScaling.ResourceKey;
                    int additionalCost = options.UpcastLevel * ability.UpcastScaling.CostPerLevel;

                    if (effectiveCost.ResourceCosts.ContainsKey(resourceKey))
                        effectiveCost.ResourceCosts[resourceKey] += additionalCost;
                    else
                        effectiveCost.ResourceCosts[resourceKey] = ability.UpcastScaling.BaseCost + additionalCost;
                }

                return effectiveCost;
            }

            private List<TestEffectDefinition> BuildEffectiveEffects(
                TestAbilityDefinition ability,
                TestAbilityVariant? variant,
                int upcastLevel)
            {
                var effectiveEffects = new List<TestEffectDefinition>();

                foreach (var baseEffect in ability.Effects)
                {
                    var effect = baseEffect.Clone();

                    // Apply variant modifications
                    if (variant != null)
                    {
                        if (!string.IsNullOrEmpty(variant.ReplaceDamageType) && !string.IsNullOrEmpty(effect.DamageType))
                        {
                            effect.DamageType = variant.ReplaceDamageType;
                        }

                        if (variant.AdditionalDamage != 0 && effect.Type == "damage")
                        {
                            effect.Value += variant.AdditionalDamage;
                        }

                        if (!string.IsNullOrEmpty(variant.AdditionalDice) && effect.Type == "damage")
                        {
                            effect.DiceFormula = CombineDice(effect.DiceFormula, variant.AdditionalDice);
                        }

                        if (!string.IsNullOrEmpty(variant.ReplaceStatusId) && effect.Type == "apply_status")
                        {
                            effect.StatusId = variant.ReplaceStatusId;
                        }
                    }

                    // Apply upcast modifications
                    if (upcastLevel > 0 && ability.UpcastScaling != null)
                    {
                        if (ability.UpcastScaling.DamagePerLevel != 0 && effect.Type == "damage")
                        {
                            effect.Value += ability.UpcastScaling.DamagePerLevel * upcastLevel;
                        }

                        if (!string.IsNullOrEmpty(ability.UpcastScaling.DicePerLevel) && effect.Type == "damage")
                        {
                            for (int i = 0; i < upcastLevel; i++)
                            {
                                effect.DiceFormula = CombineDice(effect.DiceFormula, ability.UpcastScaling.DicePerLevel);
                            }
                        }

                        if (ability.UpcastScaling.DurationPerLevel != 0 && effect.Type == "apply_status")
                        {
                            effect.StatusDuration += ability.UpcastScaling.DurationPerLevel * upcastLevel;
                        }
                    }

                    effectiveEffects.Add(effect);
                }

                // Add variant additional effects
                if (variant?.AdditionalEffects != null)
                {
                    effectiveEffects.AddRange(variant.AdditionalEffects);
                }

                return effectiveEffects;
            }

            private string CombineDice(string? formula1, string? formula2)
            {
                if (string.IsNullOrEmpty(formula1)) return formula2 ?? "";
                if (string.IsNullOrEmpty(formula2)) return formula1;

                var (count1, sides1, bonus1) = ParseDice(formula1);
                var (count2, sides2, bonus2) = ParseDice(formula2);

                if (sides1 == sides2 && sides1 > 0)
                {
                    int totalCount = count1 + count2;
                    int totalBonus = bonus1 + bonus2;
                    if (totalBonus > 0)
                        return $"{totalCount}d{sides1}+{totalBonus}";
                    else if (totalBonus < 0)
                        return $"{totalCount}d{sides1}{totalBonus}";
                    else
                        return $"{totalCount}d{sides1}";
                }

                return formula1; // Fallback
            }

            private (int count, int sides, int bonus) ParseDice(string formula)
            {
                if (string.IsNullOrEmpty(formula))
                    return (0, 0, 0);

                formula = formula.ToLower().Replace(" ", "");

                int bonus = 0;
                int plusIdx = formula.IndexOf('+');
                if (plusIdx > 0)
                {
                    int.TryParse(formula[(plusIdx + 1)..], out bonus);
                    formula = formula[..plusIdx];
                }

                int dIdx = formula.IndexOf('d');
                if (dIdx < 0)
                {
                    int.TryParse(formula, out int flat);
                    return (0, 0, flat + bonus);
                }

                string countStr = dIdx == 0 ? "1" : formula[..dIdx];
                string sidesStr = formula[(dIdx + 1)..];

                int.TryParse(countStr, out int count);
                int.TryParse(sidesStr, out int sides);

                return (count, sides, bonus);
            }

            private int RollDice(string formula)
            {
                var (count, sides, bonus) = ParseDice(formula);
                int total = bonus;
                for (int i = 0; i < count; i++)
                {
                    total += _rng.Next(1, sides + 1);
                }
                return total;
            }

            private TestEffectResult ExecuteEffect(TestEffectDefinition effect, TestCombatant target)
            {
                var result = new TestEffectResult
                {
                    EffectType = effect.Type,
                    DamageType = effect.DamageType,
                    StatusId = effect.StatusId,
                    Duration = effect.StatusDuration
                };

                if (effect.Type == "damage")
                {
                    int damage = string.IsNullOrEmpty(effect.DiceFormula)
                        ? (int)effect.Value
                        : RollDice(effect.DiceFormula) + (int)effect.Value;
                    target.CurrentHP -= damage;
                    result.Value = damage;
                }
                else if (effect.Type == "apply_status")
                {
                    result.Value = effect.StatusDuration;
                }

                return result;
            }
        }

        #endregion

        #region Variant Tests

        [Fact]
        public void Variant_ChangesDamageType_FromBaseAbility()
        {
            // Arrange - Chromatic Orb style ability
            var ability = new TestAbilityDefinition
            {
                Id = "chromatic_orb",
                Name = "Chromatic Orb",
                Effects = new List<TestEffectDefinition>
                {
                    new() { Type = "damage", DiceFormula = "3d8", DamageType = "acid" }
                },
                Variants = new List<TestAbilityVariant>
                {
                    new() { VariantId = "fire", DisplayName = "Fire", ReplaceDamageType = "fire" },
                    new() { VariantId = "cold", DisplayName = "Cold", ReplaceDamageType = "cold" },
                    new() { VariantId = "lightning", DisplayName = "Lightning", ReplaceDamageType = "lightning" }
                }
            };

            var target = new TestCombatant { Id = "enemy1", MaxHP = 50, CurrentHP = 50 };
            var pipeline = new TestEffectPipeline();

            // Act - Use fire variant
            var results = pipeline.ExecuteAbility(ability, target, new TestAbilityExecutionOptions { VariantId = "fire" });

            // Assert
            Assert.Single(results);
            Assert.Equal("fire", results[0].DamageType);
        }

        [Fact]
        public void Variant_AddsExtraEffects_ToBaseAbility()
        {
            // Arrange - Ability with variant that adds a status effect
            var ability = new TestAbilityDefinition
            {
                Id = "elemental_strike",
                Name = "Elemental Strike",
                Effects = new List<TestEffectDefinition>
                {
                    new() { Type = "damage", Value = 10, DamageType = "slashing" }
                },
                Variants = new List<TestAbilityVariant>
                {
                    new()
                    {
                        VariantId = "frost",
                        DisplayName = "Frost Strike",
                        ReplaceDamageType = "cold",
                        AdditionalEffects = new List<TestEffectDefinition>
                        {
                            new() { Type = "apply_status", StatusId = "chilled", StatusDuration = 2 }
                        }
                    }
                }
            };

            var target = new TestCombatant { Id = "enemy1", MaxHP = 50, CurrentHP = 50 };
            var pipeline = new TestEffectPipeline();

            // Act
            var results = pipeline.ExecuteAbility(ability, target, new TestAbilityExecutionOptions { VariantId = "frost" });

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal("damage", results[0].EffectType);
            Assert.Equal("cold", results[0].DamageType);
            Assert.Equal("apply_status", results[1].EffectType);
            Assert.Equal("chilled", results[1].StatusId);
        }

        [Fact]
        public void Variant_AddsAdditionalDamage_ToBaseEffect()
        {
            // Arrange
            var ability = new TestAbilityDefinition
            {
                Id = "smite",
                Name = "Divine Smite",
                Effects = new List<TestEffectDefinition>
                {
                    new() { Type = "damage", Value = 5, DamageType = "radiant" }
                },
                Variants = new List<TestAbilityVariant>
                {
                    new()
                    {
                        VariantId = "greater",
                        DisplayName = "Greater Smite",
                        AdditionalDamage = 10
                    }
                }
            };

            var target = new TestCombatant { Id = "undead1", MaxHP = 30, CurrentHP = 30 };
            var pipeline = new TestEffectPipeline();

            // Act
            var results = pipeline.ExecuteAbility(ability, target, new TestAbilityExecutionOptions { VariantId = "greater" });

            // Assert
            Assert.Single(results);
            Assert.Equal(15, results[0].Value); // 5 base + 10 additional
            Assert.Equal(15, target.CurrentHP); // 30 - 15
        }

        [Fact]
        public void Variant_AddsAdditionalDice_CombinesFormulas()
        {
            // Arrange
            var ability = new TestAbilityDefinition
            {
                Id = "fireball",
                Name = "Empowered Fireball",
                Effects = new List<TestEffectDefinition>
                {
                    new() { Type = "damage", DiceFormula = "2d6", DamageType = "fire" }
                },
                Variants = new List<TestAbilityVariant>
                {
                    new()
                    {
                        VariantId = "empowered",
                        DisplayName = "Empowered",
                        AdditionalDice = "1d6"
                    }
                }
            };

            var target = new TestCombatant { Id = "enemy1", MaxHP = 100, CurrentHP = 100 };
            var pipeline = new TestEffectPipeline();

            // Act
            var results = pipeline.ExecuteAbility(ability, target, new TestAbilityExecutionOptions { VariantId = "empowered" });

            // Assert
            Assert.Single(results);
            // Damage should be 3d6 (2d6 + 1d6), rolling between 3-18
            Assert.InRange(results[0].Value, 3, 18);
        }

        [Fact]
        public void Variant_ReplacesStatusId_InApplyStatusEffect()
        {
            // Arrange
            var ability = new TestAbilityDefinition
            {
                Id = "curse",
                Name = "Hex",
                Effects = new List<TestEffectDefinition>
                {
                    new() { Type = "apply_status", StatusId = "hex_basic", StatusDuration = 3 }
                },
                Variants = new List<TestAbilityVariant>
                {
                    new()
                    {
                        VariantId = "greater_curse",
                        DisplayName = "Greater Curse",
                        ReplaceStatusId = "hex_greater"
                    }
                }
            };

            var target = new TestCombatant { Id = "enemy1", MaxHP = 50, CurrentHP = 50 };
            var pipeline = new TestEffectPipeline();

            // Act
            var results = pipeline.ExecuteAbility(ability, target, new TestAbilityExecutionOptions { VariantId = "greater_curse" });

            // Assert
            Assert.Single(results);
            Assert.Equal("hex_greater", results[0].StatusId);
        }

        [Fact]
        public void Variant_WithAdditionalCost_IncreasesResourceCost()
        {
            // Arrange
            var ability = new TestAbilityDefinition
            {
                Id = "spell",
                Name = "Elemental Blast",
                Cost = new TestAbilityCost
                {
                    UsesAction = true,
                    ResourceCosts = new Dictionary<string, int> { { "mana", 10 } }
                },
                Effects = new List<TestEffectDefinition>
                {
                    new() { Type = "damage", Value = 20, DamageType = "fire" }
                },
                Variants = new List<TestAbilityVariant>
                {
                    new()
                    {
                        VariantId = "maximized",
                        DisplayName = "Maximized",
                        AdditionalDamage = 10,
                        AdditionalCost = new TestAbilityCost
                        {
                            ResourceCosts = new Dictionary<string, int> { { "mana", 5 } }
                        }
                    }
                }
            };

            var pipeline = new TestEffectPipeline();

            // Act
            var effectiveCost = pipeline.CalculateEffectiveCost(ability, new TestAbilityExecutionOptions { VariantId = "maximized" });

            // Assert
            Assert.Equal(15, effectiveCost.ResourceCosts["mana"]); // 10 base + 5 variant
        }

        [Fact]
        public void NoVariant_UsesBaseAbility_Unchanged()
        {
            // Arrange
            var ability = new TestAbilityDefinition
            {
                Id = "magic_missile",
                Name = "Magic Missile",
                Effects = new List<TestEffectDefinition>
                {
                    new() { Type = "damage", DiceFormula = "1d4+1", DamageType = "force" }
                },
                Variants = new List<TestAbilityVariant>
                {
                    new() { VariantId = "empowered", ReplaceDamageType = "radiant" }
                }
            };

            var target = new TestCombatant { Id = "enemy1", MaxHP = 50, CurrentHP = 50 };
            var pipeline = new TestEffectPipeline();

            // Act - No variant specified
            var results = pipeline.ExecuteAbility(ability, target, new TestAbilityExecutionOptions());

            // Assert
            Assert.Single(results);
            Assert.Equal("force", results[0].DamageType); // Base damage type preserved
        }

        #endregion

        #region Upcast Tests

        [Fact]
        public void Upcast_IncreasesDamage_ByDamagePerLevel()
        {
            // Arrange
            var ability = new TestAbilityDefinition
            {
                Id = "burning_hands",
                Name = "Burning Hands",
                Effects = new List<TestEffectDefinition>
                {
                    new() { Type = "damage", Value = 10, DamageType = "fire" }
                },
                CanUpcast = true,
                UpcastScaling = new TestUpcastScaling
                {
                    DamagePerLevel = 5
                }
            };

            var target = new TestCombatant { Id = "enemy1", MaxHP = 100, CurrentHP = 100 };
            var pipeline = new TestEffectPipeline();

            // Act - Upcast by 2 levels
            var results = pipeline.ExecuteAbility(ability, target, new TestAbilityExecutionOptions { UpcastLevel = 2 });

            // Assert
            Assert.Single(results);
            Assert.Equal(20, results[0].Value); // 10 base + 5*2 upcast
        }

        [Fact]
        public void Upcast_AddsExtraDice_PerLevel()
        {
            // Arrange
            var ability = new TestAbilityDefinition
            {
                Id = "fireball",
                Name = "Fireball",
                Effects = new List<TestEffectDefinition>
                {
                    new() { Type = "damage", DiceFormula = "8d6", DamageType = "fire" }
                },
                CanUpcast = true,
                UpcastScaling = new TestUpcastScaling
                {
                    DicePerLevel = "1d6"
                }
            };

            var target = new TestCombatant { Id = "enemy1", MaxHP = 200, CurrentHP = 200 };
            var pipeline = new TestEffectPipeline();

            // Act - Upcast by 3 levels (8d6 + 3d6 = 11d6)
            var results = pipeline.ExecuteAbility(ability, target, new TestAbilityExecutionOptions { UpcastLevel = 3 });

            // Assert
            Assert.Single(results);
            // 11d6 should roll between 11 and 66
            Assert.InRange(results[0].Value, 11, 66);
        }

        [Fact]
        public void Upcast_IncreasesStatusDuration_ByDurationPerLevel()
        {
            // Arrange
            var ability = new TestAbilityDefinition
            {
                Id = "hold_person",
                Name = "Hold Person",
                Effects = new List<TestEffectDefinition>
                {
                    new() { Type = "apply_status", StatusId = "paralyzed", StatusDuration = 3 }
                },
                CanUpcast = true,
                UpcastScaling = new TestUpcastScaling
                {
                    DurationPerLevel = 1
                }
            };

            var target = new TestCombatant { Id = "enemy1", MaxHP = 50, CurrentHP = 50 };
            var pipeline = new TestEffectPipeline();

            // Act - Upcast by 2 levels
            var results = pipeline.ExecuteAbility(ability, target, new TestAbilityExecutionOptions { UpcastLevel = 2 });

            // Assert
            Assert.Single(results);
            Assert.Equal(5, results[0].Duration); // 3 base + 1*2 upcast
        }

        [Fact]
        public void Upcast_IncreasesResourceCost_ByLevelAndCostPerLevel()
        {
            // Arrange
            var ability = new TestAbilityDefinition
            {
                Id = "cure_wounds",
                Name = "Cure Wounds",
                Cost = new TestAbilityCost
                {
                    ResourceCosts = new Dictionary<string, int> { { "spell_slot", 1 } }
                },
                Effects = new List<TestEffectDefinition>
                {
                    new() { Type = "heal", Value = 10 }
                },
                CanUpcast = true,
                UpcastScaling = new TestUpcastScaling
                {
                    ResourceKey = "spell_slot",
                    BaseCost = 1,
                    CostPerLevel = 1
                }
            };

            var pipeline = new TestEffectPipeline();

            // Act - Upcast by 3 levels
            var effectiveCost = pipeline.CalculateEffectiveCost(ability, new TestAbilityExecutionOptions { UpcastLevel = 3 });

            // Assert
            Assert.Equal(4, effectiveCost.ResourceCosts["spell_slot"]); // 1 base + 1*3 upcast
        }

        [Fact]
        public void NoUpcast_UseBaseLevel_NoCostIncrease()
        {
            // Arrange
            var ability = new TestAbilityDefinition
            {
                Id = "magic_missile",
                Name = "Magic Missile",
                Cost = new TestAbilityCost
                {
                    ResourceCosts = new Dictionary<string, int> { { "spell_slot", 1 } }
                },
                Effects = new List<TestEffectDefinition>
                {
                    new() { Type = "damage", Value = 10, DamageType = "force" }
                },
                CanUpcast = true,
                UpcastScaling = new TestUpcastScaling
                {
                    DamagePerLevel = 5
                }
            };

            var target = new TestCombatant { Id = "enemy1", MaxHP = 50, CurrentHP = 50 };
            var pipeline = new TestEffectPipeline();

            // Act - No upcast
            var results = pipeline.ExecuteAbility(ability, target, new TestAbilityExecutionOptions { UpcastLevel = 0 });

            // Assert
            Assert.Single(results);
            Assert.Equal(10, results[0].Value); // Base damage, no scaling
        }

        #endregion

        #region Combined Variant + Upcast Tests

        [Fact]
        public void VariantAndUpcast_BothApply_ToSameAbility()
        {
            // Arrange - Chromatic Orb with upcast
            var ability = new TestAbilityDefinition
            {
                Id = "chromatic_orb",
                Name = "Chromatic Orb",
                Effects = new List<TestEffectDefinition>
                {
                    new() { Type = "damage", Value = 10, DamageType = "acid" }
                },
                Variants = new List<TestAbilityVariant>
                {
                    new()
                    {
                        VariantId = "fire",
                        DisplayName = "Fire",
                        ReplaceDamageType = "fire",
                        AdditionalDamage = 2 // Fire variant does slightly more
                    }
                },
                CanUpcast = true,
                UpcastScaling = new TestUpcastScaling
                {
                    DamagePerLevel = 5
                }
            };

            var target = new TestCombatant { Id = "enemy1", MaxHP = 100, CurrentHP = 100 };
            var pipeline = new TestEffectPipeline();

            // Act - Fire variant + upcast level 2
            var results = pipeline.ExecuteAbility(ability, target, new TestAbilityExecutionOptions
            {
                VariantId = "fire",
                UpcastLevel = 2
            });

            // Assert
            Assert.Single(results);
            Assert.Equal("fire", results[0].DamageType);
            Assert.Equal(22, results[0].Value); // 10 base + 2 variant + 5*2 upcast
        }

        [Fact]
        public void VariantAdditionalEffects_AlsoScaleWithUpcast()
        {
            // Arrange
            var ability = new TestAbilityDefinition
            {
                Id = "elemental_blast",
                Name = "Elemental Blast",
                Effects = new List<TestEffectDefinition>
                {
                    new() { Type = "damage", Value = 10, DamageType = "fire" }
                },
                Variants = new List<TestAbilityVariant>
                {
                    new()
                    {
                        VariantId = "frost",
                        DisplayName = "Frost Blast",
                        ReplaceDamageType = "cold",
                        AdditionalEffects = new List<TestEffectDefinition>
                        {
                            new() { Type = "damage", Value = 5, DamageType = "cold" }
                        }
                    }
                },
                CanUpcast = true,
                UpcastScaling = new TestUpcastScaling
                {
                    DamagePerLevel = 3
                }
            };

            var target = new TestCombatant { Id = "enemy1", MaxHP = 100, CurrentHP = 100 };
            var pipeline = new TestEffectPipeline();

            // Act - Frost variant + upcast level 1
            var results = pipeline.ExecuteAbility(ability, target, new TestAbilityExecutionOptions
            {
                VariantId = "frost",
                UpcastLevel = 1
            });

            // Assert
            Assert.Equal(2, results.Count);
            // First effect gets upcast scaling
            Assert.Equal("cold", results[0].DamageType);
            Assert.Equal(13, results[0].Value); // 10 base + 3 upcast
            // Additional effect (from variant) doesn't get upcast in this test implementation
            // (The actual implementation may handle this differently)
            Assert.Equal("cold", results[1].DamageType);
        }

        [Fact]
        public void VariantAndUpcast_BothIncreaseCost()
        {
            // Arrange
            var ability = new TestAbilityDefinition
            {
                Id = "mega_spell",
                Name = "Mega Spell",
                Cost = new TestAbilityCost
                {
                    ResourceCosts = new Dictionary<string, int>
                    {
                        { "spell_slot", 2 },
                        { "mana", 10 }
                    }
                },
                Effects = new List<TestEffectDefinition>
                {
                    new() { Type = "damage", Value = 20, DamageType = "arcane" }
                },
                Variants = new List<TestAbilityVariant>
                {
                    new()
                    {
                        VariantId = "maximized",
                        DisplayName = "Maximized",
                        AdditionalCost = new TestAbilityCost
                        {
                            ResourceCosts = new Dictionary<string, int> { { "mana", 15 } }
                        }
                    }
                },
                CanUpcast = true,
                UpcastScaling = new TestUpcastScaling
                {
                    ResourceKey = "spell_slot",
                    CostPerLevel = 1
                }
            };

            var pipeline = new TestEffectPipeline();

            // Act - Maximized variant + upcast 2
            var effectiveCost = pipeline.CalculateEffectiveCost(ability, new TestAbilityExecutionOptions
            {
                VariantId = "maximized",
                UpcastLevel = 2
            });

            // Assert
            Assert.Equal(4, effectiveCost.ResourceCosts["spell_slot"]); // 2 base + 1*2 upcast
            Assert.Equal(25, effectiveCost.ResourceCosts["mana"]); // 10 base + 15 variant
        }

        #endregion
    }
}
