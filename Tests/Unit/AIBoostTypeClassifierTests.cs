using System.Linq;
using Xunit;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Data.Statuses;
using QDND.Tests.Helpers;
using Godot;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for AIBoostTypeClassifier — verifies pattern matching
    /// of BG3 boost DSL strings to archetype profile multipliers.
    /// </summary>
    public class AIBoostTypeClassifierTests
    {
        private static BG3ArchetypeProfile CreateDefaultProfile()
        {
            return new BG3ArchetypeProfile();
        }

        [Fact]
        public void ClassifyBoosts_ACBoost_ReturnsMultiplierBoostAc()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("AC(2)", bg3);

            Assert.Single(results);
            Assert.Equal("AC", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostAc, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_AdvantageAttack_ReturnsCorrectMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("Advantage(AttackRoll)", bg3);

            Assert.Single(results);
            Assert.Equal("AdvantageAttack", results[0].boostType);
            Assert.Equal(bg3.MultiplierAdvantageAttack, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_MultipleBoosts_ReturnsAll()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("AC(2);Advantage(AttackRoll)", bg3);

            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.boostType == "AC");
            Assert.Contains(results, r => r.boostType == "AdvantageAttack");
        }

        [Fact]
        public void ClassifyBoosts_UnrecognizedBoost_ReturnsEmpty()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("SomeTotallyUnknownBoost(3)", bg3);

            Assert.Empty(results);
        }

        [Fact]
        public void ClassifyBoosts_NullOrEmpty_ReturnsEmpty()
        {
            var bg3 = CreateDefaultProfile();

            Assert.Empty(AIBoostTypeClassifier.ClassifyBoosts(null, bg3));
            Assert.Empty(AIBoostTypeClassifier.ClassifyBoosts("", bg3));
            Assert.Empty(AIBoostTypeClassifier.ClassifyBoosts("   ", bg3));
        }

        [Fact]
        public void ClassifyBoosts_NullProfile_ReturnsEmpty()
        {
            Assert.Empty(AIBoostTypeClassifier.ClassifyBoosts("AC(2)", null));
        }

        [Fact]
        public void ClassifyBoosts_CriticalHitAlways_ReturnsCorrectMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("CriticalHit(Always)", bg3);

            Assert.Single(results);
            Assert.Equal("CriticalHitAlways", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostCriticalHitAlways, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_CriticalHitNever_ReturnsNever()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("CriticalHit(Never)", bg3);

            Assert.Single(results);
            Assert.Equal("CriticalHitNever", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostCriticalHitNever, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_Resistance_ReturnsMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("Resistance(Fire,Resistant)", bg3);

            Assert.Single(results);
            Assert.Equal("Resistance", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostResistance, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_WeaponDamage_ReturnsMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("WeaponDamage(1d6)", bg3);

            Assert.Single(results);
            Assert.Equal("WeaponDamage", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostWeaponDamage, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_DamageBonus_MapsToWeaponDamage()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("DamageBonus(1d4)", bg3);

            Assert.Single(results);
            Assert.Equal("WeaponDamage", results[0].boostType);
        }

        [Fact]
        public void ClassifyBoosts_ActionResource_ReturnsMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("ActionResource(SpellSlot,1,1)", bg3);

            Assert.Single(results);
            Assert.Equal("ActionResource", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostActionResource, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_ActionResourceMultiplier_UsesSpecificProperty()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("ActionResourceMultiplier(SpellSlot,2,1)", bg3);

            Assert.Single(results);
            Assert.Equal("ActionResourceMultiplier", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostActionResourceMultiplier, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_BlockSpellCast_ReturnsMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("BlockSpellCast", bg3);

            Assert.Single(results);
            Assert.Equal("BlockSpellCast", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostBlockSpellCast, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_AdvantageAbility_ReturnsCorrectMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("Advantage(Ability,Strength)", bg3);

            Assert.Single(results);
            Assert.Equal("AdvantageAbility", results[0].boostType);
            Assert.Equal(bg3.MultiplierAdvantageAbility, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_AdvantageSkill_ReturnsCorrectMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("Advantage(Skill,Stealth)", bg3);

            Assert.Single(results);
            Assert.Equal("AdvantageSkill", results[0].boostType);
            Assert.Equal(bg3.MultiplierAdvantageSkill, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_AbilityFailedSavingThrow_UsesSpecificProperty()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("AbilityFailedSavingThrow(Constitution,1)", bg3);

            Assert.Single(results);
            Assert.Equal("AbilityFailedSavingThrow", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostAbilityFailedSavingThrow, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_TemporaryHP_ReturnsMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("TemporaryHP(10)", bg3);

            Assert.Single(results);
            Assert.Equal("TemporaryHp", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostTemporaryHp, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_SavingThrow_ReturnsMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("SavingThrow(Wisdom,2)", bg3);

            Assert.Single(results);
            Assert.Equal("SavingThrow", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostSavingThrow, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_MixedKnownAndUnknown_ReturnsOnlyKnown()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("AC(2);UnknownThing;Resistance(Fire,Resistant)", bg3);

            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.boostType == "AC");
            Assert.Contains(results, r => r.boostType == "Resistance");
        }

        [Fact]
        public void ClassifyBoosts_CaseInsensitive_MatchesCorrectly()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("ac(2)", bg3);

            Assert.Single(results);
            Assert.Equal("AC", results[0].boostType);
        }

        [Fact]
        public void ClassifyBoosts_IgnoreFallDamage_ReturnsNegativeMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("IgnoreFallDamage", bg3);

            Assert.Single(results);
            Assert.Equal("IgnoreFallDamage", results[0].boostType);
            Assert.True(results[0].multiplier < 0, "IgnoreFallDamage should have a negative multiplier");
        }

        // ──────────────────────────────────────────────
        //  MAJOR 1: Disadvantage handling
        // ──────────────────────────────────────────────

        [Fact]
        public void ClassifyBoosts_DisadvantageAttackRoll_ReturnsNegativeAdvantageAttack()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("Disadvantage(AttackRoll)", bg3);

            Assert.Single(results);
            Assert.Equal("DisadvantageAttack", results[0].boostType);
            Assert.Equal(-bg3.MultiplierAdvantageAttack, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_DisadvantageAllAbilities_ReturnsNegativeAdvantageAbility()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("Disadvantage(AllAbilities)", bg3);

            Assert.Single(results);
            Assert.Equal("DisadvantageAbility", results[0].boostType);
            Assert.Equal(-bg3.MultiplierAdvantageAbility, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_DisadvantageAbility_ReturnsNegativeAdvantageAbility()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("Disadvantage(Ability, Dexterity)", bg3);

            Assert.Single(results);
            Assert.Equal("DisadvantageAbility", results[0].boostType);
            Assert.Equal(-bg3.MultiplierAdvantageAbility, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_DisadvantageSavingThrow_ReturnsNegativeSavingThrow()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("Disadvantage(SavingThrow, Strength)", bg3);

            Assert.Single(results);
            Assert.Equal("DisadvantageSavingThrow", results[0].boostType);
            Assert.Equal(-bg3.MultiplierBoostSavingThrow, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_DisadvantageAllSavingThrows_ReturnsNegativeSavingThrow()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("Disadvantage(AllSavingThrows)", bg3);

            Assert.Single(results);
            Assert.Equal("DisadvantageSavingThrow", results[0].boostType);
            Assert.Equal(-bg3.MultiplierBoostSavingThrow, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_DisadvantageSkill_ReturnsNegativeAdvantageSkill()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("Disadvantage(Skill, Stealth)", bg3);

            Assert.Single(results);
            Assert.Equal("DisadvantageSkill", results[0].boostType);
            Assert.Equal(-bg3.MultiplierAdvantageSkill, results[0].multiplier);
        }

        // ──────────────────────────────────────────────
        //  MAJOR 2: ActionResource(Movement) & MovementSpeedLimit
        // ──────────────────────────────────────────────

        [Fact]
        public void ClassifyBoosts_ActionResourceMovement_ReturnsMovementMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("ActionResource(Movement,3,0)", bg3);

            Assert.Single(results);
            Assert.Equal("Movement", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostMovement, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_ActionResourceNonMovement_ReturnsGenericActionResource()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("ActionResource(SpellSlot,1,1)", bg3);

            Assert.Single(results);
            Assert.Equal("ActionResource", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostActionResource, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_MovementSpeedLimit_ReturnsMovementMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("MovementSpeedLimit(Walk)", bg3);

            Assert.Single(results);
            Assert.Equal("Movement", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostMovement, results[0].multiplier);
        }

        // ──────────────────────────────────────────────
        //  MAJOR 3: SightRange variants
        // ──────────────────────────────────────────────

        [Fact]
        public void ClassifyBoosts_SightRangeOverride_ReturnsMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("SightRangeOverride(0)", bg3);

            Assert.Single(results);
            Assert.Equal("SightRange", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostSightRange, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_SightRangeAdditive_ReturnsMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("SightRangeAdditive(5)", bg3);

            Assert.Single(results);
            Assert.Equal("SightRange", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostSightRange, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_SightRangeMinimum_ReturnsMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("SightRangeMinimum(2)", bg3);

            Assert.Single(results);
            Assert.Equal("SightRange", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostSightRange, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_SightRangeMaximum_ReturnsMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("SightRangeMaximum(3)", bg3);

            Assert.Single(results);
            Assert.Equal("SightRange", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostSightRange, results[0].multiplier);
        }

        // ──────────────────────────────────────────────
        //  MAJOR 4: Advantage(SavingThrow) / Advantage(AllSavingThrows)
        // ──────────────────────────────────────────────

        [Fact]
        public void ClassifyBoosts_AdvantageSavingThrow_ReturnsMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("Advantage(SavingThrow, Dexterity)", bg3);

            Assert.Single(results);
            Assert.Equal("AdvantageSavingThrow", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostSavingThrow, results[0].multiplier);
        }

        [Fact]
        public void ClassifyBoosts_AdvantageAllSavingThrows_ReturnsMultiplier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("Advantage(AllSavingThrows)", bg3);

            Assert.Single(results);
            Assert.Equal("AdvantageSavingThrow", results[0].boostType);
            Assert.Equal(bg3.MultiplierBoostSavingThrow, results[0].multiplier);
        }

        // ──────────────────────────────────────────────
        //  MAJOR 5: RollBonus parsing
        // ──────────────────────────────────────────────

        [Fact]
        public void RollBonus_Attack_ReturnsAttackModifier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("RollBonus(Attack,1d4)", bg3);

            Assert.Single(results);
            Assert.Equal("RollBonus_Attack", results[0].boostType);
            // modifier * average(1d4)=2.5
            Assert.Equal(bg3.ModifierBoostRollbonusAttack * 2.5f, results[0].multiplier, 4);
        }

        [Fact]
        public void RollBonus_MeleeWeaponAttack_ReturnsMeleeModifier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("RollBonus(MeleeWeaponAttack,1d4)", bg3);

            Assert.Single(results);
            Assert.Equal("RollBonus_MeleeWeaponAttack", results[0].boostType);
            Assert.Equal(bg3.ModifierBoostRollbonusMeleeweaponattack * 2.5f, results[0].multiplier, 4);
        }

        [Fact]
        public void RollBonus_SavingThrow_ReturnsSavingThrowModifier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("RollBonus(SavingThrow,1d4)", bg3);

            Assert.Single(results);
            Assert.Equal("RollBonus_SavingThrow", results[0].boostType);
            Assert.Equal(bg3.ModifierBoostRollbonusSavingthrow * 2.5f, results[0].multiplier, 4);
        }

        [Fact]
        public void RollBonus_SkillCheck_ReturnsSkillModifier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("RollBonus(SkillCheck,1d4)", bg3);

            Assert.Single(results);
            Assert.Equal("RollBonus_SkillCheck", results[0].boostType);
            Assert.Equal(bg3.ModifierBoostRollbonusSkill * 2.5f, results[0].multiplier, 4);
        }

        [Fact]
        public void RollBonus_Damage_ReturnsDamageModifier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("RollBonus(Damage,1d6,Fire)", bg3);

            Assert.Single(results);
            Assert.Equal("RollBonus_Damage", results[0].boostType);
            // modifier * average(1d6)=3.5
            Assert.Equal(bg3.ModifierBoostRollbonusDamage * 3.5f, results[0].multiplier, 4);
        }

        [Fact]
        public void RollBonus_UnknownType_ReturnsGenericAttack()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("RollBonus(SomeUnknownRoll,1d4)", bg3);

            Assert.Single(results);
            Assert.Equal("RollBonus_SomeUnknownRoll", results[0].boostType);
            // Falls back to generic attack modifier
            Assert.Equal(bg3.ModifierBoostRollbonusAttack * 2.5f, results[0].multiplier, 4);
        }

        [Fact]
        public void RollBonus_CaseInsensitive_Works()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("rollbonus(attack,1d4)", bg3);

            Assert.Single(results);
            Assert.Equal("RollBonus_attack", results[0].boostType);
            Assert.Equal(bg3.ModifierBoostRollbonusAttack * 2.5f, results[0].multiplier, 4);
        }

        [Fact]
        public void RollBonus_FlatValue_UsesValueAsScale()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("RollBonus(Attack,2)", bg3);

            Assert.Single(results);
            Assert.Equal(bg3.ModifierBoostRollbonusAttack * 2f, results[0].multiplier, 4);
        }

        [Fact]
        public void RollBonus_NegativeDice_UsesAbsoluteAverage()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("RollBonus(Attack,-1d4)", bg3);

            Assert.Single(results);
            // Negative dice stripped to magnitude: avg(1d4)=2.5
            Assert.Equal(bg3.ModifierBoostRollbonusAttack * 2.5f, results[0].multiplier, 4);
        }

        [Fact]
        public void RollBonus_DeathSavingThrow_MapsSavingThrowModifier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("RollBonus(DeathSavingThrow,1d4)", bg3);

            Assert.Single(results);
            Assert.Equal("RollBonus_DeathSavingThrow", results[0].boostType);
            Assert.Equal(bg3.ModifierBoostRollbonusSavingthrow * 2.5f, results[0].multiplier, 4);
        }

        [Fact]
        public void RollBonus_RawAbility_MapsAbilityModifier()
        {
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("RollBonus(RawAbility,1d6)", bg3);

            Assert.Single(results);
            Assert.Equal("RollBonus_RawAbility", results[0].boostType);
            Assert.Equal(bg3.ModifierBoostRollbonusAbility * 3.5f, results[0].multiplier, 4);
        }

        [Fact]
        public void RollBonus_MultipleInBG3String_ReturnsAll()
        {
            // Real BG3 pattern: Bless
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts(
                "RollBonus(Attack,1d4);RollBonus(SavingThrow,1d4);RollBonus(DeathSavingThrow,1d4)", bg3);

            Assert.Equal(3, results.Count);
            Assert.Contains(results, r => r.boostType == "RollBonus_Attack");
            Assert.Contains(results, r => r.boostType == "RollBonus_SavingThrow");
            Assert.Contains(results, r => r.boostType == "RollBonus_DeathSavingThrow");
        }

        [Fact]
        public void RollBonus_MixedWithOtherBoosts_AllClassified()
        {
            // Real BG3: AC(3);RollBonus(SavingThrow,3)
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts("AC(3);RollBonus(SavingThrow,3)", bg3);

            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.boostType == "AC");
            Assert.Contains(results, r => r.boostType == "RollBonus_SavingThrow");
        }

        // ──────────────────────────────────────────────
        //  Real BG3 multi-boost strings with Disadvantage
        // ──────────────────────────────────────────────

        [Fact]
        public void ClassifyBoosts_BG3FearBoosts_MatchesDisadvantage()
        {
            // From Status_FEAR.txt: Disadvantage(AllAbilities);Disadvantage(AttackRoll)
            var bg3 = CreateDefaultProfile();
            var results = AIBoostTypeClassifier.ClassifyBoosts(
                "Disadvantage(AllAbilities);Disadvantage(AttackRoll)", bg3);

            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.boostType == "DisadvantageAbility");
            Assert.Contains(results, r => r.boostType == "DisadvantageAttack");
            Assert.All(results, r => Assert.True(r.multiplier < 0, $"{r.boostType} should be negative"));
        }
    }

    /// <summary>
    /// Integration tests verifying AIScorer uses boost-type-specific multipliers
    /// when scoring status effects with BG3 profiles.
    /// </summary>
    public class AIScorerBoostTypeIntegrationTests
    {
        private AIScorer CreateScorer(StatusRegistry statusRegistry = null)
        {
            ICombatContext context = null;
            if (statusRegistry != null)
            {
                var headless = new HeadlessCombatContext();
                headless.RegisterService(statusRegistry);
                context = headless;
            }
            return new AIScorer(context);
        }

        private Combatant CreateTestCombatant(string id, int hp, int maxHp, Vector3 position, Faction faction)
        {
            var combatant = new Combatant(id, id, faction, maxHp, 10);
            combatant.Resources.CurrentHP = hp;
            combatant.Position = position;
            return combatant;
        }

        [Fact]
        public void ScoreStatusEffect_ACBoostStatus_UsesTypeSpecificMultiplier()
        {
            // Arrange: register a BOOST status with AC(2) in the boosts string
            var registry = new StatusRegistry();
            registry.RegisterStatus(new BG3StatusData
            {
                StatusId = "SHIELD_OF_FAITH",
                StatusType = BG3StatusType.BOOST,
                Boosts = "AC(2)"
            });

            var scorer = CreateScorer(registry);
            var profile = new AIProfile { BG3Profile = new BG3ArchetypeProfile() };
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var target = CreateTestCombatant("ally", 40, 40, new Vector3(5, 0, 0), Faction.Player);

            var actionWithBoosts = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "shield_of_faith" };

            // Act
            scorer.ScoreStatusEffect(actionWithBoosts, actor, target, "SHIELD_OF_FAITH", profile);

            // Assert — should have the boost_type_score breakdown entry
            Assert.True(actionWithBoosts.ScoreBreakdown.ContainsKey("boost_type_score"),
                "Expected boost_type_score in breakdown when status has typed boosts");
            Assert.True(actionWithBoosts.ScoreBreakdown["boost_type_score"] > 0,
                "AC boost type score should be positive");
        }

        [Fact]
        public void ScoreStatusEffect_NoBoostsString_NoBoostTypeScore()
        {
            // Arrange: status with no boosts string — should use generic scoring only
            var registry = new StatusRegistry();
            registry.RegisterStatus(new BG3StatusData
            {
                StatusId = "GENERIC_BUFF",
                StatusType = BG3StatusType.BOOST,
                Boosts = null
            });

            var scorer = CreateScorer(registry);
            var profile = new AIProfile { BG3Profile = new BG3ArchetypeProfile() };
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);
            var self = actor; // self-cast

            var action = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "generic_buff" };

            // Act
            scorer.ScoreStatusEffect(action, actor, self, "GENERIC_BUFF", profile);

            // Assert — should NOT have boost_type_score
            Assert.False(action.ScoreBreakdown.ContainsKey("boost_type_score"),
                "Should not have boost_type_score when status has no Boosts string");
        }

        [Fact]
        public void ScoreStatusEffect_MultiBoostStatus_ScoresHigherThanSingle()
        {
            // Arrange: one status with AC+Advantage vs one with just AC
            var registry = new StatusRegistry();
            registry.RegisterStatus(new BG3StatusData
            {
                StatusId = "AC_ONLY",
                StatusType = BG3StatusType.BOOST,
                Boosts = "AC(2)"
            });
            registry.RegisterStatus(new BG3StatusData
            {
                StatusId = "AC_AND_ADVANTAGE",
                StatusType = BG3StatusType.BOOST,
                Boosts = "AC(2);Advantage(AttackRoll)"
            });

            var scorer = CreateScorer(registry);
            var profile = new AIProfile { BG3Profile = new BG3ArchetypeProfile() };
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);

            var acAction = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "ac" };
            var multiAction = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "multi" };

            // Act
            scorer.ScoreStatusEffect(acAction, actor, actor, "AC_ONLY", profile);
            scorer.ScoreStatusEffect(multiAction, actor, actor, "AC_AND_ADVANTAGE", profile);

            // Assert
            Assert.True(acAction.ScoreBreakdown.ContainsKey("boost_type_score"),
                "AC_ONLY should have boost_type_score in breakdown");
            Assert.True(multiAction.ScoreBreakdown.ContainsKey("boost_type_score"),
                "AC_AND_ADVANTAGE should have boost_type_score in breakdown");
            Assert.True(multiAction.Score > acAction.Score,
                $"Multi-boost ({multiAction.Score}) should score higher than single boost ({acAction.Score})");
        }

        [Fact]
        public void ScoreStatusEffect_NoBG3Profile_NoBoostTypeScore()
        {
            // Arrange: legacy profile (no BG3)
            var registry = new StatusRegistry();
            registry.RegisterStatus(new BG3StatusData
            {
                StatusId = "SOME_BUFF",
                StatusType = BG3StatusType.BOOST,
                Boosts = "AC(2)"
            });

            var scorer = CreateScorer(registry);
            var profile = new AIProfile(); // No BG3Profile
            var actor = CreateTestCombatant("actor", 50, 50, Vector3.Zero, Faction.Player);

            var action = new AIAction { ActionType = AIActionType.UseAbility, ActionId = "buff" };

            // Act
            scorer.ScoreStatusEffect(action, actor, actor, "SOME_BUFF", profile);

            // Assert — legacy path, no boost_type_score
            Assert.False(action.ScoreBreakdown.ContainsKey("boost_type_score"));
        }
    }
}
