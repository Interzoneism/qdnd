using System;
using Xunit;
using QDND.Combat.Rules.Conditions;
using QDND.Combat.Entities;
using QDND.Data.CharacterModel;
using QDND.Combat.Rules.Boosts;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Core unit tests for the ConditionEvaluator - focused on the most critical functionality.
    /// </summary>
    public class ConditionEvaluatorCoreTests
    {
        // ══════════════════════════════════════════════
        // BASIC OPERATORS AND LITERALS
        // ══════════════════════════════════════════════

        [Fact]
        public void Evaluate_TrueLiteral_ReturnsTrue()
        {
            var evaluator = ConditionEvaluator.Instance;
            var ctx = new ConditionContext();
            Assert.True(evaluator.Evaluate("true", ctx));
        }

        [Fact]
        public void Evaluate_FalseLiteral_ReturnsFalse()
        {
            var evaluator = ConditionEvaluator.Instance;
            var ctx = new ConditionContext();
            Assert.False(evaluator.Evaluate("false", ctx));
        }

        [Fact]
        public void Evaluate_NullOrEmptyCondition_ReturnsTrue()
        {
            var evaluator = ConditionEvaluator.Instance;
            var ctx = new ConditionContext();
            Assert.True(evaluator.Evaluate(null, ctx));
            Assert.True(evaluator.Evaluate("", ctx));
            Assert.True(evaluator.Evaluate("   ", ctx));
        }

        [Fact]
        public void Evaluate_NotOperator_Works()
        {
            var evaluator = ConditionEvaluator.Instance;
            var ctx = new ConditionContext();
            Assert.True(evaluator.Evaluate("not false", ctx));
            Assert.False(evaluator.Evaluate("not true", ctx));
        }

        [Fact]
        public void Evaluate_AndOperator_Works()
        {
            var evaluator = ConditionEvaluator.Instance;
            var ctx = new ConditionContext();
            Assert.True(evaluator.Evaluate("true and true", ctx));
            Assert.False(evaluator.Evaluate("true and false", ctx));
            Assert.False(evaluator.Evaluate("false and true", ctx));
        }

        [Fact]
        public void Evaluate_OrOperator_Works()
        {
            var evaluator = ConditionEvaluator.Instance;
            var ctx = new ConditionContext();
            Assert.True(evaluator.Evaluate("true or false", ctx));
            Assert.True(evaluator.Evaluate("false or true", ctx));
            Assert.False(evaluator.Evaluate("false or false", ctx));
        }

        [Fact]
        public void Evaluate_ComplexBooleanExpression()
        {
            var evaluator = ConditionEvaluator.Instance;
            var ctx = new ConditionContext();
            Assert.True(evaluator.Evaluate("not false and true or false", ctx));
            Assert.False(evaluator.Evaluate("false or (true and false)", ctx));
        }

        // ══════════════════════════════════════════════
        // COMPARISON OPERATORS
        // ══════════════════════════════════════════════

        [Fact]
        public void Evaluate_NumericComparisons()
        {
            var evaluator = ConditionEvaluator.Instance;
            var ctx = new ConditionContext();
            
            Assert.True(evaluator.Evaluate("5 < 10", ctx));
            Assert.False(evaluator.Evaluate("10 < 5", ctx));
            Assert.True(evaluator.Evaluate("5 <= 5", ctx));
            Assert.True(evaluator.Evaluate("10 > 5", ctx));
            Assert.True(evaluator.Evaluate("5 >= 5", ctx));
            Assert.True(evaluator.Evaluate("5 == 5", ctx));
            Assert.False(evaluator.Evaluate("5 == 10", ctx));
            Assert.True(evaluator.Evaluate("5 != 10", ctx));
        }

        // ══════════════════════════════════════════════
        // ATTACK TYPE CHECKS
        // ══════════════════════════════════════════════

        [Fact]
        public void Evaluate_IsMeleeAttack()
        {
            var evaluator = ConditionEvaluator.Instance;
            
            var meleeCtx = new ConditionContext
            {
                IsMelee = true,
                IsWeaponAttack = true
            };
            Assert.True(evaluator.Evaluate("IsMeleeAttack()", meleeCtx));
            
            var rangedCtx = new ConditionContext
            {
                IsRanged = true,
                IsWeaponAttack = true
            };
            Assert.False(evaluator.Evaluate("IsMeleeAttack()", rangedCtx));
        }

        [Fact]
        public void Evaluate_IsWeaponAttack()
        {
            var evaluator = ConditionEvaluator.Instance;
            
            var weaponCtx = new ConditionContext { IsWeaponAttack = true };
            Assert.True(evaluator.Evaluate("IsWeaponAttack()", weaponCtx));
            
            var spellCtx = new ConditionContext { IsSpellAttack = true };
            Assert.False(evaluator.Evaluate("IsWeaponAttack()", spellCtx));
        }

        [Fact]
        public void Evaluate_IsMeleeWeaponAttack()
        {
            var evaluator = ConditionEvaluator.Instance;
            
            var ctx = new ConditionContext
            {
                IsMelee = true,
                IsWeaponAttack = true
            };
            Assert.True(evaluator.Evaluate("IsMeleeWeaponAttack()", ctx));
        }

        // ══════════════════════════════════════════════
        // HIT / CRITICAL CHECKS
        // ══════════════════════════════════════════════

        [Fact]
        public void Evaluate_IsCriticalHit()
        {
            var evaluator = ConditionEvaluator.Instance;
            
            var critCtx = new ConditionContext { IsCriticalHit = true };
            Assert.True(evaluator.Evaluate("IsCriticalHit()", critCtx));
            
            var normalCtx = new ConditionContext { IsCriticalHit = false };
            Assert.False(evaluator.Evaluate("IsCriticalHit()", normalCtx));
        }

        [Fact]
        public void Evaluate_HasDamageEffectFlag()
        {
            var evaluator = ConditionEvaluator.Instance;
            
            var hitCtx = new ConditionContext { IsHit = true };
            Assert.True(evaluator.Evaluate("HasDamageEffectFlag(DamageFlags.Hit)", hitCtx));
            
            var missCtx = new ConditionContext { IsHit = false };
            Assert.True(evaluator.Evaluate("HasDamageEffectFlag(DamageFlags.Miss)", missCtx));
        }

        // ══════════════════════════════════════════════
        // ENTITY CHECKS WITH COMBATANTS
        // ══════════════════════════════════════════════

        [Fact]
        public void Evaluate_Self_WhenSourceEqualsTarget()
        {
            var evaluator = ConditionEvaluator.Instance;
            var source = new Combatant("id1", "TestChar", Faction.Player, 50, 10);
            
            var ctx = new ConditionContext
            {
                Source = source,
                Target = source
            };
            
            Assert.True(evaluator.Evaluate("Self()", ctx));
        }

        [Fact]
        public void Evaluate_HasStatus_WithBoosts()
        {
            var evaluator = ConditionEvaluator.Instance;
            var source = new Combatant("id1", "Barbarian", Faction.Player, 50, 10);
            
            // Add a boost from Status source to simulate having the status
            source.Boosts.AddBoost(
                new BoostDefinition { Type = BoostType.AC, Parameters = new object[] { 2 } },
                "Status",
                "RAGING"
            );
            
            var ctx = new ConditionContext { Source = source };
            Assert.True(evaluator.Evaluate("HasStatus('RAGING')", ctx));
        }

        // ══════════════════════════════════════════════
        // REAL-WORLD BG3 CONDITIONS
        // ══════════════════════════════════════════════

        [Fact]
        public void Evaluate_RealWorldCondition_MeleeAttackWithStatus()
        {
            var evaluator = ConditionEvaluator.Instance;
            var source = new Combatant("id1", "Barbarian", Faction.Player, 50, 10);
            source.Boosts.AddBoost(
                new BoostDefinition { Type = BoostType.AC, Parameters = new object[] { 2 } },
                "Status",
                "RAGING"
            );
            
            var ctx = new ConditionContext
            {
                Source = source,
                IsMelee = true,
                IsWeaponAttack = true
            };
            
            // Real BG3 condition from Rage passives
            Assert.True(evaluator.Evaluate("HasStatus('RAGING') and IsMeleeAttack()", ctx));
        }

        [Fact]
        public void Evaluate_RealWorldCondition_CloseRangeMelee()
        {
            var evaluator = ConditionEvaluator.Instance;
            var source = new Combatant("id1", "Fighter", Faction.Player, 50, 10);
            var target = new Combatant("id2", "Enemy", Faction.Hostile, 30, 8);
            
            var ctx = new ConditionContext
            {
                Source = source,
                Target = target,
                IsMelee = true,
                IsWeaponAttack = true
            };
            
            // Real BG3 condition for close-range advantage
            Assert.True(evaluator.Evaluate("not DistanceToTargetGreaterThan(3) and IsMeleeAttack()", ctx));
        }

        [Fact]
        public void Evaluate_RealWorldCondition_RangedOrSpellAttack()
        {
            var evaluator = ConditionEvaluator.Instance;
            var ctx = new ConditionContext { IsSpellAttack = true };
            
            // Common BG3 condition for ranged penalties
            Assert.True(evaluator.Evaluate("IsRangedAttack() or IsSpellAttack()", ctx));
        }
    }
}
