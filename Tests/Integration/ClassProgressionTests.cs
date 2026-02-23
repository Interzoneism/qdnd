using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Verifies that all 12 BG3 classes have correct progression data at every level 1-12.
    /// Loads actual JSON class definitions and resolves characters through CharacterResolver.
    /// </summary>
    public class ClassProgressionTests
    {
        private readonly ITestOutputHelper _output;
        private readonly CharacterDataRegistry _registry;
        private readonly CharacterResolver _resolver;

        public ClassProgressionTests(ITestOutputHelper output)
        {
            _output = output;
            _registry = new CharacterDataRegistry();
            _registry.LoadFromDirectory(ResolveDataPath());
            _resolver = new CharacterResolver(_registry);
        }

        private static string ResolveDataPath()
        {
            var candidates = new[]
            {
                "Data",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Data"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "Data")
            };

            foreach (var path in candidates)
            {
                if (Directory.Exists(Path.Combine(path, "Classes")))
                    return path;
            }

            throw new DirectoryNotFoundException("Could not locate Data directory for ClassProgressionTests");
        }

        private ResolvedCharacter ResolveClass(string classId, int level, string subclassId = null)
        {
            var sheet = new CharacterSheet
            {
                Name = $"Test {classId} L{level}",
                ClassLevels = Enumerable.Range(0, level)
                    .Select(_ => new ClassLevel(classId, subclassId))
                    .ToList()
            };
            return _resolver.Resolve(sheet);
        }

        // ───────────────────────────────────────────
        // ALL 12 CLASSES LOAD AND RESOLVE AT ALL LEVELS
        // ───────────────────────────────────────────

        [Theory]
        [InlineData("fighter")]
        [InlineData("barbarian")]
        [InlineData("monk")]
        [InlineData("rogue")]
        [InlineData("cleric")]
        [InlineData("paladin")]
        [InlineData("druid")]
        [InlineData("ranger")]
        [InlineData("wizard")]
        [InlineData("sorcerer")]
        [InlineData("warlock")]
        [InlineData("bard")]
        public void AllLevels_ResolveWithoutError(string classId)
        {
            var classDef = _registry.GetClass(classId);
            Assert.NotNull(classDef);

            for (int level = 1; level <= 12; level++)
            {
                var resolved = ResolveClass(classId, level);
                Assert.NotNull(resolved);
                Assert.Equal(level, resolved.Sheet.TotalLevel);
                _output.WriteLine($"{classId} L{level}: HP={resolved.MaxHP}, ExtraAttacks={resolved.ExtraAttacks}, Resources=[{string.Join(", ", resolved.Resources.Select(r => $"{r.Key}={r.Value}"))}]");
            }
        }

        [Theory]
        [InlineData("fighter")]
        [InlineData("barbarian")]
        [InlineData("monk")]
        [InlineData("rogue")]
        [InlineData("cleric")]
        [InlineData("paladin")]
        [InlineData("druid")]
        [InlineData("ranger")]
        [InlineData("wizard")]
        [InlineData("sorcerer")]
        [InlineData("warlock")]
        [InlineData("bard")]
        public void AllLevels_HaveCompleteLevelTable(string classId)
        {
            var classDef = _registry.GetClass(classId);
            Assert.NotNull(classDef);

            for (int level = 1; level <= 12; level++)
            {
                Assert.True(
                    classDef.LevelTable.ContainsKey(level.ToString()),
                    $"{classId} missing LevelTable entry for level {level}");
            }
        }

        // ───────────────────────────────────────────
        // EXTRA ATTACKS
        // ───────────────────────────────────────────

        [Theory]
        [InlineData("fighter", 4, 0)]
        [InlineData("fighter", 5, 1)]
        [InlineData("fighter", 10, 1)]
        [InlineData("fighter", 11, 2)]
        [InlineData("fighter", 12, 2)]
        [InlineData("barbarian", 4, 0)]
        [InlineData("barbarian", 5, 1)]
        [InlineData("barbarian", 12, 1)]
        [InlineData("monk", 4, 0)]
        [InlineData("monk", 5, 1)]
        [InlineData("monk", 12, 1)]
        [InlineData("paladin", 4, 0)]
        [InlineData("paladin", 5, 1)]
        [InlineData("paladin", 12, 1)]
        [InlineData("ranger", 4, 0)]
        [InlineData("ranger", 5, 1)]
        [InlineData("ranger", 12, 1)]
        public void ExtraAttacks_CorrectAtLevel(string classId, int level, int expectedExtraAttacks)
        {
            var resolved = ResolveClass(classId, level);
            Assert.Equal(expectedExtraAttacks, resolved.ExtraAttacks);
        }

        [Theory]
        [InlineData("wizard")]
        [InlineData("sorcerer")]
        [InlineData("warlock")]
        [InlineData("bard")]
        [InlineData("cleric")]
        [InlineData("druid")]
        [InlineData("rogue")]
        public void NonMartialClasses_NoExtraAttacks(string classId)
        {
            var resolved = ResolveClass(classId, 12);
            Assert.Equal(0, resolved.ExtraAttacks);
        }

        // ───────────────────────────────────────────
        // WIZARD SPELL SLOT PROGRESSION
        // ───────────────────────────────────────────

        [Theory]
        [InlineData(1, 2, 0, 0, 0, 0, 0)]  // L1: 2 first-level slots
        [InlineData(2, 3, 0, 0, 0, 0, 0)]  // L2: 3 first-level
        [InlineData(3, 4, 2, 0, 0, 0, 0)]  // L3: 4/2
        [InlineData(5, 4, 3, 2, 0, 0, 0)]  // L5: 4/3/2
        [InlineData(7, 4, 3, 3, 1, 0, 0)]  // L7: 4/3/3/1
        [InlineData(9, 4, 3, 3, 3, 1, 0)]  // L9: 4/3/3/3/1
        [InlineData(11, 4, 3, 3, 3, 2, 1)] // L11: 4/3/3/3/2/1
        public void Wizard_SpellSlotProgression(int level, int s1, int s2, int s3, int s4, int s5, int s6)
        {
            var resolved = ResolveClass("wizard", level);
            AssertSpellSlots(resolved, s1, s2, s3, s4, s5, s6);
        }

        // ───────────────────────────────────────────
        // CLERIC SPELL SLOT PROGRESSION (same as wizard through 12)
        // ───────────────────────────────────────────

        [Theory]
        [InlineData(1, 2, 0, 0, 0, 0, 0)]
        [InlineData(3, 4, 2, 0, 0, 0, 0)]
        [InlineData(5, 4, 3, 2, 0, 0, 0)]
        [InlineData(9, 4, 3, 3, 3, 1, 0)]
        public void Cleric_SpellSlotProgression(int level, int s1, int s2, int s3, int s4, int s5, int s6)
        {
            var resolved = ResolveClass("cleric", level);
            AssertSpellSlots(resolved, s1, s2, s3, s4, s5, s6);
        }

        // ───────────────────────────────────────────
        // HALF-CASTER SPELL SLOT PROGRESSION (Paladin, Ranger)
        // ───────────────────────────────────────────

        [Theory]
        [InlineData("paladin", 1, 0, 0)]  // No spells at L1
        [InlineData("paladin", 2, 2, 0)]  // L2: 2 first-level
        [InlineData("paladin", 5, 4, 2)]  // L5: 4/2
        [InlineData("paladin", 9, 4, 3)]  // L9: 4/3
        [InlineData("ranger", 1, 0, 0)]
        [InlineData("ranger", 2, 2, 0)]
        [InlineData("ranger", 5, 4, 2)]
        [InlineData("ranger", 9, 4, 3)]
        public void HalfCaster_SpellSlotProgression(string classId, int level, int s1, int s2)
        {
            var resolved = ResolveClass(classId, level);
            Assert.Equal(s1, resolved.Resources.GetValueOrDefault("spell_slot_1", 0));
            Assert.Equal(s2, resolved.Resources.GetValueOrDefault("spell_slot_2", 0));
        }

        // ───────────────────────────────────────────
        // WARLOCK PACT SLOT PROGRESSION
        // ───────────────────────────────────────────

        [Theory]
        [InlineData(1, 1)]  // 1 pact slot
        [InlineData(2, 2)]  // 2 pact slots
        [InlineData(3, 2)]  // 2 pact slots (level 2 spells)
        [InlineData(5, 2)]  // 2 pact slots (level 3 spells)
        public void Warlock_PactSlotProgression(int level, int expectedPactSlots)
        {
            var resolved = ResolveClass("warlock", level);
            Assert.Equal(expectedPactSlots, resolved.Resources.GetValueOrDefault("pact_slots", 0));
        }

        // ───────────────────────────────────────────
        // MONK KI POINTS
        // ───────────────────────────────────────────

        [Theory]
        [InlineData(1, 2)]   // Ki = level + 1 (BG3 grants ki at L1)
        [InlineData(2, 3)]   // Ki = level + 1
        [InlineData(5, 6)]   // Ki = level + 1
        [InlineData(10, 11)] // Ki = level + 1
        [InlineData(12, 13)] // Ki = level + 1
        public void Monk_KiPointProgression(int level, int expectedKi)
        {
            var resolved = ResolveClass("monk", level);
            Assert.Equal(expectedKi, resolved.Resources.GetValueOrDefault("ki_points", 0));
        }

        // ───────────────────────────────────────────
        // BARBARIAN RAGE CHARGES
        // ───────────────────────────────────────────

        [Theory]
        [InlineData(1, 2)]
        [InlineData(2, 2)]
        [InlineData(3, 3)]
        [InlineData(5, 3)]
        [InlineData(6, 4)]
        [InlineData(11, 4)]
        [InlineData(12, 5)]
        public void Barbarian_RageChargeProgression(int level, int expectedRage)
        {
            var resolved = ResolveClass("barbarian", level);
            Assert.Equal(expectedRage, resolved.Resources.GetValueOrDefault("rage_charges", 0));
        }

        // ───────────────────────────────────────────
        // ROGUE SNEAK ATTACK DICE
        // ───────────────────────────────────────────

        [Theory]
        [InlineData(1, 1)]
        [InlineData(3, 2)]
        [InlineData(5, 3)]
        [InlineData(7, 4)]
        [InlineData(9, 5)]
        [InlineData(11, 6)]
        public void Rogue_SneakAttackDiceProgression(int level, int expectedDice)
        {
            var resolved = ResolveClass("rogue", level);
            Assert.Equal(expectedDice, resolved.Resources.GetValueOrDefault("sneak_attack_dice", 0));
        }

        // ───────────────────────────────────────────
        // SORCERER SORCERY POINTS
        // ───────────────────────────────────────────

        [Theory]
        [InlineData(1, 0)]
        [InlineData(2, 2)]
        [InlineData(5, 5)]
        [InlineData(12, 12)]
        public void Sorcerer_SorceryPointProgression(int level, int expectedPoints)
        {
            var resolved = ResolveClass("sorcerer", level);
            Assert.Equal(expectedPoints, resolved.Resources.GetValueOrDefault("sorcery_points", 0));
        }

        // ───────────────────────────────────────────
        // BARD BARDIC INSPIRATION
        // ───────────────────────────────────────────

        [Theory]
        [InlineData(1, 3)]   // 3 uses at L1 (CHA mod minimum)
        [InlineData(5, 3)]   // Still 3 at L5
        public void Bard_BardicInspirationProgression(int level, int expectedUses)
        {
            var resolved = ResolveClass("bard", level);
            Assert.Equal(expectedUses, resolved.Resources.GetValueOrDefault("bardic_inspiration", 0));
        }

        // ───────────────────────────────────────────
        // HIT DICE / HP
        // ───────────────────────────────────────────

        [Theory]
        [InlineData("fighter", 10)]
        [InlineData("barbarian", 12)]
        [InlineData("monk", 8)]
        [InlineData("rogue", 8)]
        [InlineData("wizard", 6)]
        [InlineData("sorcerer", 6)]
        [InlineData("bard", 8)]
        [InlineData("warlock", 8)]
        [InlineData("cleric", 8)]
        [InlineData("paladin", 10)]
        [InlineData("druid", 8)]
        [InlineData("ranger", 10)]
        public void HitDie_MatchesBG3(string classId, int expectedHitDie)
        {
            var classDef = _registry.GetClass(classId);
            Assert.NotNull(classDef);
            Assert.Equal(expectedHitDie, classDef.HitDie);
        }

        [Fact]
        public void Fighter_Level1_MaxHP_Equals10PlusCon()
        {
            // Default CON 10 → modifier 0 → HP = 10
            var resolved = ResolveClass("fighter", 1);
            Assert.Equal(10, resolved.MaxHP);
        }

        [Fact]
        public void Wizard_Level1_MaxHP_Equals6PlusCon()
        {
            var resolved = ResolveClass("wizard", 1);
            Assert.Equal(6, resolved.MaxHP);
        }

        // ───────────────────────────────────────────
        // SAVING THROW PROFICIENCIES
        // ───────────────────────────────────────────

        [Theory]
        [InlineData("fighter", "Strength", "Constitution")]
        [InlineData("wizard", "Intelligence", "Wisdom")]
        [InlineData("rogue", "Dexterity", "Intelligence")]
        [InlineData("cleric", "Wisdom", "Charisma")]
        [InlineData("barbarian", "Strength", "Constitution")]
        [InlineData("monk", "Strength", "Dexterity")]
        [InlineData("paladin", "Wisdom", "Charisma")]
        [InlineData("ranger", "Strength", "Dexterity")]
        [InlineData("sorcerer", "Constitution", "Charisma")]
        [InlineData("warlock", "Wisdom", "Charisma")]
        [InlineData("bard", "Dexterity", "Charisma")]
        [InlineData("druid", "Intelligence", "Wisdom")]
        public void SavingThrowProficiencies_MatchBG3(string classId, string save1, string save2)
        {
            var resolved = ResolveClass(classId, 1);
            Assert.True(
                Enum.TryParse<AbilityType>(save1, out var ability1),
                $"Invalid ability type: {save1}");
            Assert.True(
                Enum.TryParse<AbilityType>(save2, out var ability2),
                $"Invalid ability type: {save2}");
            Assert.True(resolved.Proficiencies.IsProficientInSave(ability1),
                $"{classId} should have {save1} save proficiency");
            Assert.True(resolved.Proficiencies.IsProficientInSave(ability2),
                $"{classId} should have {save2} save proficiency");
        }

        // ───────────────────────────────────────────
        // KEY FEATURES AT CORRECT LEVELS
        // ───────────────────────────────────────────

        [Theory]
        [InlineData("fighter", 1, "fighting_style")]
        [InlineData("fighter", 1, "second_wind")]
        [InlineData("fighter", 2, "action_surge")]
        [InlineData("fighter", 5, "extra_attack")]
        [InlineData("fighter", 9, "indomitable")]
        [InlineData("barbarian", 1, "rage")]
        [InlineData("barbarian", 2, "reckless_attack")]
        [InlineData("barbarian", 2, "danger_sense")]
        [InlineData("barbarian", 5, "extra_attack")]
        [InlineData("barbarian", 7, "feral_instinct")]
        [InlineData("barbarian", 9, "brutal_critical")]
        [InlineData("monk", 1, "martial_arts")]
        [InlineData("monk", 2, "flurry_of_blows")]
        [InlineData("monk", 2, "patient_defence")]
        [InlineData("monk", 3, "deflect_missiles")]
        [InlineData("monk", 4, "slow_fall")]
        [InlineData("monk", 5, "extra_attack")]
        [InlineData("monk", 5, "stunning_strike")]
        [InlineData("monk", 7, "evasion")]
        [InlineData("monk", 7, "stillness_of_mind")]
        [InlineData("rogue", 1, "sneak_attack")]
        [InlineData("rogue", 2, "cunning_action")]
        [InlineData("rogue", 5, "uncanny_dodge")]
        [InlineData("rogue", 7, "evasion")]
        [InlineData("paladin", 1, "divine_sense")]
        [InlineData("paladin", 2, "divine_smite")]
        [InlineData("paladin", 2, "lay_on_hands")]
        [InlineData("paladin", 5, "extra_attack")]
        [InlineData("paladin", 6, "aura_of_protection")]
        [InlineData("ranger", 1, "favoured_enemy")]
        [InlineData("ranger", 2, "fighting_style")]
        [InlineData("ranger", 5, "extra_attack")]
        [InlineData("wizard", 2, "arcane_recovery")]
        [InlineData("sorcerer", 2, "font_of_magic")]
        [InlineData("bard", 1, "bardic_inspiration")]
        [InlineData("bard", 2, "jack_of_all_trades")]
        [InlineData("bard", 2, "song_of_rest")]
        public void KeyFeature_PresentAtCorrectLevel(string classId, int level, string featureId)
        {
            var resolved = ResolveClass(classId, level);
            Assert.Contains(resolved.Features, f => f.Id == featureId);
        }

        [Theory]
        [InlineData("fighter", 4, "extra_attack")]  // Not yet at L4
        [InlineData("barbarian", 4, "extra_attack")]
        [InlineData("monk", 4, "extra_attack")]
        [InlineData("paladin", 4, "extra_attack")]
        [InlineData("ranger", 4, "extra_attack")]
        public void ExtraAttackFeature_NotPresentBelowLevel5(string classId, int level, string featureId)
        {
            var resolved = ResolveClass(classId, level);
            Assert.DoesNotContain(resolved.Features, f => f.Id == featureId);
        }

        // ───────────────────────────────────────────
        // MULTICLASS EXTRA ATTACKS (highest wins)
        // ───────────────────────────────────────────

        [Fact]
        public void Multiclass_Fighter5Barbarian5_ExtraAttacks1()
        {
            var sheet = new CharacterSheet
            {
                Name = "Fighter/Barbarian",
                ClassLevels = new List<ClassLevel>()
            };
            // 5 Fighter levels then 5 Barbarian levels
            for (int i = 0; i < 5; i++) sheet.ClassLevels.Add(new ClassLevel("fighter"));
            for (int i = 0; i < 5; i++) sheet.ClassLevels.Add(new ClassLevel("barbarian"));

            var resolved = _resolver.Resolve(sheet);
            // Both grant ExtraAttacks = 1, highest is 1
            Assert.Equal(1, resolved.ExtraAttacks);
        }

        [Fact]
        public void Multiclass_Fighter11Barbarian1_ExtraAttacks2()
        {
            var sheet = new CharacterSheet
            {
                Name = "Fighter 11 / Barbarian 1",
                ClassLevels = new List<ClassLevel>()
            };
            for (int i = 0; i < 11; i++) sheet.ClassLevels.Add(new ClassLevel("fighter"));
            sheet.ClassLevels.Add(new ClassLevel("barbarian"));

            var resolved = _resolver.Resolve(sheet);
            // Fighter 11 grants ExtraAttacks = 2
            Assert.Equal(2, resolved.ExtraAttacks);
        }

        // ───────────────────────────────────────────
        // ALL 12 CLASSES LOADED
        // ───────────────────────────────────────────

        [Fact]
        public void Registry_Contains_All12Classes()
        {
            var expectedIds = new[]
            {
                "fighter", "barbarian", "monk", "rogue",
                "cleric", "paladin", "druid", "ranger",
                "wizard", "sorcerer", "warlock", "bard"
            };

            foreach (var id in expectedIds)
            {
                Assert.NotNull(_registry.GetClass(id));
            }

            // At least 12 BG3 classes; test fixture classes (e.g. test_dummy) may also be present
            Assert.True(_registry.GetAllClasses().Count >= 12,
                $"Expected at least 12 classes, got {_registry.GetAllClasses().Count}");
        }

        // ───────────────────────────────────────────
        // HELPERS
        // ───────────────────────────────────────────

        private void AssertSpellSlots(ResolvedCharacter resolved, int s1, int s2, int s3, int s4, int s5, int s6)
        {
            Assert.Equal(s1, resolved.Resources.GetValueOrDefault("spell_slot_1", 0));
            Assert.Equal(s2, resolved.Resources.GetValueOrDefault("spell_slot_2", 0));
            Assert.Equal(s3, resolved.Resources.GetValueOrDefault("spell_slot_3", 0));
            Assert.Equal(s4, resolved.Resources.GetValueOrDefault("spell_slot_4", 0));
            Assert.Equal(s5, resolved.Resources.GetValueOrDefault("spell_slot_5", 0));
            Assert.Equal(s6, resolved.Resources.GetValueOrDefault("spell_slot_6", 0));
        }
    }
}
