using System.Collections.Generic;
using Xunit;
using QDND.Data.CharacterModel;
using QDND.Tests.Helpers;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Phase 10 parity regression: golden tests for the stat authority path.
    ///
    /// These tests pin the BG3/D&D modifier formula (Math.Floor((score - 10) / 2.0))
    /// and verify that CharacterSheet, Combatant, and ResolvedCharacter all derive
    /// modifiers from the same formula. The score-9 case (should be -1, not 0) is
    /// specifically included to catch an integer-division regression.
    /// </summary>
    public class StatAuthorityGoldenTests
    {
        // -------------------------------------------------------------------
        //  AbilityModifier formula — CharacterSheet.GetModifier
        // -------------------------------------------------------------------

        [Theory]
        [InlineData(10, 0)]   // baseline
        [InlineData(15, 2)]   // odd score rounds down
        [InlineData(16, 3)]   // even score
        [InlineData(8, -1)]   // below 10
        [InlineData(1, -5)]   // minimum — tests floor on large negative
        [InlineData(9, -1)]   // floor(-0.5) = -1, integer division would give 0 (bug check)
        public void AbilityModifier_MatchesFloorFormula(int score, int expected)
        {
            Assert.Equal(expected, CharacterSheet.GetModifier(score));
        }

        // -------------------------------------------------------------------
        //  Combatant modifier via ResolvedCharacter.AbilityScores
        // -------------------------------------------------------------------

        [Fact]
        public void MakeCombatant_Str16_ReturnsModPlusThree()
        {
            var combatant = TestHelpers.MakeCombatant(str: 16);

            Assert.Equal(3, combatant.GetAbilityModifier(AbilityType.Strength));
        }

        // -------------------------------------------------------------------
        //  Proficiency bonus from CharacterSheet total level
        // -------------------------------------------------------------------

        [Fact]
        public void ProfBonus_Level1_IsTwo()
        {
            // Any total level ≤ 4 should yield proficiency +2
            var sheet = new CharacterSheet
            {
                ClassLevels = new List<ClassLevel>
                {
                    new ClassLevel { ClassId = "fighter" }
                }
            };

            Assert.Equal(2, sheet.ProficiencyBonus);
        }
    }
}
