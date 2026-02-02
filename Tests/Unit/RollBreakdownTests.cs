using Xunit;
using QDND.Combat.Rules;
using System.Collections.Generic;

namespace QDND.Tests.Unit
{
    public class RollBreakdownTests
    {
        #region BreakdownEntry Tests

        [Fact]
        public void BreakdownEntry_ToString_FormatsPositiveValue()
        {
            var entry = new BreakdownEntry("High Ground", 2, BreakdownCategory.Situational);
            
            var result = entry.ToString();
            
            Assert.Equal("+2 (High Ground)", result);
        }

        [Fact]
        public void BreakdownEntry_ToString_FormatsNegativeValue()
        {
            var entry = new BreakdownEntry("Low Ground", -2, BreakdownCategory.Situational);
            
            var result = entry.ToString();
            
            Assert.Equal("-2 (Low Ground)", result);
        }

        [Fact]
        public void BreakdownEntry_Constructor_SetsAllProperties()
        {
            var entry = new BreakdownEntry("STR", 3, BreakdownCategory.Ability);
            
            Assert.Equal("STR", entry.Source);
            Assert.Equal(3, entry.Value);
            Assert.Equal(BreakdownCategory.Ability, entry.Category);
        }

        #endregion

        #region RollBreakdown Basic Tests

        [Fact]
        public void RollBreakdown_AddModifier_AddsToList()
        {
            var breakdown = new RollBreakdown();
            
            breakdown.AddModifier("STR", 3, BreakdownCategory.Ability);
            breakdown.AddModifier("Proficiency", 2, BreakdownCategory.Proficiency);
            
            Assert.Equal(2, breakdown.Modifiers.Count);
            Assert.Equal("STR", breakdown.Modifiers[0].Source);
            Assert.Equal("Proficiency", breakdown.Modifiers[1].Source);
        }

        [Fact]
        public void RollBreakdown_IsCritical_TrueOnNat20()
        {
            var breakdown = new RollBreakdown { NaturalRoll = 20 };
            
            Assert.True(breakdown.IsCritical);
            Assert.False(breakdown.IsCriticalFailure);
        }

        [Fact]
        public void RollBreakdown_IsCriticalFailure_TrueOnNat1()
        {
            var breakdown = new RollBreakdown { NaturalRoll = 1 };
            
            Assert.True(breakdown.IsCriticalFailure);
            Assert.False(breakdown.IsCritical);
        }

        [Fact]
        public void RollBreakdown_GetTotalModifier_SumsAllModifiers()
        {
            var breakdown = new RollBreakdown();
            breakdown.AddModifier("STR", 3, BreakdownCategory.Ability);
            breakdown.AddModifier("Proficiency", 2, BreakdownCategory.Proficiency);
            breakdown.AddModifier("Bane", -1, BreakdownCategory.Status);
            
            var total = breakdown.GetTotalModifier();
            
            Assert.Equal(4, total); // 3 + 2 - 1
        }

        [Fact]
        public void RollBreakdown_GetCategoryTotal_SumsCorrectCategory()
        {
            var breakdown = new RollBreakdown();
            breakdown.AddModifier("STR", 3, BreakdownCategory.Ability);
            breakdown.AddModifier("DEX", 2, BreakdownCategory.Ability);
            breakdown.AddModifier("High Ground", 2, BreakdownCategory.Situational);
            
            var abilityTotal = breakdown.GetCategoryTotal(BreakdownCategory.Ability);
            var situationalTotal = breakdown.GetCategoryTotal(BreakdownCategory.Situational);
            
            Assert.Equal(5, abilityTotal);
            Assert.Equal(2, situationalTotal);
        }

        [Fact]
        public void RollBreakdown_GetModifiersByCategory_FiltersCorrectly()
        {
            var breakdown = new RollBreakdown();
            breakdown.AddModifier("STR", 3, BreakdownCategory.Ability);
            breakdown.AddModifier("High Ground", 2, BreakdownCategory.Situational);
            breakdown.AddModifier("Cover", -2, BreakdownCategory.Situational);
            
            var situational = breakdown.GetModifiersByCategory(BreakdownCategory.Situational);
            
            Assert.Equal(2, situational.Count);
            Assert.Contains(situational, e => e.Source == "High Ground");
            Assert.Contains(situational, e => e.Source == "Cover");
        }

        #endregion

        #region ToFormattedString Tests

        [Fact]
        public void ToFormattedString_BasicRoll_FormatsCorrectly()
        {
            var breakdown = new RollBreakdown
            {
                NaturalRoll = 15,
                Total = 20
            };
            breakdown.AddModifier("STR", 3, BreakdownCategory.Ability);
            breakdown.AddModifier("Proficiency", 2, BreakdownCategory.Proficiency);
            
            var result = breakdown.ToFormattedString();
            
            Assert.Contains("d20(15)", result);
            Assert.Contains("+3 (STR)", result);
            Assert.Contains("+2 (Proficiency)", result);
            Assert.Contains("= 20", result);
        }

        [Fact]
        public void ToFormattedString_WithAdvantage_ShowsAdvantageRolls()
        {
            var breakdown = new RollBreakdown
            {
                NaturalRoll = 18,
                Total = 23,
                HasAdvantage = true,
                AdvantageRolls = (18, 12)
            };
            breakdown.AddModifier("STR", 5, BreakdownCategory.Ability);
            
            var result = breakdown.ToFormattedString();
            
            Assert.Contains("d20(18|12)", result);
            Assert.Contains("[ADV]", result);
        }

        [Fact]
        public void ToFormattedString_WithDisadvantage_ShowsDisadvantageRolls()
        {
            var breakdown = new RollBreakdown
            {
                NaturalRoll = 8,
                Total = 13,
                HasDisadvantage = true,
                AdvantageRolls = (8, 17)
            };
            breakdown.AddModifier("STR", 5, BreakdownCategory.Ability);
            
            var result = breakdown.ToFormattedString();
            
            Assert.Contains("d20(8|17)", result);
            Assert.Contains("[DIS]", result);
        }

        [Fact]
        public void ToFormattedString_Critical_ShowsCritAnnotation()
        {
            var breakdown = new RollBreakdown
            {
                NaturalRoll = 20,
                Total = 25
            };
            
            var result = breakdown.ToFormattedString();
            
            Assert.Contains("[CRIT]", result);
        }

        [Fact]
        public void ToFormattedString_CriticalFailure_ShowsCritFailAnnotation()
        {
            var breakdown = new RollBreakdown
            {
                NaturalRoll = 1,
                Total = 6
            };
            
            var result = breakdown.ToFormattedString();
            
            Assert.Contains("[CRIT FAIL]", result);
        }

        [Fact]
        public void ToFormattedString_NegativeModifiers_FormatsCorrectly()
        {
            var breakdown = new RollBreakdown
            {
                NaturalRoll = 15,
                Total = 12
            };
            breakdown.AddModifier("Bane", -2, BreakdownCategory.Status);
            breakdown.AddModifier("Cover", -1, BreakdownCategory.Situational);
            
            var result = breakdown.ToFormattedString();
            
            Assert.Contains("-2 (Bane)", result);
            Assert.Contains("-1 (Cover)", result);
        }

        #endregion

        #region FromQueryResult Tests

        [Fact]
        public void FromQueryResult_ConvertsBasicResult()
        {
            var queryResult = new QueryResult
            {
                NaturalRoll = 15,
                FinalValue = 20,
                AdvantageState = 0,
                AppliedModifiers = new List<Modifier>
                {
                    new Modifier { Name = "Strength", Value = 3, Source = "STR" },
                    new Modifier { Name = "Prof", Value = 2, Source = "Proficiency" }
                }
            };
            
            var breakdown = RollBreakdown.FromQueryResult(queryResult);
            
            Assert.Equal(15, breakdown.NaturalRoll);
            Assert.Equal(20, breakdown.Total);
            Assert.False(breakdown.HasAdvantage);
            Assert.False(breakdown.HasDisadvantage);
            Assert.Equal(2, breakdown.Modifiers.Count);
        }

        [Fact]
        public void FromQueryResult_SetsAdvantageFromState()
        {
            var queryResultAdv = new QueryResult
            {
                NaturalRoll = 18,
                FinalValue = 23,
                AdvantageState = 1,
                RollValues = new[] { 18, 12 },
                AppliedModifiers = new List<Modifier>()
            };
            
            var breakdownAdv = RollBreakdown.FromQueryResult(queryResultAdv);
            
            Assert.True(breakdownAdv.HasAdvantage);
            Assert.False(breakdownAdv.HasDisadvantage);
            Assert.NotNull(breakdownAdv.AdvantageRolls);
            Assert.Equal(18, breakdownAdv.AdvantageRolls.Value.Used);
            Assert.Equal(12, breakdownAdv.AdvantageRolls.Value.Discarded);
        }

        [Fact]
        public void FromQueryResult_SetsDisadvantageFromState()
        {
            var queryResult = new QueryResult
            {
                NaturalRoll = 8,
                FinalValue = 13,
                AdvantageState = -1,
                RollValues = new[] { 17, 8 },
                AppliedModifiers = new List<Modifier>()
            };
            
            var breakdown = RollBreakdown.FromQueryResult(queryResult);
            
            Assert.False(breakdown.HasAdvantage);
            Assert.True(breakdown.HasDisadvantage);
            Assert.NotNull(breakdown.AdvantageRolls);
            Assert.Equal(8, breakdown.AdvantageRolls.Value.Used);
            Assert.Equal(17, breakdown.AdvantageRolls.Value.Discarded);
        }

        [Fact]
        public void FromQueryResult_CategorizesSituationalModifiers()
        {
            var queryResult = new QueryResult
            {
                NaturalRoll = 15,
                FinalValue = 17,
                AppliedModifiers = new List<Modifier>
                {
                    new Modifier { Name = "Height", Value = 2, Source = "High Ground" }
                }
            };
            
            var breakdown = RollBreakdown.FromQueryResult(queryResult);
            
            Assert.Single(breakdown.Modifiers);
            Assert.Equal(BreakdownCategory.Situational, breakdown.Modifiers[0].Category);
        }

        [Fact]
        public void FromQueryResult_CategorizesAbilityModifiers()
        {
            var queryResult = new QueryResult
            {
                NaturalRoll = 15,
                FinalValue = 18,
                AppliedModifiers = new List<Modifier>
                {
                    new Modifier { Name = "STR", Value = 3, Source = "Strength" }
                }
            };
            
            var breakdown = RollBreakdown.FromQueryResult(queryResult);
            
            Assert.Single(breakdown.Modifiers);
            Assert.Equal(BreakdownCategory.Ability, breakdown.Modifiers[0].Category);
        }

        [Fact]
        public void FromQueryResult_CategorizesStatusModifiers()
        {
            var queryResult = new QueryResult
            {
                NaturalRoll = 15,
                FinalValue = 19,
                AppliedModifiers = new List<Modifier>
                {
                    new Modifier { Name = "Blessed", Value = 4, Source = "Bless" }
                }
            };
            
            var breakdown = RollBreakdown.FromQueryResult(queryResult);
            
            Assert.Single(breakdown.Modifiers);
            Assert.Equal(BreakdownCategory.Status, breakdown.Modifiers[0].Category);
        }

        #endregion

        #region Integration with RulesEngine Tests

        [Fact]
        public void RulesEngine_RollAttack_PopulatesBreakdown()
        {
            var engine = new RulesEngine(42);
            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                BaseValue = 5,
                DC = 15
            };
            
            var result = engine.RollAttack(input);
            
            Assert.NotNull(result.Breakdown);
            Assert.Equal(result.NaturalRoll, result.Breakdown.NaturalRoll);
            Assert.Equal((int)result.FinalValue, result.Breakdown.Total);
        }

        [Fact]
        public void RulesEngine_RollAttack_BreakdownMatchesResult()
        {
            var engine = new RulesEngine(123);
            engine.AddGlobalModifier(Modifier.Flat("Test Bonus", ModifierTarget.AttackRoll, 2, "Test"));
            
            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                BaseValue = 3,
                DC = 12
            };
            
            var result = engine.RollAttack(input);
            
            Assert.NotNull(result.Breakdown);
            // Breakdown total should equal result's final value
            Assert.Equal((int)result.FinalValue, result.Breakdown.Total);
            // Should have at least the test modifier 
            Assert.True(result.Breakdown.Modifiers.Count >= 1);
        }

        [Fact]
        public void RulesEngine_RollAttack_HeightModifierInBreakdown()
        {
            var engine = new RulesEngine(42);
            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                BaseValue = 5,
                DC = 15,
                Parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "heightModifier", 2 }
                }
            };
            
            var result = engine.RollAttack(input);
            
            Assert.NotNull(result.Breakdown);
            Assert.Contains(result.Breakdown.Modifiers, m => m.Source == "High Ground");
        }

        [Fact]
        public void RulesEngine_RollSave_PopulatesBreakdown()
        {
            var engine = new RulesEngine(42);
            var input = new QueryInput
            {
                Type = QueryType.SavingThrow,
                BaseValue = 3,
                DC = 14
            };
            
            var result = engine.RollSave(input);
            
            Assert.NotNull(result.Breakdown);
            Assert.Equal(result.NaturalRoll, result.Breakdown.NaturalRoll);
            Assert.Equal((int)result.FinalValue, result.Breakdown.Total);
        }

        [Fact]
        public void RulesEngine_RollWithAdvantage_BreakdownShowsAdvantage()
        {
            var engine = new RulesEngine(42);
            engine.AddGlobalModifier(Modifier.Advantage("Lucky", ModifierTarget.AttackRoll, "Test"));
            
            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                BaseValue = 5,
                DC = 15
            };
            
            var result = engine.RollAttack(input);
            
            Assert.NotNull(result.Breakdown);
            Assert.True(result.Breakdown.HasAdvantage);
            Assert.NotNull(result.Breakdown.AdvantageRolls);
        }

        [Fact]
        public void RulesEngine_RollWithDisadvantage_BreakdownShowsDisadvantage()
        {
            var engine = new RulesEngine(42);
            engine.AddGlobalModifier(Modifier.Disadvantage("Blinded", ModifierTarget.AttackRoll, "Test"));
            
            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                BaseValue = 5,
                DC = 15
            };
            
            var result = engine.RollAttack(input);
            
            Assert.NotNull(result.Breakdown);
            Assert.True(result.Breakdown.HasDisadvantage);
            Assert.NotNull(result.Breakdown.AdvantageRolls);
        }

        [Fact]
        public void Breakdown_ToFormattedString_ProducesReadableOutput()
        {
            var engine = new RulesEngine(42);
            engine.AddGlobalModifier(Modifier.Flat("Bless", ModifierTarget.AttackRoll, 2, "Bless spell"));
            
            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                BaseValue = 5,
                DC = 15,
                Parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "heightModifier", 2 }
                }
            };
            
            var result = engine.RollAttack(input);
            var formatted = result.Breakdown.ToFormattedString();
            
            // Should be a non-empty, readable string
            Assert.False(string.IsNullOrWhiteSpace(formatted));
            Assert.Contains("d20", formatted);
            Assert.Contains("=", formatted);
        }

        #endregion

        #region Category Assignment Tests

        [Theory]
        [InlineData("STR", BreakdownCategory.Ability)]
        [InlineData("Strength", BreakdownCategory.Ability)]
        [InlineData("DEX", BreakdownCategory.Ability)]
        [InlineData("Dexterity", BreakdownCategory.Ability)]
        [InlineData("CON", BreakdownCategory.Ability)]
        [InlineData("INT", BreakdownCategory.Ability)]
        [InlineData("WIS", BreakdownCategory.Ability)]
        [InlineData("CHA", BreakdownCategory.Ability)]
        public void CategorizeModifier_AbilityScores_Detected(string source, BreakdownCategory expected)
        {
            var queryResult = new QueryResult
            {
                NaturalRoll = 10,
                FinalValue = 13,
                AppliedModifiers = new List<Modifier>
                {
                    new Modifier { Name = source, Value = 3, Source = source }
                }
            };
            
            var breakdown = RollBreakdown.FromQueryResult(queryResult);
            
            Assert.Equal(expected, breakdown.Modifiers[0].Category);
        }

        [Theory]
        [InlineData("Proficiency", BreakdownCategory.Proficiency)]
        [InlineData("Proficiency Bonus", BreakdownCategory.Proficiency)]
        public void CategorizeModifier_Proficiency_Detected(string source, BreakdownCategory expected)
        {
            var queryResult = new QueryResult
            {
                NaturalRoll = 10,
                FinalValue = 12,
                AppliedModifiers = new List<Modifier>
                {
                    new Modifier { Name = source, Value = 2, Source = source }
                }
            };
            
            var breakdown = RollBreakdown.FromQueryResult(queryResult);
            
            Assert.Equal(expected, breakdown.Modifiers[0].Category);
        }

        [Theory]
        [InlineData("High Ground", BreakdownCategory.Situational)]
        [InlineData("Low Ground", BreakdownCategory.Situational)]
        [InlineData("Half Cover", BreakdownCategory.Situational)]
        [InlineData("Three-Quarters Cover", BreakdownCategory.Situational)]
        [InlineData("Flanking", BreakdownCategory.Situational)]
        [InlineData("Prone", BreakdownCategory.Situational)]
        public void CategorizeModifier_Situational_Detected(string source, BreakdownCategory expected)
        {
            var queryResult = new QueryResult
            {
                NaturalRoll = 10,
                FinalValue = 12,
                AppliedModifiers = new List<Modifier>
                {
                    new Modifier { Name = source, Value = 2, Source = source }
                }
            };
            
            var breakdown = RollBreakdown.FromQueryResult(queryResult);
            
            Assert.Equal(expected, breakdown.Modifiers[0].Category);
        }

        [Theory]
        [InlineData("Bless", BreakdownCategory.Status)]
        [InlineData("Bane", BreakdownCategory.Status)]
        [InlineData("Curse", BreakdownCategory.Status)]
        [InlineData("Poisoned", BreakdownCategory.Status)]
        [InlineData("Frightened", BreakdownCategory.Status)]
        public void CategorizeModifier_Status_Detected(string source, BreakdownCategory expected)
        {
            var queryResult = new QueryResult
            {
                NaturalRoll = 10,
                FinalValue = 12,
                AppliedModifiers = new List<Modifier>
                {
                    new Modifier { Name = source, Value = 2, Source = source }
                }
            };
            
            var breakdown = RollBreakdown.FromQueryResult(queryResult);
            
            Assert.Equal(expected, breakdown.Modifiers[0].Category);
        }

        [Theory]
        [InlineData("Weapon", BreakdownCategory.Equipment)]
        [InlineData("Magic Sword", BreakdownCategory.Equipment)]
        [InlineData("Shield", BreakdownCategory.Equipment)]
        [InlineData("Ring of Protection", BreakdownCategory.Equipment)]
        public void CategorizeModifier_Equipment_Detected(string source, BreakdownCategory expected)
        {
            var queryResult = new QueryResult
            {
                NaturalRoll = 10,
                FinalValue = 12,
                AppliedModifiers = new List<Modifier>
                {
                    new Modifier { Name = source, Value = 2, Source = source }
                }
            };
            
            var breakdown = RollBreakdown.FromQueryResult(queryResult);
            
            Assert.Equal(expected, breakdown.Modifiers[0].Category);
        }

        #endregion
    }
}
