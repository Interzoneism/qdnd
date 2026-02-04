using System.Collections.Generic;
using Xunit;
using QDND.Combat.Rules;
using QDND.Combat.Abilities.Effects;
using QDND.Combat.Entities;
using QDND.Combat.Statuses;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for standardized damage type handling
    /// </summary>
    public class DamageTypeTaggingTests
    {
        #region NormalizeType Tests

        [Fact]
        public void NormalizeType_ReturnsLowercase()
        {
            // Mixed case inputs should be normalized to lowercase
            Assert.Equal("fire", DamageTypes.NormalizeType("Fire"));
            Assert.Equal("fire", DamageTypes.NormalizeType("FIRE"));
            Assert.Equal("fire", DamageTypes.NormalizeType("FiRe"));
        }

        [Fact]
        public void NormalizeType_TrimsWhitespace()
        {
            // Whitespace should be trimmed from input
            Assert.Equal("fire", DamageTypes.NormalizeType("  fire  "));
            Assert.Equal("fire", DamageTypes.NormalizeType("\tfire\n"));
            Assert.Equal("cold", DamageTypes.NormalizeType(" Cold "));
        }

        [Fact]
        public void NormalizeType_ReturnsUntypedForNullOrEmpty()
        {
            // Null or empty input should return "untyped"
            Assert.Equal("untyped", DamageTypes.NormalizeType(null));
            Assert.Equal("untyped", DamageTypes.NormalizeType(""));
            Assert.Equal("untyped", DamageTypes.NormalizeType("   "));
            Assert.Equal("untyped", DamageTypes.NormalizeType("\t"));
        }

        #endregion

        #region ToTag Tests

        [Fact]
        public void ToTag_ProducesCorrectFormat()
        {
            // ToTag should produce "damage:<typeId>" format
            Assert.Equal("damage:fire", DamageTypes.ToTag("fire"));
            Assert.Equal("damage:cold", DamageTypes.ToTag("cold"));
            Assert.Equal("damage:lightning", DamageTypes.ToTag("lightning"));
        }

        [Fact]
        public void ToTag_NormalizesInputFirst()
        {
            // ToTag should normalize mixed case and whitespace
            Assert.Equal("damage:fire", DamageTypes.ToTag("Fire"));
            Assert.Equal("damage:fire", DamageTypes.ToTag("  FIRE  "));
            Assert.Equal("damage:poison", DamageTypes.ToTag("Poison"));
        }

        [Fact]
        public void ToTag_HandlesNullAsUntyped()
        {
            // Null input should produce "damage:untyped"
            Assert.Equal("damage:untyped", DamageTypes.ToTag(null));
            Assert.Equal("damage:untyped", DamageTypes.ToTag(""));
        }

        #endregion

        #region Integration with Rules Engine

        [Fact]
        public void ConditionalModifier_AppliesOnlyToFireDamage()
        {
            // Create a rules engine with a fire damage modifier
            var rules = new RulesEngine(42);

            // Create source and target combatants
            var source = new Combatant("source", "Source", Faction.Player, 10, 10);
            var target = new Combatant("target", "Target", Faction.Hostile, 10, 5);

            // Add a conditional modifier that only applies to fire damage: +5 damage
            var fireBoostModifier = Modifier.Flat("Fire Boost", ModifierTarget.DamageDealt, 5, "test");
            fireBoostModifier.Condition = ctx => ctx.Tags.Contains(DamageTypes.ToTag("fire"));
            rules.AddModifier(source.Id, fireBoostModifier);

            // Test fire damage (should get +5)
            var fireQuery = new QueryInput
            {
                Type = QueryType.DamageRoll,
                Source = source,
                Target = target,
                BaseValue = 10
            };
            fireQuery.Tags.Add(DamageTypes.ToTag("fire"));

            var fireResult = rules.RollDamage(fireQuery);
            Assert.Equal(15, fireResult.FinalValue); // 10 base + 5 from modifier

            // Test cold damage (should NOT get +5)
            var coldQuery = new QueryInput
            {
                Type = QueryType.DamageRoll,
                Source = source,
                Target = target,
                BaseValue = 10
            };
            coldQuery.Tags.Add(DamageTypes.ToTag("cold"));

            var coldResult = rules.RollDamage(coldQuery);
            Assert.Equal(10, coldResult.FinalValue); // 10 base, no modifier
        }

        #endregion

        #region Constants Tests

        [Fact]
        public void Constants_AreAllLowercase()
        {
            // Verify all constants are lowercase
            Assert.Equal("fire", DamageTypes.Fire);
            Assert.Equal("cold", DamageTypes.Cold);
            Assert.Equal("lightning", DamageTypes.Lightning);
            Assert.Equal("poison", DamageTypes.Poison);
            Assert.Equal("acid", DamageTypes.Acid);
            Assert.Equal("thunder", DamageTypes.Thunder);
            Assert.Equal("necrotic", DamageTypes.Necrotic);
            Assert.Equal("radiant", DamageTypes.Radiant);
            Assert.Equal("psychic", DamageTypes.Psychic);
            Assert.Equal("force", DamageTypes.Force);
            Assert.Equal("bludgeoning", DamageTypes.Bludgeoning);
            Assert.Equal("piercing", DamageTypes.Piercing);
            Assert.Equal("slashing", DamageTypes.Slashing);
            Assert.Equal("untyped", DamageTypes.Untyped);
        }

        #endregion
    }
}
