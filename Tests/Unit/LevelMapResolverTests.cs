using Xunit;
using QDND.Combat.Rules;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for LevelMapResolver — verifies that RageDamage and SneakAttackDamage
    /// resolve correctly at every breakpoint level.
    /// </summary>
    public class LevelMapResolverTests
    {
        // ── RageDamage ──────────────────────────────────────────────────────────

        [Theory]
        [InlineData(1,  "2")]
        [InlineData(8,  "2")]
        [InlineData(9,  "3")]
        [InlineData(15, "3")]
        [InlineData(16, "4")]
        [InlineData(20, "4")]
        public void RageDamage_ReturnsCorrectBonus(int barbarianLevel, string expected)
        {
            string result = LevelMapResolver.Resolve("RageDamage", barbarianLevel);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void RageDamage_IsCaseInsensitive()
        {
            Assert.Equal("3", LevelMapResolver.Resolve("ragedamage", 9));
            Assert.Equal("3", LevelMapResolver.Resolve("RAGEDAMAGE", 9));
            Assert.Equal("3", LevelMapResolver.Resolve("RageDamage", 9));
        }

        [Fact]
        public void RageDamage_GetClassForMap_ReturnsBarbarian()
        {
            Assert.Equal("Barbarian", LevelMapResolver.GetClassForMap("RageDamage"));
        }

        // ── SneakAttackDamage ───────────────────────────────────────────────────

        [Theory]
        [InlineData(1,  "1d6")]
        [InlineData(2,  "1d6")]
        [InlineData(3,  "2d6")]
        [InlineData(4,  "2d6")]
        [InlineData(5,  "3d6")]
        [InlineData(9,  "5d6")]
        [InlineData(10, "5d6")]
        [InlineData(11, "6d6")]
        [InlineData(20, "10d6")]
        public void SneakAttackDamage_ReturnsCorrectDice(int rogueLevel, string expected)
        {
            string result = LevelMapResolver.Resolve("SneakAttackDamage", rogueLevel);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void SneakAttackDamage_GetClassForMap_ReturnsRogue()
        {
            Assert.Equal("Rogue", LevelMapResolver.GetClassForMap("SneakAttackDamage"));
        }

        // ── Unknown map ─────────────────────────────────────────────────────────

        [Fact]
        public void UnknownMap_ReturnsZero()
        {
            Assert.Equal("0", LevelMapResolver.Resolve("UnknownMap", 10));
        }

        [Fact]
        public void UnknownMap_GetClassForMap_ReturnsNull()
        {
            Assert.Null(LevelMapResolver.GetClassForMap("UnknownMap"));
        }
    }
}

