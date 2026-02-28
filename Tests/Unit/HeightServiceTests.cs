using Godot;
using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Environment;

namespace QDND.Tests.Unit
{
    public class HeightServiceTests
    {
        private HeightService CreateService()
        {
            return new HeightService();
        }

        private Combatant CreateCombatant(string id, float height)
        {
            var c = new Combatant(id, id, Faction.Neutral, 100, 10);
            c.Position = new Vector3(0, height, 0);
            return c;
        }

        [Fact]
        public void GetHeightAdvantage_Higher_ReturnsHigher()
        {
            var service = CreateService();
            var attacker = CreateCombatant("attacker", 10);
            var target = CreateCombatant("target", 0);

            var result = service.GetHeightAdvantage(attacker, target);

            Assert.Equal(HeightAdvantage.Higher, result);
        }

        [Fact]
        public void GetHeightAdvantage_Lower_ReturnsLower()
        {
            var service = CreateService();
            var attacker = CreateCombatant("attacker", 0);
            var target = CreateCombatant("target", 10);

            var result = service.GetHeightAdvantage(attacker, target);

            Assert.Equal(HeightAdvantage.Lower, result);
        }

        [Fact]
        public void GetHeightAdvantage_Level_ReturnsLevel()
        {
            var service = CreateService();
            var attacker = CreateCombatant("attacker", 5);
            var target = CreateCombatant("target", 5);

            var result = service.GetHeightAdvantage(attacker, target);

            Assert.Equal(HeightAdvantage.Level, result);
        }

        [Fact]
        public void GetAttackModifier_Higher_ReturnsPositive()
        {
            var service = CreateService();
            var attacker = CreateCombatant("attacker", 10);
            var target = CreateCombatant("target", 0);

            int modifier = service.GetAttackModifier(attacker, target);

            Assert.Equal(2, modifier);
        }

        [Fact]
        public void GetAttackModifier_Lower_ReturnsNegative()
        {
            var service = CreateService();
            var attacker = CreateCombatant("attacker", 0);
            var target = CreateCombatant("target", 10);

            int modifier = service.GetAttackModifier(attacker, target);

            Assert.Equal(-2, modifier);
        }

        [Fact]
        public void CalculateFallDamage_SafeFall_NoDamage()
        {
            var service = new HeightService { SafeFallDistance = 10 };

            var result = service.CalculateFallDamage(5);

            Assert.Equal(0, result.Damage);
            Assert.False(result.IsProne);
        }

        [Fact]
        public void CalculateFallDamage_DamagingFall_HasDamage()
        {
            var service = new HeightService { SafeFallDistance = 10, DamagePerUnit = 1 };

            var result = service.CalculateFallDamage(30);

            Assert.True(result.Damage > 0);
        }

        [Fact]
        public void CalculateFallDamage_LethalFall_IsLethal()
        {
            var service = new HeightService { LethalFallDistance = 200 };

            var result = service.CalculateFallDamage(250);

            Assert.True(result.IsLethal);
        }

        [Fact]
        public void ApplyFallDamage_DealsToHP()
        {
            var service = new HeightService { SafeFallDistance = 0, DamagePerUnit = 1 };
            var combatant = CreateCombatant("test", 0);

            var result = service.ApplyFallDamage(combatant, 30);

            Assert.True(combatant.Resources.CurrentHP < 100);
        }

        [Fact]
        public void GetDamageModifier_HigherRanged_NoDamageBonus()
        {
            var service = CreateService();
            var attacker = CreateCombatant("attacker", 10);
            var target = CreateCombatant("target", 0);

            float modifier = service.GetDamageModifier(attacker, target, isRanged: true);

            Assert.Equal(1f, modifier);
        }

        [Fact]
        public void GetDamageModifier_LowerRanged_NoDamagePenalty()
        {
            var service = CreateService();
            var attacker = CreateCombatant("attacker", 0);
            var target = CreateCombatant("target", 10);

            float modifier = service.GetDamageModifier(attacker, target, isRanged: true);

            Assert.Equal(1f, modifier);
        }

        [Fact]
        public void GetDamageModifier_Melee_NoChange()
        {
            var service = CreateService();
            var attacker = CreateCombatant("attacker", 10);
            var target = CreateCombatant("target", 0);

            float modifier = service.GetDamageModifier(attacker, target, isRanged: false);

            Assert.Equal(1f, modifier);
        }

        [Fact]
        public void IsJumpSafe_ShortJump_True()
        {
            var service = new HeightService { SafeFallDistance = 10 };

            bool safe = service.IsJumpSafe(new Vector3(0, 5, 0), new Vector3(10, 2, 0));

            Assert.True(safe);
        }

        [Fact]
        public void IsJumpSafe_LongFall_False()
        {
            var service = new HeightService { SafeFallDistance = 10 };

            bool safe = service.IsJumpSafe(new Vector3(0, 50, 0), new Vector3(10, 0, 0));

            Assert.False(safe);
        }

        [Fact]
        public void HasHeightAdvantage_True()
        {
            var service = CreateService();
            var attacker = CreateCombatant("attacker", 10);
            var target = CreateCombatant("target", 0);

            Assert.True(service.HasHeightAdvantage(attacker, target));
        }

        [Fact]
        public void HasHeightDisadvantage_True()
        {
            var service = CreateService();
            var attacker = CreateCombatant("attacker", 0);
            var target = CreateCombatant("target", 10);

            Assert.True(service.HasHeightDisadvantage(attacker, target));
        }
    }
}
