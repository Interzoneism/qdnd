using Xunit;
using QDND.Combat.Rules;
using System.Linq;
using System.Collections.Generic;

namespace QDND.Tests.Unit
{
    public class BreakdownPayloadTests
    {
        [Fact]
        public void Add_ComponentIncluded()
        {
            var payload = new BreakdownPayload { BaseValue = 10 };
            payload.Add("Strength", 3);

            Assert.Single(payload.Components);
            Assert.Equal("Strength", payload.Components[0].Source);
            Assert.Equal(3, payload.Components[0].Value);
        }

        [Fact]
        public void AddMultiplier_AppliesCorrectly()
        {
            var payload = new BreakdownPayload { BaseValue = 10 };
            payload.AddMultiplier("Vulnerability", 2.0f, "Double damage");

            Assert.Single(payload.Components);
            Assert.True(payload.Components[0].IsMultiplier);
            Assert.Equal(2.0f, payload.Components[0].Value);
        }

        [Fact]
        public void Calculate_SumsAdditiveModifiers()
        {
            var payload = new BreakdownPayload { BaseValue = 10 };
            payload.Add("Strength", 3);
            payload.Add("Magic", 2);
            payload.Add("Bane", -1);

            float result = payload.Calculate();

            Assert.Equal(14, result); // 10 + 3 + 2 - 1
            Assert.Equal(14, payload.FinalValue);
        }

        [Fact]
        public void Calculate_AppliesMultipliers()
        {
            var payload = new BreakdownPayload { BaseValue = 10 };
            payload.Add("Strength", 3);
            payload.AddMultiplier("Critical", 2.0f);

            float result = payload.Calculate();

            Assert.Equal(26, result); // (10 + 3) * 2
            Assert.Equal(26, payload.FinalValue);
        }

        [Fact]
        public void AttackRoll_Factory_SetsCritical()
        {
            var payload = BreakdownPayload.AttackRoll(20, 5, 15);

            Assert.Equal(BreakdownType.AttackRoll, payload.Type);
            Assert.Equal(20, payload.DieRoll);
            Assert.True(payload.IsCritical);
            Assert.True(payload.Success);
        }

        [Fact]
        public void AttackRoll_Factory_DeterminesSuccess()
        {
            var hit = BreakdownPayload.AttackRoll(15, 3, 17);
            var miss = BreakdownPayload.AttackRoll(10, 3, 17);
            var critFail = BreakdownPayload.AttackRoll(1, 10, 5);

            Assert.True(hit.Success); // 15 + 3 = 18 >= 17
            Assert.False(miss.Success); // 10 + 3 = 13 < 17
            Assert.False(critFail.Success); // Critical failure always fails
            Assert.True(critFail.IsCriticalFailure);
        }

        [Fact]
        public void DamageRoll_Factory_IncludesBonus()
        {
            var payload = BreakdownPayload.DamageRoll(8, 1, 8, 3);

            Assert.Equal(BreakdownType.DamageRoll, payload.Type);
            Assert.Equal(8, payload.DieRoll);
            Assert.Equal(1, payload.DiceCount);
            Assert.Equal(8, payload.DieSize);
            Assert.Equal(11, payload.FinalValue); // 8 + 3
        }

        [Fact]
        public void SavingThrow_Factory_ChecksVsDC()
        {
            var success = BreakdownPayload.SavingThrow("Dexterity", 15, 2, 15);
            var failure = BreakdownPayload.SavingThrow("Constitution", 8, 1, 15);

            Assert.Equal(BreakdownType.SavingThrow, success.Type);
            Assert.True(success.Success); // 15 + 2 = 17 >= 15
            Assert.False(failure.Success); // 8 + 1 = 9 < 15
        }

        [Fact]
        public void Format_IncludesAllComponents()
        {
            var payload = new BreakdownPayload
            {
                Type = BreakdownType.AttackRoll,
                Label = "Longsword Attack",
                BaseValue = 12,
                DieRoll = 12,
                DiceCount = 1,
                DieSize = 20
            };
            payload.Add("Strength", 4);
            payload.Add("Proficiency", 2);
            payload.Calculate();

            string formatted = payload.Format();

            Assert.Contains("Longsword Attack", formatted);
            Assert.Contains("Roll: 12", formatted);
            Assert.Contains("Base: 12", formatted);
            Assert.Contains("Strength", formatted);
            Assert.Contains("Proficiency", formatted);
            Assert.Contains("Total: 18", formatted);
        }

        [Fact]
        public void ToJson_ValidFormat()
        {
            var payload = new BreakdownPayload
            {
                Type = BreakdownType.DamageRoll,
                BaseValue = 10,
                FinalValue = 15
            };
            payload.Add("Bonus", 5);

            string json = payload.ToJson();

            Assert.NotEmpty(json);
            Assert.Contains("DamageRoll", json);
            Assert.Contains("10", json);
            Assert.Contains("15", json);
        }

        [Fact]
        public void ToDictionary_ContainsAllFields()
        {
            var payload = new BreakdownPayload
            {
                Type = BreakdownType.SavingThrow,
                BaseValue = 12,
                FinalValue = 15,
                DieRoll = 12,
                Target = 14,
                Success = true
            };
            payload.Add("Wisdom", 3);

            var dict = payload.ToDictionary();

            Assert.Equal("SavingThrow", dict["type"]);
            Assert.Equal(12f, dict["base"]);
            Assert.Equal(15f, dict["final"]);
            Assert.Equal(12, dict["roll"]);
            Assert.Equal(14, dict["target"]);
            Assert.True((bool)dict["success"]);

            var modifiers = dict["modifiers"] as List<Dictionary<string, object>>;
            Assert.NotNull(modifiers);
            Assert.Single(modifiers);
            Assert.Equal("Wisdom", modifiers[0]["source"]);
        }

        [Fact]
        public void Advantage_TrackedInPayload()
        {
            var payload = new BreakdownPayload
            {
                DieRoll = 18,
                HasAdvantage = true,
                AdvantageRolls = (18, 12)
            };

            string formatted = payload.Format();

            Assert.Contains("Advantage", formatted);
            Assert.Contains("used 18", formatted);
            Assert.Contains("discarded 12", formatted);
        }

        [Fact]
        public void GetTotalModifier_SumsAdditiveOnly()
        {
            var payload = new BreakdownPayload { BaseValue = 10 };
            payload.Add("Strength", 3);
            payload.Add("Magic", 2);
            payload.AddMultiplier("Critical", 2.0f);

            float modifier = payload.GetTotalModifier();

            Assert.Equal(5, modifier); // Only 3 + 2, not the multiplier
        }

        [Fact]
        public void GetTotalMultiplier_CombinesMultipliers()
        {
            var payload = new BreakdownPayload { BaseValue = 10 };
            payload.AddMultiplier("Critical", 2.0f);
            payload.AddMultiplier("Vulnerability", 2.0f);
            payload.Add("Strength", 3); // Should be ignored

            float multiplier = payload.GetTotalMultiplier();

            Assert.Equal(4.0f, multiplier); // 2.0 * 2.0
        }

        [Fact]
        public void ArmorClass_Factory_CreatesBasicBreakdown()
        {
            var payload = BreakdownPayload.ArmorClass(15);

            Assert.Equal(BreakdownType.ArmorClass, payload.Type);
            Assert.Equal(15, payload.BaseValue);
            Assert.Equal(15, payload.FinalValue);
            Assert.Equal("Armor Class", payload.Label);
        }

        [Fact]
        public void BreakdownComponent_ToString_FormatsCorrectly()
        {
            var additive = new BreakdownComponent("Strength", 3, "modifier", "Strength bonus");
            var multiplier = new BreakdownComponent
            {
                Source = "Critical",
                Value = 2.0f,
                IsMultiplier = true,
                Description = "Critical hit"
            };

            Assert.Contains("Strength bonus", additive.ToString());
            Assert.Contains("+3", additive.ToString());
            Assert.Contains("Critical hit", multiplier.ToString());
            Assert.Contains("x2", multiplier.ToString());
        }

        [Fact]
        public void Calculate_MultipleMultipliers_AppliesSequentially()
        {
            var payload = new BreakdownPayload { BaseValue = 10 };
            payload.Add("Bonus", 5);
            payload.AddMultiplier("First", 2.0f);
            payload.AddMultiplier("Second", 1.5f);

            float result = payload.Calculate();

            // (10 + 5) * 2.0 * 1.5 = 45
            Assert.Equal(45, result);
        }

        [Fact]
        public void Notes_CanBeAdded()
        {
            var payload = new BreakdownPayload();
            payload.Notes.Add("Target has cover");
            payload.Notes.Add("Attacker prone");

            string formatted = payload.Format();

            Assert.Contains("* Target has cover", formatted);
            Assert.Contains("* Attacker prone", formatted);
        }
    }
}
