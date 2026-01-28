using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for the Rules Engine and Modifier systems.
    /// Uses self-contained test implementations to avoid Godot dependencies.
    /// </summary>
    public class RulesEngineTests
    {
        #region Test Implementations

        public enum TestModifierType { Flat, Percentage, Override, Advantage, Disadvantage }
        public enum TestModifierTarget { AttackRoll, DamageDealt, DamageTaken, ArmorClass, SavingThrow, Initiative }

        public class TestModifier
        {
            public string Name { get; set; }
            public TestModifierType Type { get; set; }
            public TestModifierTarget Target { get; set; }
            public float Value { get; set; }
            public string Source { get; set; }
            public int Priority { get; set; }

            public static TestModifier Flat(string name, TestModifierTarget target, float value, string source = null) =>
                new() { Name = name, Target = target, Type = TestModifierType.Flat, Value = value, Source = source, Priority = 50 };
            
            public static TestModifier Percentage(string name, TestModifierTarget target, float value, string source = null) =>
                new() { Name = name, Target = target, Type = TestModifierType.Percentage, Value = value, Source = source, Priority = 60 };
            
            public static TestModifier Advantage(string name, TestModifierTarget target, string source = null) =>
                new() { Name = name, Target = target, Type = TestModifierType.Advantage, Value = 1, Source = source };
            
            public static TestModifier Disadvantage(string name, TestModifierTarget target, string source = null) =>
                new() { Name = name, Target = target, Type = TestModifierType.Disadvantage, Value = 1, Source = source };
        }

        public class TestModifierStack
        {
            private readonly List<TestModifier> _modifiers = new();

            public void AddModifier(TestModifier mod) => _modifiers.Add(mod);
            
            public void RemoveBySource(string source) => 
                _modifiers.RemoveAll(m => m.Source == source);

            public List<TestModifier> GetModifiers(TestModifierTarget target) =>
                _modifiers.Where(m => m.Target == target).OrderBy(m => m.Priority).ToList();

            public (float FinalValue, List<TestModifier> Applied) Apply(float baseValue, TestModifierTarget target)
            {
                var mods = GetModifiers(target);
                float result = baseValue;
                
                // Check for override first
                var overrideMod = mods.FirstOrDefault(m => m.Type == TestModifierType.Override);
                if (overrideMod != null)
                    return (overrideMod.Value, new List<TestModifier> { overrideMod });

                // Apply flat modifiers
                foreach (var mod in mods.Where(m => m.Type == TestModifierType.Flat))
                {
                    result += mod.Value;
                }

                // Apply percentage modifiers
                foreach (var mod in mods.Where(m => m.Type == TestModifierType.Percentage))
                {
                    result *= (1 + mod.Value / 100f);
                }

                return (result, mods);
            }

            public int GetAdvantageState(TestModifierTarget target)
            {
                int state = 0;
                foreach (var mod in GetModifiers(target))
                {
                    if (mod.Type == TestModifierType.Advantage) state++;
                    if (mod.Type == TestModifierType.Disadvantage) state--;
                }
                return Math.Clamp(state, -1, 1);
            }
        }

        public class TestDiceRoller
        {
            private readonly Random _rng;

            public TestDiceRoller(int seed) => _rng = new Random(seed);

            public int Roll(int sides) => _rng.Next(1, sides + 1);

            public int RollWithAdvantage(int sides, int advantageState)
            {
                int roll1 = Roll(sides);
                if (advantageState == 0) return roll1;
                
                int roll2 = Roll(sides);
                return advantageState > 0 ? Math.Max(roll1, roll2) : Math.Min(roll1, roll2);
            }
        }

        public class TestCombatant
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public int HP { get; set; }
            public int MaxHP { get; set; }
            public int AC { get; set; } = 10;
            public bool IsAlive => HP > 0;
        }

        public class TestRulesEngine
        {
            private readonly Dictionary<string, TestModifierStack> _stacks = new();
            private readonly TestModifierStack _globalStack = new();
            private readonly TestDiceRoller _dice;

            public TestRulesEngine(int seed) => _dice = new TestDiceRoller(seed);

            public TestModifierStack GetModifiers(string combatantId)
            {
                if (!_stacks.TryGetValue(combatantId, out var stack))
                {
                    stack = new TestModifierStack();
                    _stacks[combatantId] = stack;
                }
                return stack;
            }

            public void AddModifier(string combatantId, TestModifier mod) =>
                GetModifiers(combatantId).AddModifier(mod);

            public (int NaturalRoll, int Total, bool IsHit, bool IsCrit) RollAttack(
                TestCombatant source, TestCombatant target, int baseBonus)
            {
                var advState = GetModifiers(source.Id).GetAdvantageState(TestModifierTarget.AttackRoll);
                int natRoll = _dice.RollWithAdvantage(20, advState);
                
                var (modifiedBonus, _) = GetModifiers(source.Id).Apply(baseBonus, TestModifierTarget.AttackRoll);
                int total = natRoll + (int)modifiedBonus;

                int targetAC = target.AC;
                var (acMod, _) = GetModifiers(target.Id).Apply(0, TestModifierTarget.ArmorClass);
                targetAC += (int)acMod;

                bool isCrit = natRoll == 20;
                bool isHit = isCrit || (natRoll != 1 && total >= targetAC);

                return (natRoll, total, isHit, isCrit);
            }

            public float CalculateHitChance(TestCombatant source, TestCombatant target, int baseBonus)
            {
                var (modifiedBonus, _) = GetModifiers(source.Id).Apply(baseBonus, TestModifierTarget.AttackRoll);
                int targetAC = target.AC;
                var (acMod, _) = GetModifiers(target.Id).Apply(0, TestModifierTarget.ArmorClass);
                targetAC += (int)acMod;

                int neededRoll = targetAC - (int)modifiedBonus;
                neededRoll = Math.Clamp(neededRoll, 2, 20); // 1 always misses, 20 always hits
                
                return (21 - neededRoll) * 5f; // Percentage
            }
        }

        #endregion

        [Fact]
        public void DiceRoller_WithSameSeed_IsDeterministic()
        {
            var roller1 = new TestDiceRoller(12345);
            var roller2 = new TestDiceRoller(12345);

            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(roller1.Roll(20), roller2.Roll(20));
            }
        }

        [Fact]
        public void ModifierStack_AppliesFlat_Correctly()
        {
            var stack = new TestModifierStack();
            stack.AddModifier(TestModifier.Flat("Bonus", TestModifierTarget.AttackRoll, 5));
            stack.AddModifier(TestModifier.Flat("Penalty", TestModifierTarget.AttackRoll, -2));

            var (finalValue, applied) = stack.Apply(10, TestModifierTarget.AttackRoll);

            Assert.Equal(13, finalValue); // 10 + 5 - 2
            Assert.Equal(2, applied.Count);
        }

        [Fact]
        public void ModifierStack_AppliesPercentage_AfterFlat()
        {
            var stack = new TestModifierStack();
            stack.AddModifier(TestModifier.Flat("FlatBonus", TestModifierTarget.DamageDealt, 10));
            stack.AddModifier(TestModifier.Percentage("PercentBonus", TestModifierTarget.DamageDealt, 50));

            var (finalValue, applied) = stack.Apply(100, TestModifierTarget.DamageDealt);

            // Base 100 + Flat 10 = 110, then 50% = 110 * 1.5 = 165
            Assert.Equal(165, finalValue);
        }

        [Fact]
        public void ModifierStack_Override_ReplacesBase()
        {
            var stack = new TestModifierStack();
            stack.AddModifier(TestModifier.Flat("FlatBonus", TestModifierTarget.ArmorClass, 5));
            stack.AddModifier(new TestModifier
            {
                Name = "Override",
                Target = TestModifierTarget.ArmorClass,
                Type = TestModifierType.Override,
                Value = 20
            });

            var (finalValue, applied) = stack.Apply(10, TestModifierTarget.ArmorClass);

            Assert.Equal(20, finalValue); // Override wins
        }

        [Fact]
        public void ModifierStack_AdvantageState_TracksCorrectly()
        {
            var stack = new TestModifierStack();
            
            // Initially neutral
            Assert.Equal(0, stack.GetAdvantageState(TestModifierTarget.AttackRoll));
            
            // Add advantage
            stack.AddModifier(TestModifier.Advantage("Lucky", TestModifierTarget.AttackRoll));
            Assert.Equal(1, stack.GetAdvantageState(TestModifierTarget.AttackRoll));
            
            // Add disadvantage - cancels out
            stack.AddModifier(TestModifier.Disadvantage("Blinded", TestModifierTarget.AttackRoll));
            Assert.Equal(0, stack.GetAdvantageState(TestModifierTarget.AttackRoll));
        }

        [Fact]
        public void ModifierStack_RemoveBySource_Works()
        {
            var stack = new TestModifierStack();
            stack.AddModifier(TestModifier.Flat("Buff", TestModifierTarget.AttackRoll, 5, "spell:bless"));
            stack.AddModifier(TestModifier.Flat("AnotherBuff", TestModifierTarget.AttackRoll, 3, "item:ring"));

            stack.RemoveBySource("spell:bless");

            var (finalValue, applied) = stack.Apply(10, TestModifierTarget.AttackRoll);
            Assert.Equal(13, finalValue); // Only the ring's +3 remains
            Assert.Single(applied);
        }

        [Fact]
        public void RulesEngine_AttackRoll_AppliesModifiers()
        {
            var engine = new TestRulesEngine(42);
            var source = new TestCombatant { Id = "src", Name = "Attacker", HP = 20, MaxHP = 20 };
            var target = new TestCombatant { Id = "tgt", Name = "Defender", HP = 20, MaxHP = 20, AC = 10 };
            
            engine.AddModifier(source.Id, TestModifier.Flat("TestAttackBonus", TestModifierTarget.AttackRoll, 5));

            var (natRoll, total, _, _) = engine.RollAttack(source, target, 3);

            // Should have natural roll + 3 base + 5 modifier = total
            Assert.Equal(natRoll + 8, total);
        }

        [Fact]
        public void RulesEngine_HitChance_ReturnsPercentage()
        {
            var engine = new TestRulesEngine(42);
            var source = new TestCombatant { Id = "src", Name = "Attacker", HP = 20, MaxHP = 20 };
            var target = new TestCombatant { Id = "tgt", Name = "Defender", HP = 20, MaxHP = 20, AC = 10 };

            float hitChance = engine.CalculateHitChance(source, target, 5);

            Assert.InRange(hitChance, 0, 100);
        }

        [Fact]
        public void RulesEngine_ArmorClassModifier_AffectsTarget()
        {
            var engine = new TestRulesEngine(42);
            var source = new TestCombatant { Id = "src", Name = "Attacker", HP = 20, MaxHP = 20 };
            var target = new TestCombatant { Id = "tgt", Name = "Defender", HP = 20, MaxHP = 20, AC = 10 };

            float baseChance = engine.CalculateHitChance(source, target, 5);
            
            engine.AddModifier(target.Id, TestModifier.Flat("Shield", TestModifierTarget.ArmorClass, 5));
            
            float afterBonus = engine.CalculateHitChance(source, target, 5);

            Assert.True(afterBonus < baseChance); // Higher AC = lower hit chance
        }

        [Fact]
        public void RulesEngine_MultipleRolls_DifferentResults()
        {
            var engine = new TestRulesEngine(42);
            var source = new TestCombatant { Id = "src", Name = "Attacker", HP = 20, MaxHP = 20 };
            var target = new TestCombatant { Id = "tgt", Name = "Defender", HP = 20, MaxHP = 20, AC = 10 };

            var rolls = new HashSet<int>();
            for (int i = 0; i < 20; i++)
            {
                var (natRoll, _, _, _) = engine.RollAttack(source, target, 0);
                rolls.Add(natRoll);
            }

            Assert.True(rolls.Count > 1); // Should have gotten different results
        }
    }
}
