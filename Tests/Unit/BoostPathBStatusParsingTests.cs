using QDND.Combat.Entities;
using QDND.Combat.Rules.Boosts;
using QDND.Data.CharacterModel;
using Xunit;

namespace QDND.Tests.Unit
{
    public class BoostPathBStatusParsingTests
    {
        [Fact]
        public void ParseBoostString_NewStatusBoostTypes_ParsesAllRequestedTypes()
        {
            var boosts = BoostParser.ParseBoostString(
                "Invulnerable();JumpMaxDistanceMultiplier(3);MaximizeHealing(Incoming);WeaponDamageResistance(Bludgeoning)");

            Assert.Equal(4, boosts.Count);
            Assert.Contains(boosts, b => b.Type == BoostType.Invulnerable);
            Assert.Contains(boosts, b => b.Type == BoostType.JumpMaxDistanceMultiplier);
            Assert.Contains(boosts, b => b.Type == BoostType.MaximizeHealing);
            Assert.Contains(boosts, b => b.Type == BoostType.WeaponDamageResistance);
        }

        [Fact]
        public void ParseBoostString_SpeedMultiplier_IsSkippedAsUnknown()
        {
            var boosts = BoostParser.ParseBoostString("SpeedMultiplier(1.5)");

            Assert.Empty(boosts);
        }

        [Fact]
        public void ApplyBoosts_TemporaryHP_GrantsImmediateTemporaryHp()
        {
            var combatant = CreateCombatant();

            BoostApplicator.ApplyBoosts(combatant, "TemporaryHP(7)", "Status", "TEST_TEMP_HP");

            Assert.Equal(7, combatant.Resources.TemporaryHP);
            Assert.Equal(7, BoostEvaluator.GetTemporaryHP(combatant));
        }

        [Fact]
        public void GetRerollRules_StatusRerollDamage_ParsesAndReturnsRule()
        {
            var combatant = CreateCombatant();

            BoostApplicator.ApplyBoosts(combatant, "Reroll(Damage,9,false)", "Status", "TEST_REROLL");
            var rules = BoostEvaluator.GetRerollRules(combatant);

            Assert.Single(rules);
            Assert.Equal("Damage", rules[0].RollType);
            Assert.Equal(9, rules[0].MinValue);
            Assert.False(rules[0].KeepHigher);
        }

        [Fact]
        public void GetResourceConsumeMultiplier_ActionResourceConsumeMultiplier_ReturnsExpectedMultiplier()
        {
            var combatant = CreateCombatant();

            BoostApplicator.ApplyBoosts(combatant, "ActionResourceConsumeMultiplier(Movement,1.5,0)", "Status", "TEST_SLOW");

            float multiplier = BoostEvaluator.GetResourceConsumeMultiplier(combatant, "Movement");
            Assert.Equal(1.5f, multiplier, 3);
        }

        [Fact]
        public void GetResourceConsumeMultiplier_SpeedMultiplierBoost_DoesNotApply()
        {
            var combatant = CreateCombatant();

            BoostApplicator.ApplyBoosts(combatant, "SpeedMultiplier(2)", "Status", "TEST_SPEED_ALIAS");

            float multiplier = BoostEvaluator.GetResourceConsumeMultiplier(combatant, "Movement");
            Assert.Equal(1f, multiplier, 3);
        }

        [Fact]
        public void IsInvulnerable_InvulnerableBoost_ReturnsTrue()
        {
            var combatant = CreateCombatant();

            BoostApplicator.ApplyBoosts(combatant, "Invulnerable()", "Status", "TEST_INVULN");

            Assert.True(BoostEvaluator.IsInvulnerable(combatant));
        }

        [Fact]
        public void IsInvulnerable_AttributeFallback_ReturnsTrue()
        {
            var combatant = CreateCombatant();

            BoostApplicator.ApplyBoosts(combatant, "Attribute(Invulnerable)", "Status", "TEST_INVULN_ATTR");

            Assert.True(BoostEvaluator.IsInvulnerable(combatant));
        }

        [Fact]
        public void GetJumpDistanceMultiplier_MultipleBoosts_MultipliesValues()
        {
            var combatant = CreateCombatant();

            BoostApplicator.ApplyBoosts(combatant, "JumpMaxDistanceMultiplier(1.5);JumpMaxDistanceMultiplier(2)", "Status", "TEST_JUMP");

            float multiplier = BoostEvaluator.GetJumpDistanceMultiplier(combatant);
            Assert.Equal(3f, multiplier, 3);
        }

        [Fact]
        public void IsHealingMaximized_IncomingBoost_ReturnsTrueForIncomingOnly()
        {
            var combatant = CreateCombatant();

            BoostApplicator.ApplyBoosts(combatant, "MaximizeHealing(Incoming)", "Status", "TEST_MAX_HEAL");

            Assert.True(BoostEvaluator.IsHealingMaximized(combatant, "Incoming"));
            Assert.False(BoostEvaluator.IsHealingMaximized(combatant, "Outgoing"));
        }

        [Fact]
        public void GetWeaponDamageResistanceLevel_MatchingDamageType_ReturnsResistant()
        {
            var combatant = CreateCombatant();

            BoostApplicator.ApplyBoosts(combatant, "WeaponDamageResistance(Bludgeoning)", "Status", "TEST_WEAPON_RES");

            Assert.Equal(ResistanceLevel.Resistant, BoostEvaluator.GetWeaponDamageResistanceLevel(combatant, DamageType.Bludgeoning));
            Assert.Equal(ResistanceLevel.Normal, BoostEvaluator.GetWeaponDamageResistanceLevel(combatant, DamageType.Fire));
        }

        private static Combatant CreateCombatant()
        {
            return new Combatant("test-combatant", "Test Combatant", Faction.Player, maxHP: 30, initiative: 10);
        }
    }
}
