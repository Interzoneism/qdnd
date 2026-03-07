using QDND.Data.Descriptions;
using Xunit;

namespace QDND.Tests.Unit
{
    public class DescriptionParamResolverTests
    {
        [Fact]
        public void Resolve_PlainNumberSubstitution_ReplacesPlaceholder()
        {
            var result = DescriptionParamResolver.Resolve("Slash at up to [1] enemies", "3");

            Assert.Equal("Slash at up to 3 enemies", result);
        }

        [Fact]
        public void Resolve_DealDamageParam_FormatsDamageText()
        {
            var result = DescriptionParamResolver.Resolve("Deal [1]", "DealDamage(3d6,Fire)");

            Assert.Equal("Deal 3d6 Fire damage", result);
        }

        [Fact]
        public void Resolve_RegainHitPointsParam_FormatsHealingText()
        {
            var result = DescriptionParamResolver.Resolve("Gain [1]", "RegainHitPoints(10)");

            Assert.Equal("Gain 10 hit points", result);
        }

        [Fact]
        public void Resolve_DistanceParam_FormatsMetricAndFeet()
        {
            var result = DescriptionParamResolver.Resolve("Push back [1]", "Distance(5)");

            Assert.Equal("Push back 5m / 16ft", result);
        }

        [Fact]
        public void Resolve_DistanceParam_MeleeReach_FormatsMetricAndFeet()
        {
            var result = DescriptionParamResolver.Resolve("Reach [1]", "Distance(1.5)");

            Assert.Equal("Reach 1.5m / 5ft", result);
        }

        [Fact]
        public void Resolve_DistanceParam_StandardMovement_FormatsMetricAndFeet()
        {
            var result = DescriptionParamResolver.Resolve("Move [1]", "Distance(9)");

            Assert.Equal("Move 9m / 30ft", result);
        }

        [Fact]
        public void Resolve_MultipleParams_ResolvesEachPlaceholder()
        {
            var result = DescriptionParamResolver.Resolve(
                "Deal [1] and gain [2]",
                "DealDamage(1d8,Fire);RegainHitPoints(5)");

            Assert.Equal("Deal 1d8 Fire damage and gain 5 hit points", result);
        }

        [Fact]
        public void Resolve_GainTemporaryHitPointsParam_FormatsTempHpText()
        {
            var result = DescriptionParamResolver.Resolve("Gain [1]", "GainTemporaryHitPoints(8)");

            Assert.Equal("Gain 8 temporary hit points", result);
        }

        [Fact]
        public void Resolve_MultipleParams_WithGainTemporaryHitPoints_ResolvesEachPlaceholder()
        {
            var result = DescriptionParamResolver.Resolve(
                "Gain [1] and deal [2]",
                "GainTemporaryHitPoints(5);DealDamage(5,Cold)");

            Assert.Equal("Gain 5 temporary hit points and deal 5 Cold damage", result);
        }

        [Fact]
        public void Resolve_NoParams_ReturnsDescriptionUnchanged()
        {
            var result = DescriptionParamResolver.Resolve("No placeholders here", string.Empty);

            Assert.Equal("No placeholders here", result);
        }

        [Fact]
        public void Resolve_EmptyDescription_ReturnsEmptyString()
        {
            var result = DescriptionParamResolver.Resolve(string.Empty, "DealDamage(1d4,Fire)");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void Resolve_UnmatchedPlaceholders_AreLeftAsIs()
        {
            var result = DescriptionParamResolver.Resolve("Deal [1] and [2]", "3");

            Assert.Equal("Deal 3 and [2]", result);
        }

        [Fact]
        public void Resolve_ComplexExpressionFallback_PreservesRawExpression()
        {
            var result = DescriptionParamResolver.Resolve(
                "Deal [1]",
                "DealDamage(max(1,StrengthModifier), Bludgeoning)");

            Assert.Equal("Deal DealDamage(max(1,StrengthModifier), Bludgeoning)", result);
        }
    }
}
