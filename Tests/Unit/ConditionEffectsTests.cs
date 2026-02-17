using System.Collections.Generic;
using System.Linq;
using Xunit;
using QDND.Combat.Statuses;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for the ConditionEffects system — D&D 5e condition mechanics.
    /// ConditionEffects is pure C# with no Godot dependencies, so it can be tested directly.
    /// </summary>
    public class ConditionEffectsTests
    {
        #region Condition Identification

        [Theory]
        [InlineData("blinded", true)]
        [InlineData("charmed", true)]
        [InlineData("deafened", true)]
        [InlineData("frightened", true)]
        [InlineData("grappled", true)]
        [InlineData("incapacitated", true)]
        [InlineData("invisible", true)]
        [InlineData("paralyzed", true)]
        [InlineData("petrified", true)]
        [InlineData("poisoned", true)]
        [InlineData("prone", true)]
        [InlineData("restrained", true)]
        [InlineData("stunned", true)]
        [InlineData("unconscious", true)]
        [InlineData("burning", false)]
        [InlineData("blessed_bg3", false)]
        [InlineData("hasted", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsCondition_ReturnsCorrectly(string statusId, bool expected)
        {
            Assert.Equal(expected, ConditionEffects.IsCondition(statusId));
        }

        [Theory]
        [InlineData("blinded", ConditionType.Blinded)]
        [InlineData("BLINDED", ConditionType.Blinded)] // Case-insensitive
        [InlineData("Blinded", ConditionType.Blinded)]
        [InlineData("asleep", ConditionType.Unconscious)]
        [InlineData("downed", ConditionType.Unconscious)]
        [InlineData("webbed", ConditionType.Restrained)]
        [InlineData("ensnared", ConditionType.Restrained)]
        [InlineData("hypnotised", ConditionType.Incapacitated)]
        [InlineData("crown_of_madness", ConditionType.Charmed)]
        [InlineData("darkness_obscured", ConditionType.Blinded)]
        [InlineData("greater_invisible", ConditionType.Invisible)]
        public void GetConditionType_MapsCorrectly(string statusId, ConditionType expected)
        {
            var result = ConditionEffects.GetConditionType(statusId);
            Assert.NotNull(result);
            Assert.Equal(expected, result.Value);
        }

        [Fact]
        public void GetConditionType_UnknownStatus_ReturnsNull()
        {
            Assert.Null(ConditionEffects.GetConditionType("burning"));
            Assert.Null(ConditionEffects.GetConditionType("hasted"));
            Assert.Null(ConditionEffects.GetConditionType(null));
            Assert.Null(ConditionEffects.GetConditionType(""));
        }

        #endregion

        #region Blinded

        [Fact]
        public void Blinded_AttackersHaveAdvantage()
        {
            Assert.True(ConditionEffects.ShouldAttackerHaveAdvantage("blinded", isMeleeAttack: true));
            Assert.True(ConditionEffects.ShouldAttackerHaveAdvantage("blinded", isMeleeAttack: false));
        }

        [Fact]
        public void Blinded_HasDisadvantageOnOwnAttacks()
        {
            Assert.True(ConditionEffects.HasDisadvantageOnOwnAttacks("blinded"));
        }

        #endregion

        #region Frightened

        [Fact]
        public void Frightened_HasDisadvantageOnAttacks()
        {
            Assert.True(ConditionEffects.HasDisadvantageOnOwnAttacks("frightened"));
        }

        [Fact]
        public void Frightened_HasDisadvantageOnAbilityChecks()
        {
            var mechanics = ConditionEffects.GetConditionMechanics("frightened");
            Assert.NotNull(mechanics);
            Assert.True(mechanics.HasDisadvantageOnAbilityChecks);
        }

        #endregion

        #region Invisible

        [Fact]
        public void Invisible_HasAdvantageOnOwnAttacks()
        {
            Assert.True(ConditionEffects.HasAdvantageOnOwnAttacks("invisible"));
            Assert.True(ConditionEffects.HasAdvantageOnOwnAttacks("greater_invisible"));
        }

        [Fact]
        public void Invisible_AttackersHaveDisadvantage()
        {
            Assert.True(ConditionEffects.ShouldAttackerHaveDisadvantage("invisible", isMeleeAttack: true));
            Assert.True(ConditionEffects.ShouldAttackerHaveDisadvantage("invisible", isMeleeAttack: false));
        }

        #endregion

        #region Paralyzed

        [Fact]
        public void Paralyzed_IsIncapacitating()
        {
            Assert.True(ConditionEffects.IsIncapacitating("paralyzed"));
        }

        [Fact]
        public void Paralyzed_AutoFailsStrDexSaves()
        {
            Assert.True(ConditionEffects.ShouldAutoFailSave("paralyzed", "STR"));
            Assert.True(ConditionEffects.ShouldAutoFailSave("paralyzed", "DEX"));
            Assert.True(ConditionEffects.ShouldAutoFailSave("paralyzed", "Strength"));
            Assert.True(ConditionEffects.ShouldAutoFailSave("paralyzed", "Dexterity"));
            Assert.False(ConditionEffects.ShouldAutoFailSave("paralyzed", "CON"));
            Assert.False(ConditionEffects.ShouldAutoFailSave("paralyzed", "WIS"));
        }

        [Fact]
        public void Paralyzed_MeleeAutocrits()
        {
            Assert.True(ConditionEffects.ShouldMeleeAutoCrit("paralyzed"));
        }

        [Fact]
        public void Paralyzed_AttackersHaveAdvantage()
        {
            Assert.True(ConditionEffects.ShouldAttackerHaveAdvantage("paralyzed", isMeleeAttack: true));
            Assert.True(ConditionEffects.ShouldAttackerHaveAdvantage("paralyzed", isMeleeAttack: false));
        }

        [Fact]
        public void Paralyzed_PreventsMovement()
        {
            Assert.True(ConditionEffects.PreventsMovement("paralyzed"));
        }

        #endregion

        #region Petrified

        [Fact]
        public void Petrified_HasAllMechanics()
        {
            var mechanics = ConditionEffects.GetConditionMechanics("petrified");
            Assert.NotNull(mechanics);
            Assert.True(mechanics.IsIncapacitated);
            Assert.True(mechanics.CantMove);
            Assert.True(mechanics.CantSpeak);
            Assert.True(mechanics.AutoFailStrDexSaves);
            Assert.True(mechanics.GrantsAdvantageToAttackers);
            Assert.True(mechanics.HasResistanceToAllDamage);
        }

        [Fact]
        public void Petrified_HasResistanceToAllDamage()
        {
            Assert.True(ConditionEffects.HasResistanceToAllDamage("petrified"));
            Assert.False(ConditionEffects.HasResistanceToAllDamage("paralyzed"));
        }

        #endregion

        #region Poisoned

        [Fact]
        public void Poisoned_HasDisadvantageOnAttacks()
        {
            Assert.True(ConditionEffects.HasDisadvantageOnOwnAttacks("poisoned"));
        }

        [Fact]
        public void Poisoned_HasDisadvantageOnAbilityChecks()
        {
            var mechanics = ConditionEffects.GetConditionMechanics("poisoned");
            Assert.NotNull(mechanics);
            Assert.True(mechanics.HasDisadvantageOnAbilityChecks);
        }

        #endregion

        #region Prone

        [Fact]
        public void Prone_MeleeAttackerHasAdvantage()
        {
            Assert.True(ConditionEffects.ShouldAttackerHaveAdvantage("prone", isMeleeAttack: true));
        }

        [Fact]
        public void Prone_RangedAttackerHasDisadvantage()
        {
            Assert.False(ConditionEffects.ShouldAttackerHaveAdvantage("prone", isMeleeAttack: false));
            Assert.True(ConditionEffects.ShouldAttackerHaveDisadvantage("prone", isMeleeAttack: false));
        }

        [Fact]
        public void Prone_ProneCreatureHasDisadvantageOnAttacks()
        {
            Assert.True(ConditionEffects.HasDisadvantageOnOwnAttacks("prone"));
        }

        [Fact]
        public void Prone_MeleeAttackDoesNotGetDisadvantage()
        {
            // When the TARGET is prone and the ATTACKER is melee, attacker does NOT get disadvantage
            Assert.False(ConditionEffects.ShouldAttackerHaveDisadvantage("prone", isMeleeAttack: true));
        }

        #endregion

        #region Restrained

        [Fact]
        public void Restrained_HasDisadvantageOnAttacks()
        {
            Assert.True(ConditionEffects.HasDisadvantageOnOwnAttacks("restrained"));
        }

        [Fact]
        public void Restrained_AttackersHaveAdvantage()
        {
            Assert.True(ConditionEffects.ShouldAttackerHaveAdvantage("restrained", isMeleeAttack: true));
        }

        [Fact]
        public void Restrained_HasDisadvantageOnDexSaves()
        {
            Assert.True(ConditionEffects.HasDisadvantageOnDexSaves("restrained"));
        }

        [Fact]
        public void Restrained_PreventsMovement()
        {
            Assert.True(ConditionEffects.PreventsMovement("restrained"));
        }

        [Fact]
        public void Restrained_MapsWebbed()
        {
            // webbed maps to Restrained condition
            Assert.True(ConditionEffects.ShouldAttackerHaveAdvantage("webbed"));
            Assert.True(ConditionEffects.HasDisadvantageOnOwnAttacks("webbed"));
            Assert.True(ConditionEffects.PreventsMovement("webbed"));
        }

        #endregion

        #region Stunned

        [Fact]
        public void Stunned_IsIncapacitating()
        {
            Assert.True(ConditionEffects.IsIncapacitating("stunned"));
        }

        [Fact]
        public void Stunned_AutoFailsStrDexSaves()
        {
            Assert.True(ConditionEffects.ShouldAutoFailSave("stunned", "STR"));
            Assert.True(ConditionEffects.ShouldAutoFailSave("stunned", "DEX"));
        }

        [Fact]
        public void Stunned_AttackersHaveAdvantage()
        {
            Assert.True(ConditionEffects.ShouldAttackerHaveAdvantage("stunned"));
        }

        #endregion

        #region Unconscious

        [Fact]
        public void Unconscious_MeleeAutocrits()
        {
            Assert.True(ConditionEffects.ShouldMeleeAutoCrit("unconscious"));
            Assert.True(ConditionEffects.ShouldMeleeAutoCrit("asleep"));
            Assert.True(ConditionEffects.ShouldMeleeAutoCrit("downed"));
        }

        [Fact]
        public void Unconscious_AutoFailsStrDexSaves()
        {
            Assert.True(ConditionEffects.ShouldAutoFailSave("unconscious", "STR"));
            Assert.True(ConditionEffects.ShouldAutoFailSave("asleep", "DEX"));
        }

        [Fact]
        public void Unconscious_IsFullyIncapacitated()
        {
            var mechanics = ConditionEffects.GetConditionMechanics("unconscious");
            Assert.NotNull(mechanics);
            Assert.True(mechanics.IsIncapacitated);
            Assert.True(mechanics.CantMove);
            Assert.True(mechanics.CantSpeak);
            Assert.True(mechanics.GrantsAdvantageToAttackers);
            Assert.True(mechanics.MeleeAutocrits);
            Assert.True(mechanics.AutoFailStrDexSaves);
        }

        #endregion

        #region Grappled

        [Fact]
        public void Grappled_PreventsMovement()
        {
            Assert.True(ConditionEffects.PreventsMovement("grappled"));
        }

        [Fact]
        public void Grappled_DoesNotGrantAdvantage()
        {
            Assert.False(ConditionEffects.ShouldAttackerHaveAdvantage("grappled"));
        }

        #endregion

        #region Charmed

        [Fact]
        public void Charmed_NoAttackModifiers()
        {
            Assert.False(ConditionEffects.HasDisadvantageOnOwnAttacks("charmed"));
            Assert.False(ConditionEffects.ShouldAttackerHaveAdvantage("charmed"));
        }

        [Fact]
        public void Charmed_MapsCommandedAndCrownOfMadness()
        {
            Assert.Equal(ConditionType.Charmed, ConditionEffects.GetConditionType("commanded"));
            Assert.Equal(ConditionType.Charmed, ConditionEffects.GetConditionType("crown_of_madness"));
        }

        #endregion

        #region Aggregate Effects

        [Fact]
        public void GetAggregateEffects_EmptyList_NoEffects()
        {
            var effects = ConditionEffects.GetAggregateEffects(new List<string>());
            Assert.False(effects.HasAnyCondition);
            Assert.Empty(effects.AttackAdvantageSources);
            Assert.Empty(effects.AttackDisadvantageSources);
            Assert.Empty(effects.DefenseAdvantageSources);
            Assert.Empty(effects.DefenseDisadvantageSources);
        }

        [Fact]
        public void GetAggregateEffects_MultipleConditions_Combines()
        {
            var statuses = new List<string> { "paralyzed", "poisoned" };
            var effects = ConditionEffects.GetAggregateEffects(statuses, isMeleeAttack: true);

            Assert.True(effects.HasAnyCondition);
            Assert.Contains(ConditionType.Paralyzed, effects.ActiveConditions);
            Assert.Contains(ConditionType.Poisoned, effects.ActiveConditions);

            // Both give attackers advantage
            Assert.NotEmpty(effects.DefenseAdvantageSources);
            // Both give disadvantage on own attacks
            Assert.NotEmpty(effects.AttackDisadvantageSources);
            // Paralyzed causes auto-fail STR/DEX
            Assert.True(effects.AutoFailStrDexSaves);
            // Paralyzed causes melee autocrits
            Assert.True(effects.MeleeAutocrits);
            // Paralyzed is incapacitating
            Assert.True(effects.IsIncapacitated);
        }

        [Fact]
        public void GetAggregateEffects_ProneWithMelee_GrantsAdvantageToAttackers()
        {
            var statuses = new List<string> { "prone" };
            var effects = ConditionEffects.GetAggregateEffects(statuses, isMeleeAttack: true);

            Assert.NotEmpty(effects.DefenseAdvantageSources);
            Assert.Empty(effects.DefenseDisadvantageSources);
        }

        [Fact]
        public void GetAggregateEffects_ProneWithRanged_GrantsDisadvantageToAttackers()
        {
            var statuses = new List<string> { "prone" };
            var effects = ConditionEffects.GetAggregateEffects(statuses, isMeleeAttack: false);

            Assert.Empty(effects.DefenseAdvantageSources);
            Assert.NotEmpty(effects.DefenseDisadvantageSources);
        }

        [Fact]
        public void GetAggregateEffects_InvisibleAttackerGetsAdvantage()
        {
            var statuses = new List<string> { "invisible" };
            var effects = ConditionEffects.GetAggregateEffects(statuses, isMeleeAttack: true);

            Assert.NotEmpty(effects.AttackAdvantageSources);
            Assert.Empty(effects.AttackDisadvantageSources);
        }

        [Fact]
        public void GetAggregateEffects_MixedConditionsAndNonConditions()
        {
            // Non-condition statuses should be ignored
            var statuses = new List<string> { "blinded", "burning", "hasted", "stunned" };
            var effects = ConditionEffects.GetAggregateEffects(statuses, isMeleeAttack: true);

            Assert.Equal(2, effects.ActiveConditions.Count); // Only blinded and stunned
            Assert.Contains(ConditionType.Blinded, effects.ActiveConditions);
            Assert.Contains(ConditionType.Stunned, effects.ActiveConditions);
        }

        [Fact]
        public void GetAggregateEffects_RestrainedDisadvantageOnDexSaves()
        {
            var statuses = new List<string> { "restrained" };
            var effects = ConditionEffects.GetAggregateEffects(statuses);

            Assert.True(effects.HasDisadvantageOnDexSaves);
            Assert.False(effects.AutoFailStrDexSaves);
        }

        #endregion

        #region Status ID Lookups

        [Fact]
        public void GetAllConditionStatusIds_ReturnsAllMappings()
        {
            var ids = ConditionEffects.GetAllConditionStatusIds();
            Assert.NotEmpty(ids);
            Assert.Contains("blinded", ids);
            Assert.Contains("paralyzed", ids);
            Assert.Contains("asleep", ids);
            Assert.Contains("webbed", ids);
        }

        [Fact]
        public void GetStatusIdsForCondition_ReturnsAllMappedIds()
        {
            var unconsciousIds = ConditionEffects.GetStatusIdsForCondition(ConditionType.Unconscious);
            Assert.Contains("asleep", unconsciousIds);
            Assert.Contains("downed", unconsciousIds);
            Assert.Contains("unconscious", unconsciousIds);

            var restrainedIds = ConditionEffects.GetStatusIdsForCondition(ConditionType.Restrained);
            Assert.Contains("restrained", restrainedIds);
            Assert.Contains("webbed", restrainedIds);
            Assert.Contains("ensnared", restrainedIds);
        }

        #endregion

        #region Non-Condition Edge Cases

        [Fact]
        public void NonCondition_ShouldNotAutoFail()
        {
            Assert.False(ConditionEffects.ShouldAutoFailSave("burning", "STR"));
            Assert.False(ConditionEffects.ShouldAutoFailSave("hasted", "DEX"));
        }

        [Fact]
        public void NonCondition_ShouldNotAutoCrit()
        {
            Assert.False(ConditionEffects.ShouldMeleeAutoCrit("burning"));
            Assert.False(ConditionEffects.ShouldMeleeAutoCrit("stunned")); // stunned does NOT autocrit (only paralyzed/unconscious)
        }

        [Fact]
        public void NonCondition_ShouldNotPreventMovement()
        {
            Assert.False(ConditionEffects.PreventsMovement("poisoned"));
            Assert.False(ConditionEffects.PreventsMovement("blinded"));
        }

        #endregion
    }
}
