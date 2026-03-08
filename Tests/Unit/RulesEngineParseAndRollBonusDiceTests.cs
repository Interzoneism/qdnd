using System;
using System.Reflection;
using QDND.Combat.Rules;
using Xunit;

namespace QDND.Tests.Unit
{
    public class RulesEngineParseAndRollBonusDiceTests
    {
        private static int InvokeParseAndRollBonusDice(RulesEngine engine, string formula)
        {
            var method = typeof(RulesEngine).GetMethod(
                "ParseAndRollBonusDice",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(method);

            object? result = method!.Invoke(engine, new object[] { formula });
            return Assert.IsType<int>(result);
        }

        [Fact]
        public void ParseAndRollBonusDice_FlatPositiveInteger_ReturnsValue()
        {
            var engine = new RulesEngine(seed: 42);

            int result = InvokeParseAndRollBonusDice(engine, "5");

            Assert.Equal(5, result);
        }

        [Fact]
        public void ParseAndRollBonusDice_FlatNegativeInteger_ReturnsNegative()
        {
            var engine = new RulesEngine(seed: 42);

            int result = InvokeParseAndRollBonusDice(engine, "-5");

            Assert.Equal(-5, result);
        }

        [Fact]
        public void ParseAndRollBonusDice_PositiveWithPlus_ReturnsValue()
        {
            var engine = new RulesEngine(seed: 42);

            int result = InvokeParseAndRollBonusDice(engine, "+2");

            Assert.Equal(2, result);
        }

        [Fact]
        public void ParseAndRollBonusDice_DiceFormula_StillWorks()
        {
            var engine = new RulesEngine(seed: 42);

            int result = InvokeParseAndRollBonusDice(engine, "1d4");

            Assert.InRange(result, 1, 4);
        }

        [Fact]
        public void ParseAndRollBonusDice_EmptyString_ReturnsZero()
        {
            var engine = new RulesEngine(seed: 42);

            int result = InvokeParseAndRollBonusDice(engine, string.Empty);

            Assert.Equal(0, result);
        }

        [Fact]
        public void ParseAndRollBonusDice_NegativeDice_StillWorks()
        {
            var engine = new RulesEngine(seed: 42);

            int result = InvokeParseAndRollBonusDice(engine, "-1d4");

            Assert.InRange(result, -4, -1);
        }
    }
}
