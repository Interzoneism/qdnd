using Xunit;
using QDND.Combat.Rules.Functors;
using System.Linq;

namespace QDND.Tests.Unit
{
    public class StatsFunctorParserTests
    {
        [Fact]
        public void ParseSingleDealDamage_Success()
        {
            // BG3 example: DealDamage(1d6,Fire)
            var result = FunctorParser.ParseFunctors("DealDamage(1d6,Fire)");

            Assert.Single(result);
            var action = result[0];
            Assert.Equal(FunctorType.DealDamage, action.Type);
            Assert.Equal(2, action.Parameters.Length);
            Assert.Equal("1d6", action.Parameters[0]);
            Assert.Equal("Fire", action.Parameters[1]);
        }

        [Fact]
        public void ParseDealDamageWithDamageType_Success()
        {
            // BG3 example: DealDamage(2,MainMeleeWeaponDamageType)
            var result = FunctorParser.ParseFunctors("DealDamage(2,MainMeleeWeaponDamageType)");

            Assert.Single(result);
            var action = result[0];
            Assert.Equal(FunctorType.DealDamage, action.Type);
            Assert.Equal(2, action.Parameters.Length);
            Assert.Equal("2", action.Parameters[0]);
            Assert.Equal("MainMeleeWeaponDamageType", action.Parameters[1]);
        }

        [Fact]
        public void ParseApplyStatus_Success()
        {
            // BG3 example: ApplyStatus(BLESSED,100,2)
            var result = FunctorParser.ParseFunctors("ApplyStatus(BLESSED,100,2)");

            Assert.Single(result);
            var action = result[0];
            Assert.Equal(FunctorType.ApplyStatus, action.Type);
            Assert.Equal(3, action.Parameters.Length);
            Assert.Equal("BLESSED", action.Parameters[0]);
            Assert.Equal("100", action.Parameters[1]);
            Assert.Equal("2", action.Parameters[2]);
        }

        [Fact]
        public void ParseRegainHitPoints_Success()
        {
            // BG3 example: RegainHitPoints(1d10+5)
            var result = FunctorParser.ParseFunctors("RegainHitPoints(1d10+5)");

            Assert.Single(result);
            var action = result[0];
            Assert.Equal(FunctorType.RegainHitPoints, action.Type);
            Assert.Single(action.Parameters);
            Assert.Equal("1d10+5", action.Parameters[0]);
        }

        [Fact]
        public void ParseRemoveStatus_Success()
        {
            // BG3 example: RemoveStatus(SG_Poisoned)
            var result = FunctorParser.ParseFunctors("RemoveStatus(SG_Poisoned)");

            Assert.Single(result);
            var action = result[0];
            Assert.Equal(FunctorType.RemoveStatus, action.Type);
            Assert.Single(action.Parameters);
            Assert.Equal("SG_Poisoned", action.Parameters[0]);
        }

        [Fact]
        public void ParseRestoreResource_Success()
        {
            // BG3 example: RestoreResource(ActionPoint,1,0)
            var result = FunctorParser.ParseFunctors("RestoreResource(ActionPoint,1,0)");

            Assert.Single(result);
            var action = result[0];
            Assert.Equal(FunctorType.RestoreResource, action.Type);
            Assert.Equal(3, action.Parameters.Length);
            Assert.Equal("ActionPoint", action.Parameters[0]);
            Assert.Equal("1", action.Parameters[1]);
            Assert.Equal("0", action.Parameters[2]);
        }

        [Fact]
        public void ParseMultipleFunctors_Success()
        {
            // BG3 example: multiple functors separated by semicolons
            var input = "DealDamage(1d6,Fire);ApplyStatus(BURNING,100,2)";
            var result = FunctorParser.ParseFunctors(input);

            Assert.Equal(2, result.Count);
            Assert.Equal(FunctorType.DealDamage, result[0].Type);
            Assert.Equal(FunctorType.ApplyStatus, result[1].Type);
        }

        [Fact]
        public void ParseEmptyString_ReturnsEmpty()
        {
            var result = FunctorParser.ParseFunctors("");
            Assert.Empty(result);

            var result2 = FunctorParser.ParseFunctors(null);
            Assert.Empty(result2);
        }

        [Fact]
        public void ParseWithWhitespace_Success()
        {
            var result = FunctorParser.ParseFunctors("DealDamage( 1d6 , Fire )");

            Assert.Single(result);
            var action = result[0];
            Assert.Equal(FunctorType.DealDamage, action.Type);
            Assert.Equal("1d6", action.Parameters[0]);
            Assert.Equal("Fire", action.Parameters[1]);
        }

        [Fact]
        public void ParseComplexFormula_Success()
        {
            // BG3 example: RegainHitPoints(1d10+FighterLevel)
            var result = FunctorParser.ParseFunctors("RegainHitPoints(1d10+FighterLevel)");

            Assert.Single(result);
            var action = result[0];
            Assert.Equal(FunctorType.RegainHitPoints, action.Type);
            Assert.Equal("1d10+FighterLevel", action.Parameters[0]);
        }
    }
}
