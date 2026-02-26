using System.Collections.Generic;
using Xunit;
using QDND.Combat.Rules;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Unit
{
    public class ResistVulnImmunityTests
    {
        [Fact]
        public void Immunity_ResultsInZeroDamage()
        {
            // Arrange: 20 fire damage, target immune to fire
            int baseDamage = 20;
            var immunity = DamageResistance.CreateImmunity(DamageTypes.Fire, "Fire Elemental");
            var modifiers = new List<Modifier> { immunity };

            var context = new ModifierContext();
            context.Tags.Add(DamageTypes.ToTag(DamageTypes.Fire));

            // Apply context to modifiers manually (simulate pipeline using context)
            var applicableModifiers = modifiers
                .FindAll(m => m.Condition == null || m.Condition(context));

            // Act
            var result = DamagePipeline.Calculate(baseDamage, applicableModifiers, 0, 100);

            // Assert
            Assert.Equal(0, result.FinalDamage); // Immunity should result in 0 damage
            Assert.Equal(0, result.AppliedToHP);
        }

        [Fact]
        public void Resistance_HalvesDamage_RoundingDown()
        {
            // Arrange: 17 cold damage (odd number to test rounding), target resistant
            int baseDamage = 17;
            var resistance = DamageResistance.CreateResistance(DamageTypes.Cold, "Arctic Troll");
            var modifiers = new List<Modifier> { resistance };

            var context = new ModifierContext();
            context.Tags.Add(DamageTypes.ToTag(DamageTypes.Cold));

            var applicableModifiers = modifiers
                .FindAll(m => m.Condition == null || m.Condition(context));

            // Act
            var result = DamagePipeline.Calculate(baseDamage, applicableModifiers, 0, 100);

            // Assert: 17 * 0.5 = 8.5, Math.Round rounds to 8 (banker's rounding) or 9 (away from zero)
            // DamagePipeline uses Math.Round which defaults to MidpointRounding.ToEven (banker's rounding)
            // 8.5 rounds to 8 (nearest even)
            Assert.Equal(8, result.FinalDamage); // Resistance should halve damage (17 -> 8)
        }

        [Fact]
        public void Vulnerability_DoublesDamage()
        {
            // Arrange: 15 lightning damage, target vulnerable
            int baseDamage = 15;
            var vulnerability = DamageResistance.CreateVulnerability(DamageTypes.Lightning, "Metal Armor");
            var modifiers = new List<Modifier> { vulnerability };

            var context = new ModifierContext();
            context.Tags.Add(DamageTypes.ToTag(DamageTypes.Lightning));

            var applicableModifiers = modifiers
                .FindAll(m => m.Condition == null || m.Condition(context));

            // Act
            var result = DamagePipeline.Calculate(baseDamage, applicableModifiers, 0, 100);

            // Assert
            Assert.Equal(30, result.FinalDamage); // Vulnerability should double damage (15 -> 30)
        }

        [Fact]
        public void Resistance_DoesNotAffectDifferentDamageType()
        {
            // Arrange: 20 fire damage, but target only resistant to cold
            int baseDamage = 20;
            var coldResist = DamageResistance.CreateResistance(DamageTypes.Cold, "Arctic Troll");
            var modifiers = new List<Modifier> { coldResist };

            var context = new ModifierContext();
            context.Tags.Add(DamageTypes.ToTag(DamageTypes.Fire)); // Fire damage, not cold

            var applicableModifiers = modifiers
                .FindAll(m => m.Condition == null || m.Condition(context));

            // Act
            var result = DamagePipeline.Calculate(baseDamage, applicableModifiers, 0, 100);

            // Assert
            Assert.Equal(20, result.FinalDamage); // Cold resistance should not affect fire damage
        }

        [Fact]
        public void ImmunityOverridesResistance_WhenBothApply()
        {
            // Arrange: Apply both immunity and resistance to fire
            // Immunity has priority 50, resistance has priority 100
            // Lower priority applies first, so immunity applies first and should win
            int baseDamage = 20;
            var immunity = DamageResistance.CreateImmunity(DamageTypes.Fire, "Elemental");
            var resistance = DamageResistance.CreateResistance(DamageTypes.Fire, "Ring");
            var modifiers = new List<Modifier> { immunity, resistance };

            var context = new ModifierContext();
            context.Tags.Add(DamageTypes.ToTag(DamageTypes.Fire));

            var applicableModifiers = modifiers
                .FindAll(m => m.Condition == null || m.Condition(context));

            // Act
            var result = DamagePipeline.Calculate(baseDamage, applicableModifiers, 0, 100);

            // Assert
            // Immunity applies first (priority 50): 20 * 0.0 = 0
            // Resistance applies second (priority 100): 0 * 0.5 = 0
            // Result should be 0
            Assert.Equal(0, result.FinalDamage); // Immunity should result in 0 damage even with resistance
        }

        [Fact]
        public void ImmunityOverridesVulnerability_WhenBothApply()
        {
            // Arrange: Apply both immunity and vulnerability to fire
            int baseDamage = 20;
            var immunity = DamageResistance.CreateImmunity(DamageTypes.Fire, "Elemental");
            var vulnerability = DamageResistance.CreateVulnerability(DamageTypes.Fire, "Oil");
            var modifiers = new List<Modifier> { immunity, vulnerability };

            var context = new ModifierContext();
            context.Tags.Add(DamageTypes.ToTag(DamageTypes.Fire));

            var applicableModifiers = modifiers
                .FindAll(m => m.Condition == null || m.Condition(context));

            // Act
            var result = DamagePipeline.Calculate(baseDamage, applicableModifiers, 0, 100);

            // Assert
            Assert.Equal(0, result.FinalDamage); // Immunity should result in 0 damage even with vulnerability
        }

        [Fact]
        public void ResistanceAndVulnerability_CancelOut()
        {
            // Arrange: Apply both resistance and vulnerability to same type
            // 5e/BG3 policy: they cancel to normal damage
            // Implementation: they stack multiplicatively: 0.5 * 2.0 = 1.0
            int baseDamage = 20;
            var resistance = DamageResistance.CreateResistance(DamageTypes.Fire, "Ring");
            var vulnerability = DamageResistance.CreateVulnerability(DamageTypes.Fire, "Oil");
            var modifiers = new List<Modifier> { resistance, vulnerability };

            var context = new ModifierContext();
            context.Tags.Add(DamageTypes.ToTag(DamageTypes.Fire));

            var applicableModifiers = modifiers
                .FindAll(m => m.Condition == null || m.Condition(context));

            // Act
            var result = DamagePipeline.Calculate(baseDamage, applicableModifiers, 0, 100);

            // Assert
            // Both have priority 100, so order depends on list order
            // With multiplicative stacking: 20 * 0.5 * 2.0 = 20 (or 20 * 2.0 * 0.5 = 20)
            Assert.Equal(20, result.FinalDamage); // Resistance + vulnerability should cancel to normal damage
        }

        [Fact]
        public void MultipleResistances_DoNotStack()
        {
            // Arrange: Apply two sources of fire resistance
            // BG3/5e rule: multiple resistances to the same type DON'T stack.
            // Only the strongest resistance applies (both are -50%, so just one counts).
            int baseDamage = 20;
            var resist1 = DamageResistance.CreateResistance(DamageTypes.Fire, "Ring");
            var resist2 = DamageResistance.CreateResistance(DamageTypes.Fire, "Spell");
            var modifiers = new List<Modifier> { resist1, resist2 };

            var context = new ModifierContext();
            context.Tags.Add(DamageTypes.ToTag(DamageTypes.Fire));

            var applicableModifiers = modifiers
                .FindAll(m => m.Condition == null || m.Condition(context));

            // Act
            var result = DamagePipeline.Calculate(baseDamage, applicableModifiers, 0, 100);

            // Assert — BG3/5e correct: only one resistance applies → 20 * 0.5 = 10
            Assert.Equal(10, result.FinalDamage);
        }

        [Theory]
        [InlineData(DamageType.Fire)]
        [InlineData(DamageType.Cold)]
        [InlineData(DamageType.Lightning)]
        [InlineData(DamageType.Thunder)]
        [InlineData(DamageType.Poison)]
        [InlineData(DamageType.Acid)]
        [InlineData(DamageType.Necrotic)]
        [InlineData(DamageType.Radiant)]
        [InlineData(DamageType.Force)]
        [InlineData(DamageType.Psychic)]
        [InlineData(DamageType.Slashing)]
        [InlineData(DamageType.Piercing)]
        [InlineData(DamageType.Bludgeoning)]
        public void Resistance_HalvesDamage_AllTypes(DamageType dt)
        {
            int baseDamage = 20;
            string dtStr = dt.ToString().ToLowerInvariant();
            var resistance = DamageResistance.CreateResistance(dtStr, "Test Resistance");
            var modifiers = new List<Modifier> { resistance };

            var context = new ModifierContext();
            context.Tags.Add(DamageTypes.ToTag(dtStr));

            var applicableModifiers = modifiers
                .FindAll(m => m.Condition == null || m.Condition(context));

            var result = DamagePipeline.Calculate(baseDamage, applicableModifiers, 0, 100);

            Assert.Equal(10, result.FinalDamage); // Resistance halves 20 -> 10
        }

        [Theory]
        [InlineData(DamageType.Fire)]
        [InlineData(DamageType.Cold)]
        [InlineData(DamageType.Lightning)]
        [InlineData(DamageType.Thunder)]
        [InlineData(DamageType.Poison)]
        [InlineData(DamageType.Acid)]
        [InlineData(DamageType.Necrotic)]
        [InlineData(DamageType.Radiant)]
        [InlineData(DamageType.Force)]
        [InlineData(DamageType.Psychic)]
        [InlineData(DamageType.Slashing)]
        [InlineData(DamageType.Piercing)]
        [InlineData(DamageType.Bludgeoning)]
        public void Immunity_ZeroesDamage_AllTypes(DamageType dt)
        {
            int baseDamage = 20;
            string dtStr = dt.ToString().ToLowerInvariant();
            var immunity = DamageResistance.CreateImmunity(dtStr, "Test Immunity");
            var modifiers = new List<Modifier> { immunity };

            var context = new ModifierContext();
            context.Tags.Add(DamageTypes.ToTag(dtStr));

            var applicableModifiers = modifiers
                .FindAll(m => m.Condition == null || m.Condition(context));

            var result = DamagePipeline.Calculate(baseDamage, applicableModifiers, 0, 100);

            Assert.Equal(0, result.FinalDamage); // Immunity zeroes 20 -> 0
        }

        [Theory]
        [InlineData(DamageType.Fire)]
        [InlineData(DamageType.Cold)]
        [InlineData(DamageType.Lightning)]
        [InlineData(DamageType.Thunder)]
        [InlineData(DamageType.Poison)]
        [InlineData(DamageType.Acid)]
        [InlineData(DamageType.Necrotic)]
        [InlineData(DamageType.Radiant)]
        [InlineData(DamageType.Force)]
        [InlineData(DamageType.Psychic)]
        [InlineData(DamageType.Slashing)]
        [InlineData(DamageType.Piercing)]
        [InlineData(DamageType.Bludgeoning)]
        public void Vulnerability_DoublesDamage_AllTypes(DamageType dt)
        {
            int baseDamage = 20;
            string dtStr = dt.ToString().ToLowerInvariant();
            var vulnerability = DamageResistance.CreateVulnerability(dtStr, "Test Vulnerability");
            var modifiers = new List<Modifier> { vulnerability };

            var context = new ModifierContext();
            context.Tags.Add(DamageTypes.ToTag(dtStr));

            var applicableModifiers = modifiers
                .FindAll(m => m.Condition == null || m.Condition(context));

            var result = DamagePipeline.Calculate(baseDamage, applicableModifiers, 0, 100);

            Assert.Equal(40, result.FinalDamage); // Vulnerability doubles 20 -> 40
        }
    }
}
