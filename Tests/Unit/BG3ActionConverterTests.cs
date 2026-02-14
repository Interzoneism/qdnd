using System;
using System.Linq;
using Godot;
using QDND.Combat.Actions;
using QDND.Data.Actions;
using QDND.Data.Spells;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for BG3ActionConverter.
    /// Validates conversion from BG3SpellData to ActionDefinition.
    /// </summary>
    public static class BG3ActionConverterTests
    {
        private static int _testsPassed = 0;
        private static int _testsFailed = 0;

        /// <summary>
        /// Runs all converter tests.
        /// </summary>
        public static void RunAllTests()
        {
            _testsPassed = 0;
            _testsFailed = 0;

            GD.Print("========================================");
            GD.Print("BG3ActionConverter Unit Tests");
            GD.Print("========================================\n");

            TestBasicConversion();
            TestSpellTypeMapping();
            TestCostConversion();
            TestTargetFilterDetermination();
            TestRangeParsing();
            TestCooldownParsing();
            TestAttackTypeDetermination();
            TestSaveTypeParsing();
            TestEffectParsing_Damage();
            TestEffectParsing_Status();
            TestEffectParsing_Heal();
            TestEffectParsing_Complex();
            TestRequirementParsing();
            TestVerbalIntentParsing();
            TestUpcastScaling();
            TestBatchConversion();
            TestRawFormulasPreservation();
            TestConcentrationFlag();
            TestComponentsParsing();
            TestCastingTimeParsing();

            GD.Print("\n========================================");
            GD.Print($"Tests Passed: {_testsPassed}");
            GD.Print($"Tests Failed: {_testsFailed}");
            GD.Print("========================================");
        }

        private static void Assert(bool condition, string testName, string message)
        {
            if (condition)
            {
                _testsPassed++;
                GD.Print($"✓ {testName}");
            }
            else
            {
                _testsFailed++;
                GD.PrintErr($"✗ {testName}: {message}");
            }
        }

        #region Basic Tests

        private static void TestBasicConversion()
        {
            var spell = new BG3SpellData
            {
                Id = "Test_Spell",
                DisplayName = "Test Spell",
                Description = "Test description",
                Icon = "test_icon.png",
                Level = 1,
                SpellSchool = "Evocation"
            };

            var action = BG3ActionConverter.ConvertToAction(spell);

            Assert(action != null, "BasicConversion_NotNull", "Converted action should not be null");
            Assert(action.Id == spell.Id, "BasicConversion_Id", $"Expected {spell.Id}, got {action.Id}");
            Assert(action.Name == spell.DisplayName, "BasicConversion_Name", $"Expected {spell.DisplayName}, got {action.Name}");
            Assert(action.SpellLevel == 1, "BasicConversion_Level", $"Expected 1, got {action.SpellLevel}");
            Assert(action.School == SpellSchool.Evocation, "BasicConversion_School", $"Expected Evocation, got {action.School}");
        }

        #endregion

        #region Spell Type Mapping Tests

        private static void TestSpellTypeMapping()
        {
            var testCases = new[]
            {
                (BG3SpellType.Target, TargetType.SingleUnit),
                (BG3SpellType.Projectile, TargetType.SingleUnit),
                (BG3SpellType.Shout, TargetType.Self),
                (BG3SpellType.Zone, TargetType.Circle),
                (BG3SpellType.Multicast, TargetType.MultiUnit),
                (BG3SpellType.Rush, TargetType.Point),
                (BG3SpellType.Teleportation, TargetType.Point),
                (BG3SpellType.Wall, TargetType.Line),
                (BG3SpellType.Cone, TargetType.Cone)
            };

            foreach (var (bg3Type, expectedTargetType) in testCases)
            {
                var spell = new BG3SpellData { Id = "Test", SpellType = bg3Type };
                var action = BG3ActionConverter.ConvertToAction(spell);
                Assert(action.TargetType == expectedTargetType,
                    $"SpellTypeMapping_{bg3Type}",
                    $"Expected {expectedTargetType}, got {action.TargetType}");
            }
        }

        #endregion

        #region Cost Conversion Tests

        private static void TestCostConversion()
        {
            // Action cost
            var spell1 = new BG3SpellData
            {
                Id = "Test_Action",
                UseCosts = new SpellUseCost { ActionPoint = 1 }
            };
            var action1 = BG3ActionConverter.ConvertToAction(spell1);
            Assert(action1.Cost.UsesAction, "CostConversion_Action", "Should use action");
            Assert(!action1.Cost.UsesBonusAction, "CostConversion_NoBonus", "Should not use bonus action");

            // Bonus action cost
            var spell2 = new BG3SpellData
            {
                Id = "Test_Bonus",
                UseCosts = new SpellUseCost { BonusActionPoint = 1 }
            };
            var action2 = BG3ActionConverter.ConvertToAction(spell2);
            Assert(action2.Cost.UsesBonusAction, "CostConversion_BonusAction", "Should use bonus action");

            // Spell slot cost
            var spell3 = new BG3SpellData
            {
                Id = "Test_SpellSlot",
                UseCosts = new SpellUseCost
                {
                    ActionPoint = 1,
                    SpellSlotLevel = 3,
                    SpellSlotCount = 1
                }
            };
            var action3 = BG3ActionConverter.ConvertToAction(spell3);
            Assert(action3.Cost.ResourceCosts.ContainsKey("spell_slot_3"),
                "CostConversion_SpellSlotKey", "Should have spell_slot_3 key");
            Assert(action3.Cost.ResourceCosts["spell_slot_3"] == 1,
                "CostConversion_SpellSlotValue", "Should cost 1 spell slot");
        }

        #endregion

        #region Target Filter Tests

        private static void TestTargetFilterDetermination()
        {
            // Harmful spell -> enemies
            var spell1 = new BG3SpellData
            {
                Id = "Test_Harmful",
                SpellFlags = "IsHarmful",
                VerbalIntent = "Damage"
            };
            var action1 = BG3ActionConverter.ConvertToAction(spell1);
            Assert(action1.TargetFilter.HasFlag(TargetFilter.Enemies),
                "TargetFilter_Harmful", "Harmful spells should target enemies");

            // Healing spell -> allies
            var spell2 = new BG3SpellData
            {
                Id = "Test_Healing",
                VerbalIntent = "Healing"
            };
            var action2 = BG3ActionConverter.ConvertToAction(spell2);
            Assert(action2.TargetFilter.HasFlag(TargetFilter.Allies) || action2.TargetFilter.HasFlag(TargetFilter.Self),
                "TargetFilter_Healing", "Healing spells should target allies/self");

            // Self-cast shout
            var spell3 = new BG3SpellData
            {
                Id = "Test_SelfShout",
                SpellType = BG3SpellType.Shout
            };
            var action3 = BG3ActionConverter.ConvertToAction(spell3);
            Assert(action3.TargetFilter == TargetFilter.Self,
                "TargetFilter_SelfShout", "Self-shouts should target self only");
        }

        #endregion

        #region Range Parsing Tests

        private static void TestRangeParsing()
        {
            // Numeric range
            var spell1 = new BG3SpellData { Id = "Test", TargetRadius = "18" };
            var action1 = BG3ActionConverter.ConvertToAction(spell1);
            Assert(action1.Range == 18f, "RangeParsing_Numeric", $"Expected 18, got {action1.Range}");

            // Melee range
            var spell2 = new BG3SpellData { Id = "Test", TargetRadius = "MeleeMainWeaponRange" };
            var action2 = BG3ActionConverter.ConvertToAction(spell2);
            Assert(action2.Range == 1.5f, "RangeParsing_Melee", $"Expected 1.5, got {action2.Range}");

            // Ranged range
            var spell3 = new BG3SpellData { Id = "Test", TargetRadius = "RangedMainWeaponRange" };
            var action3 = BG3ActionConverter.ConvertToAction(spell3);
            Assert(action3.Range == 18f, "RangeParsing_Ranged", $"Expected 18, got {action3.Range}");

            // Area radius
            var spell4 = new BG3SpellData { Id = "Test", AreaRadius = "4" };
            var action4 = BG3ActionConverter.ConvertToAction(spell4);
            Assert(action4.AreaRadius == 4f, "RangeParsing_Area", $"Expected 4, got {action4.AreaRadius}");
        }

        #endregion

        #region Cooldown Tests

        private static void TestCooldownParsing()
        {
            // Once per turn
            var spell1 = new BG3SpellData { Id = "Test", Cooldown = "OncePerTurn" };
            var action1 = BG3ActionConverter.ConvertToAction(spell1);
            Assert(action1.Cooldown.TurnCooldown == 1, "Cooldown_OncePerTurn", "Should have turn cooldown");

            // Once per round
            var spell2 = new BG3SpellData { Id = "Test", Cooldown = "OncePerRound" };
            var action2 = BG3ActionConverter.ConvertToAction(spell2);
            Assert(action2.Cooldown.RoundCooldown == 1, "Cooldown_OncePerRound", "Should have round cooldown");
        }

        #endregion

        #region Attack Type Tests

        private static void TestAttackTypeDetermination()
        {
            // Melee spell attack
            var spell1 = new BG3SpellData
            {
                Id = "Test",
                SpellFlags = "IsAttack;IsMelee",
                SpellType = BG3SpellType.Target
            };
            var action1 = BG3ActionConverter.ConvertToAction(spell1);
            Assert(action1.AttackType == AttackType.MeleeSpell,
                "AttackType_MeleeSpell", $"Expected MeleeSpell, got {action1.AttackType}");

            // Ranged spell attack
            var spell2 = new BG3SpellData
            {
                Id = "Test",
                SpellFlags = "IsAttack",
                SpellType = BG3SpellType.Projectile
            };
            var action2 = BG3ActionConverter.ConvertToAction(spell2);
            Assert(action2.AttackType == AttackType.RangedSpell,
                "AttackType_RangedSpell", $"Expected RangedSpell, got {action2.AttackType}");

            // No attack
            var spell3 = new BG3SpellData { Id = "Test" };
            var action3 = BG3ActionConverter.ConvertToAction(spell3);
            Assert(action3.AttackType == null,
                "AttackType_NoAttack", "Non-attack spell should have null AttackType");
        }

        #endregion

        #region Save Type Tests

        private static void TestSaveTypeParsing()
        {
            var testCases = new[]
            {
                ("Dexterity", "dexterity"),
                ("Constitution", "constitution"),
                ("Wisdom", "wisdom"),
                ("Intelligence", "intelligence")
            };

            foreach (var (input, expected) in testCases)
            {
                var spell = new BG3SpellData { Id = "Test", SpellSaveDC = input };
                var action = BG3ActionConverter.ConvertToAction(spell);
                Assert(action.SaveType == expected,
                    $"SaveType_{input}",
                    $"Expected {expected}, got {action.SaveType}");
            }
        }

        #endregion

        #region Effect Parsing Tests

        private static void TestEffectParsing_Damage()
        {
            var spell = new BG3SpellData
            {
                Id = "Test",
                Damage = "1d8",
                DamageType = "Fire"
            };
            var action = BG3ActionConverter.ConvertToAction(spell);

            Assert(action.Effects.Count > 0, "EffectParsing_DamageCount", "Should have at least one effect");
            var damageEffect = action.Effects.FirstOrDefault(e => e.Type == "damage");
            Assert(damageEffect != null, "EffectParsing_DamageExists", "Should have damage effect");
            Assert(damageEffect!.DiceFormula == "1d8", "EffectParsing_DamageDice", "Should have correct dice formula");
            Assert(damageEffect!.DamageType == "Fire", "EffectParsing_DamageType", "Should have correct damage type");
        }

        private static void TestEffectParsing_Status()
        {
            var spell = new BG3SpellData
            {
                Id = "Test",
                SpellProperties = "ApplyStatus(BURNING,100,3)"
            };
            var action = BG3ActionConverter.ConvertToAction(spell);

            var statusEffect = action.Effects.FirstOrDefault(e => e.Type == "apply_status");
            Assert(statusEffect != null, "EffectParsing_StatusExists", "Should have status effect");
            Assert(statusEffect!.StatusId == "BURNING", "EffectParsing_StatusId", $"Expected BURNING, got {statusEffect.StatusId}");
            Assert(statusEffect!.StatusDuration == 3, "EffectParsing_StatusDuration", $"Expected 3, got {statusEffect.StatusDuration}");
        }

        private static void TestEffectParsing_Heal()
        {
            var spell = new BG3SpellData
            {
                Id = "Test",
                SpellProperties = "Heal(2d8+10)"
            };
            var action = BG3ActionConverter.ConvertToAction(spell);

            var healEffect = action.Effects.FirstOrDefault(e => e.Type == "heal");
            Assert(healEffect != null, "EffectParsing_HealExists", "Should have heal effect");
            Assert(healEffect!.DiceFormula == "2d8+10", "EffectParsing_HealFormula", $"Expected 2d8+10, got {healEffect.DiceFormula}");
        }

        private static void TestEffectParsing_Complex()
        {
            var spell = new BG3SpellData
            {
                Id = "Test",
                SpellProperties = "DealDamage(1d8,Fire);ApplyStatus(BURNING,100,3)",
                SpellSuccess = "DealDamage(2d6,Fire)",
                SpellFail = "DealDamage(1d6,Fire)"
            };
            var action = BG3ActionConverter.ConvertToAction(spell);

            Assert(action.Effects.Count >= 3, "EffectParsing_ComplexCount",
                $"Expected at least 3 effects, got {action.Effects.Count}");

            var onHitEffects = action.Effects.Where(e => e.Condition == "on_hit").ToList();
            Assert(onHitEffects.Count > 0, "EffectParsing_OnHitExists", "Should have on-hit effects");

            var onMissEffects = action.Effects.Where(e => e.Condition == "on_miss").ToList();
            Assert(onMissEffects.Count > 0, "EffectParsing_OnMissExists", "Should have on-miss effects");
        }

        #endregion

        #region Requirement Tests

        private static void TestRequirementParsing()
        {
            var spell = new BG3SpellData
            {
                Id = "Test",
                WeaponTypes = "Melee;Ranged"
            };
            var action = BG3ActionConverter.ConvertToAction(spell);

            Assert(action.Requirements.Count == 2, "RequirementParsing_Count",
                $"Expected 2 requirements, got {action.Requirements.Count}");
            Assert(action.Requirements.Any(r => r.Type == "weapon_type" && r.Value == "Melee"),
                "RequirementParsing_Melee", "Should have melee weapon requirement");
        }

        #endregion

        #region Verbal Intent Tests

        private static void TestVerbalIntentParsing()
        {
            var testCases = new[]
            {
                ("Damage", VerbalIntent.Damage),
                ("Healing", VerbalIntent.Healing),
                ("Buff", VerbalIntent.Buff),
                ("Debuff", VerbalIntent.Debuff),
                ("Utility", VerbalIntent.Utility)
            };

            foreach (var (input, expected) in testCases)
            {
                var spell = new BG3SpellData { Id = "Test", VerbalIntent = input };
                var action = BG3ActionConverter.ConvertToAction(spell);
                Assert(action.Intent == expected,
                    $"VerbalIntent_{input}",
                    $"Expected {expected}, got {action.Intent}");
            }
        }

        #endregion

        #region Upcast Scaling Tests

        private static void TestUpcastScaling()
        {
            var spell = new BG3SpellData
            {
                Id = "Test",
                Level = 1,
                Damage = "1d6"
            };
            var action = BG3ActionConverter.ConvertToAction(spell);

            Assert(action.CanUpcast, "UpcastScaling_CanUpcast", "Level 1+ spells should be upcastable");
            Assert(action.UpcastScaling != null, "UpcastScaling_NotNull", "Should have upcast scaling");
            Assert(action.UpcastScaling!.DicePerLevel == "1d6",
                "UpcastScaling_DicePerLevel", $"Expected 1d6, got {action.UpcastScaling.DicePerLevel}");

            // Cantrips shouldn't upcast
            var cantrip = new BG3SpellData
            {
                Id = "Cantrip",
                Level = 0,
                SpellType = BG3SpellType.Cantrip
            };
            var cantripAction = BG3ActionConverter.ConvertToAction(cantrip);
            Assert(!cantripAction.CanUpcast, "UpcastScaling_CantripNoUpcast", "Cantrips should not upcast");
        }

        #endregion

        #region Batch Conversion Tests

        private static void TestBatchConversion()
        {
            var spells = new[]
            {
                new BG3SpellData { Id = "Spell1", DisplayName = "Test 1" },
                new BG3SpellData { Id = "Spell2", DisplayName = "Test 2" },
                new BG3SpellData { Id = "Spell3", DisplayName = "Test 3" }
            };

            var actions = BG3ActionConverter.ConvertBatch(spells);

            Assert(actions.Count == 3, "BatchConversion_Count", $"Expected 3 actions, got {actions.Count}");
            Assert(actions.ContainsKey("Spell1"), "BatchConversion_Key1", "Should contain Spell1");
            Assert(actions.ContainsKey("Spell2"), "BatchConversion_Key2", "Should contain Spell2");
            Assert(actions.ContainsKey("Spell3"), "BatchConversion_Key3", "Should contain Spell3");
        }

        #endregion

        #region Raw Formulas Tests

        private static void TestRawFormulasPreservation()
        {
            var spell = new BG3SpellData
            {
                Id = "Test",
                SpellProperties = "DealDamage(1d8,Fire)",
                SpellRoll = "Attack(AttackType.RangedSpellAttack)",
                SpellSuccess = "DealDamage(2d6,Fire)",
                SpellFail = "DealDamage(1d6,Fire)"
            };

            var actionWithFormulas = BG3ActionConverter.ConvertToAction(spell, includeRawFormulas: true);
            Assert(actionWithFormulas.BG3SpellProperties == spell.SpellProperties,
                "RawFormulas_Properties", "Should preserve SpellProperties");
            Assert(actionWithFormulas.BG3SpellRoll == spell.SpellRoll,
                "RawFormulas_Roll", "Should preserve SpellRoll");

            var actionWithoutFormulas = BG3ActionConverter.ConvertToAction(spell, includeRawFormulas: false);
            Assert(actionWithoutFormulas.BG3SpellProperties == null,
                "RawFormulas_NotPreserved", "Should not preserve formulas when flag is false");
        }

        #endregion

        #region Concentration Tests

        private static void TestConcentrationFlag()
        {
            var spell = new BG3SpellData
            {
                Id = "Test",
                SpellFlags = "IsConcentration;IsSpell"
            };
            var action = BG3ActionConverter.ConvertToAction(spell);

            Assert(action.RequiresConcentration, "Concentration_Flag", "Should require concentration");
            Assert(action.BG3Flags.Contains("IsConcentration"),
                "Concentration_BG3Flag", "Should have IsConcentration in BG3Flags");
        }

        #endregion

        #region Components Tests

        private static void TestComponentsParsing()
        {
            var spell = new BG3SpellData
            {
                Id = "Test",
                SpellSchool = "Evocation"
            };
            var action = BG3ActionConverter.ConvertToAction(spell);

            Assert(action.Components != SpellComponents.None,
                "Components_NotNone", "Components should not be None");
            Assert(action.Components.HasFlag(SpellComponents.Verbal),
                "Components_Verbal", "Should have Verbal component");
            Assert(action.Components.HasFlag(SpellComponents.Somatic),
                "Components_Somatic", "Should have Somatic component");
        }

        #endregion

        #region Casting Time Tests

        private static void TestCastingTimeParsing()
        {
            // Action
            var spell1 = new BG3SpellData
            {
                Id = "Test",
                UseCosts = new SpellUseCost { ActionPoint = 1 }
            };
            var action1 = BG3ActionConverter.ConvertToAction(spell1);
            Assert(action1.CastingTime == CastingTimeType.Action,
                "CastingTime_Action", $"Expected Action, got {action1.CastingTime}");

            // Bonus Action
            var spell2 = new BG3SpellData
            {
                Id = "Test",
                UseCosts = new SpellUseCost { BonusActionPoint = 1 }
            };
            var action2 = BG3ActionConverter.ConvertToAction(spell2);
            Assert(action2.CastingTime == CastingTimeType.BonusAction,
                "CastingTime_BonusAction", $"Expected BonusAction, got {action2.CastingTime}");

            // Reaction
            var spell3 = new BG3SpellData
            {
                Id = "Test",
                UseCosts = new SpellUseCost { ReactionActionPoint = 1 }
            };
            var action3 = BG3ActionConverter.ConvertToAction(spell3);
            Assert(action3.CastingTime == CastingTimeType.Reaction,
                "CastingTime_Reaction", $"Expected Reaction, got {action3.CastingTime}");
        }

        #endregion
    }
}
