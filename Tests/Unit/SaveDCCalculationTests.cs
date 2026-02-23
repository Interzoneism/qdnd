using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Data.CharacterModel;
using QDND.Tests.Helpers;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Phase 10 parity regression: pin the save-DC calculation formulas.
    ///
    /// The canonical formula is:
    ///   Spell DC  = 8 + proficiency + spellcasting ability modifier (class-dependent)
    ///   Weapon DC = 8 + proficiency + max(STR mod, DEX mod)
    ///   Explicit  = action.SaveDC value (overrides all computation)
    ///
    /// Tests go through EffectPipeline.GetSaveDC (public) for stat-derived cases,
    /// and through reflection on ActionBarService.ComputeTooltipSaveDC for the
    /// explicit-override case (that branch lives in the tooltip path).
    /// </summary>
    public class SaveDCCalculationTests
    {
        // -------------------------------------------------------------------
        //  Helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Builds an EffectPipeline with no external dependencies.
        /// GetSaveDC / ComputeSaveDC do not invoke Godot APIs so this is
        /// safe inside dotnet test.
        /// </summary>
        private static EffectPipeline CreatePipeline() => new EffectPipeline();

        /// <summary>
        /// Creates a spell action with the given tags so ComputeSaveDC takes
        /// the spellcasting branch.
        /// </summary>
        private static ActionDefinition SpellAction(params string[] extraTags)
        {
            var tags = new HashSet<string>(extraTags) { "spell" };
            return new ActionDefinition
            {
                Id = "test_spell",
                Name = "Test Spell",
                Tags = tags
            };
        }

        /// <summary>
        /// Creates a weapon action (Melee) so ComputeSaveDC takes the weapon branch.
        /// </summary>
        private static ActionDefinition MeleeWeaponAction() => new ActionDefinition
        {
            Id = "test_weapon",
            Name = "Test Weapon",
            AttackType = AttackType.MeleeWeapon
        };

        /// <summary>
        /// Sets the ClassLevels on a combatant's sheet so the spellcasting-ability
        /// fallback switch picks the right ability.
        /// </summary>
        private static void SetClass(Combatant combatant, string classId, int count)
        {
            combatant.ResolvedCharacter!.Sheet.ClassLevels =
                Enumerable.Range(0, count)
                    .Select(_ => new ClassLevel { ClassId = classId })
                    .ToList();
        }

        // -------------------------------------------------------------------
        //  Tests
        // -------------------------------------------------------------------

        /// <summary>
        /// Wizard 5 (prof +3), INT 18 (+4): spell DC = 8 + 3 + 4 = 15.
        /// </summary>
        [Fact]
        public void SpellSaveDC_Wizard5_Int18_Returns15()
        {
            var pipeline = CreatePipeline();
            var wizard = TestHelpers.MakeCombatant(id: "wiz", @int: 18, profBonus: 3);
            SetClass(wizard, "wizard", 5);

            int dc = pipeline.GetSaveDC(wizard, SpellAction());

            Assert.Equal(15, dc);
        }

        /// <summary>
        /// Cleric 1 (prof +2), WIS 14 (+2): spell DC = 8 + 2 + 2 = 12.
        /// </summary>
        [Fact]
        public void SpellSaveDC_Cleric1_Wis14_Returns12()
        {
            var pipeline = CreatePipeline();
            var cleric = TestHelpers.MakeCombatant(id: "clr", wis: 14, profBonus: 2);
            SetClass(cleric, "cleric", 1);

            int dc = pipeline.GetSaveDC(cleric, SpellAction());

            Assert.Equal(12, dc);
        }

        /// <summary>
        /// Weapon save DC uses max(STR mod, DEX mod).
        /// STR 16 (+3) > DEX 10 (+0): DC = 8 + 2 + 3 = 13.
        /// </summary>
        [Fact]
        public void WeaponSaveDC_UsesMaxStrDex()
        {
            var pipeline = CreatePipeline();
            // str=16 → mod +3, dex=10 → mod 0
            var fighter = TestHelpers.MakeCombatant(id: "ftr", str: 16, dex: 10, profBonus: 2);
            var action = MeleeWeaponAction();

            int dc = pipeline.GetSaveDC(fighter, action);

            // Weapon branch: 8 + Prof(2) + max(StrMod(3), DexMod(0)) = 13
            Assert.Equal(13, dc);
        }

        /// <summary>
        /// When ActionDefinition.SaveDC is explicitly set, ActionBarService uses it
        /// directly (overrides any stat-based computation).
        ///
        /// ActionBarService.ComputeTooltipSaveDC is private; reflection is used here
        /// because no public entry point exposes this branch in isolation.
        /// </summary>
        [Fact]
        public void ExplicitSaveDCOverride_ReturnsFixedValue()
        {
            // ActionBarService is internal; use Type.GetType to locate it without a
            // compile-time reference, then reflect on the private static helper.
            var serviceType = Type.GetType(
                "QDND.Combat.Services.ActionBarService, QDND");
            Assert.NotNull(serviceType); // assembly name check

            var method = serviceType!.GetMethod(
                "ComputeTooltipSaveDC",
                BindingFlags.NonPublic | BindingFlags.Static);

            // Guard: if the method is ever renamed this test fails loudly
            Assert.NotNull(method);

            var action = new ActionDefinition
            {
                Id = "override_action",
                SaveDC = 17,
                SaveType = "Dexterity"
            };

            int dc = (int)method!.Invoke(null, new object[] { action, 3 /* profBonus */ });

            Assert.Equal(17, dc);
        }
    }
}
