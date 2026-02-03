using Xunit;
using QDND.Combat.Arena;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Rules;
using QDND.Combat.Abilities;
using System.Collections.Generic;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for enhanced combat feedback system.
    /// </summary>
    public class CombatFeedbackTests
    {
        #region CombatLogEntry Tests

        [Fact]
        public void CombatLogEntry_SupportsRollBreakdown()
        {
            // Arrange
            var entry = new CombatLogEntry
            {
                Type = CombatLogEntryType.DamageDealt,
                SourceName = "Fighter",
                TargetName = "Goblin",
                Value = 10,
                IsCritical = true
            };

            var breakdown = new RollBreakdown
            {
                NaturalRoll = 20,
                Total = 25
            };
            breakdown.AddModifier("STR", 3, BreakdownCategory.Ability);
            breakdown.AddModifier("Proficiency", 2, BreakdownCategory.Proficiency);

            // Act
            entry.AddBreakdown("rollBreakdown", breakdown);

            // Assert
            Assert.NotNull(entry.Breakdown);
            Assert.True(entry.Breakdown.ContainsKey("rollBreakdown"));
            var retrievedBreakdown = entry.Breakdown["rollBreakdown"] as RollBreakdown;
            Assert.NotNull(retrievedBreakdown);
            Assert.Equal(20, retrievedBreakdown.NaturalRoll);
            Assert.Equal(25, retrievedBreakdown.Total);
        }

        [Fact]
        public void CombatLog_LogDamage_StoresBreakdown()
        {
            // Arrange
            var log = new CombatLog();
            var breakdown = new Dictionary<string, object>
            {
                { "naturalRoll", 18 },
                { "modifiers", 5 },
                { "total", 23 }
            };

            // Act
            log.LogDamage("fighter_1", "Fighter", "goblin_1", "Goblin", 15, breakdown, isCritical: false);

            // Assert
            var entries = log.GetEntries();
            Assert.Single(entries);
            var entry = entries[0];
            Assert.Equal(CombatLogEntryType.DamageDealt, entry.Type);
            Assert.NotEmpty(entry.Breakdown);
            Assert.Equal(18, entry.Breakdown["naturalRoll"]);
            Assert.False(entry.IsCritical);
        }

        [Fact]
        public void CombatLog_LogDamage_MarksCritical()
        {
            // Arrange
            var log = new CombatLog();

            // Act
            log.LogDamage("fighter_1", "Fighter", "goblin_1", "Goblin", 24, isCritical: true);

            // Assert
            var entries = log.GetEntries();
            Assert.Single(entries);
            var entry = entries[0];
            Assert.True(entry.IsCritical);
            Assert.Equal(LogSeverity.Important, entry.Severity);
        }

        [Fact]
        public void CombatLog_LogAttack_MarksMiss()
        {
            // Arrange
            var log = new CombatLog();

            // Act
            log.LogAttack("fighter_1", "Fighter", "goblin_1", "Goblin", hit: false);

            // Assert
            var entries = log.GetEntries();
            Assert.Single(entries);
            var entry = entries[0];
            Assert.True(entry.IsMiss);
            Assert.Equal(CombatLogEntryType.AttackDeclared, entry.Type);
        }

        #endregion

        #region RulesEngine Hit/Miss Tests

        [Fact]
        public void RulesEngine_AttackRoll_DetectsCritical()
        {
            // Arrange - find seed that rolls natural 20
            int critSeed = FindSeedForNaturalRoll(20);
            var engine = new RulesEngine(critSeed);
            var source = CreateTestCombatant("attacker");
            var target = CreateTestCombatant("defender");

            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = source,
                Target = target,
                BaseValue = 5
            };

            // Act
            var result = engine.RollAttack(input);

            // Assert
            Assert.Equal(20, result.NaturalRoll);
            Assert.True(result.IsCritical);
            Assert.True(result.IsSuccess); // Nat 20 always hits
        }

        [Fact]
        public void RulesEngine_AttackRoll_DetectsMiss()
        {
            // Arrange - find seed that rolls natural 1
            int missSeed = FindSeedForNaturalRoll(1);
            var engine = new RulesEngine(missSeed);
            var source = CreateTestCombatant("attacker");
            var target = CreateTestCombatant("defender");

            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = source,
                Target = target,
                BaseValue = 100 // Even with huge bonus, nat 1 misses
            };

            // Act
            var result = engine.RollAttack(input);

            // Assert
            Assert.Equal(1, result.NaturalRoll);
            Assert.True(result.IsCriticalFailure);
            Assert.False(result.IsSuccess); // Nat 1 always misses
        }

        [Fact]
        public void RulesEngine_AttackRoll_IncludesBreakdown()
        {
            // Arrange
            var engine = new RulesEngine(42);
            var source = CreateTestCombatant("attacker");
            var target = CreateTestCombatant("defender");

            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = source,
                Target = target,
                BaseValue = 5
            };

            // Act
            var result = engine.RollAttack(input);

            // Assert
            Assert.NotNull(result.Breakdown);
            Assert.True(result.Breakdown.NaturalRoll > 0);
            Assert.Equal((int)result.FinalValue, result.Breakdown.Total);
        }

        [Fact]
        public void RulesEngine_CalculateHitChance_ReturnsValidPercentage()
        {
            // Arrange
            var engine = new RulesEngine(42);
            var source = CreateTestCombatant("attacker");
            var target = CreateTestCombatant("defender");

            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = source,
                Target = target,
                BaseValue = 5,
                DC = 15
            };

            // Act
            var result = engine.CalculateHitChance(input);

            // Assert
            Assert.True(result.FinalValue >= 0 && result.FinalValue <= 100, 
                $"Hit chance {result.FinalValue} should be between 0 and 100");
        }

        #endregion

        #region Helper Methods

        private Combatant CreateTestCombatant(string id)
        {
            var combatant = new Combatant(id, id, Faction.Hostile, 100, 10);
            combatant.Stats = new CombatantStats { BaseAC = 15 };
            return combatant;
        }

        private int FindSeedForNaturalRoll(int targetRoll)
        {
            // Brute force search for a seed that produces the target roll
            for (int seed = 0; seed < 10000; seed++)
            {
                var testEngine = new RulesEngine(seed);
                var testSource = CreateTestCombatant("test");
                var testTarget = CreateTestCombatant("target");

                var testInput = new QueryInput
                {
                    Type = QueryType.AttackRoll,
                    Source = testSource,
                    Target = testTarget,
                    BaseValue = 0
                };

                var testResult = testEngine.RollAttack(testInput);
                if (testResult.NaturalRoll == targetRoll)
                {
                    return seed;
                }
            }

            // Fallback - just return seed that might work
            return targetRoll == 20 ? 1337 : 42;
        }

        #endregion
    }
}
