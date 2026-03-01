using System;
using System.Collections.Generic;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Strongly-typed representation of all BG3 AI archetype scoring parameters.
    /// Defaults match BG3_Data/AI/Archetypes/base.txt exactly.
    /// Use <see cref="LoadFromSettings"/> to override values from resolved archetype data.
    /// </summary>
    public sealed class BG3ArchetypeProfile
    {
        // ──────────────────────────────────────────────
        //  General Score Configuration
        // ──────────────────────────────────────────────

        /// <summary>Remaps all scores – a score of ScoreMod equals the average damage of this character.</summary>
        public float ScoreMod { get; private set; } = 100.0f;

        /// <summary>Base nearby-character score.</summary>
        public float BaseNearbyScore { get; private set; } = 0.2f;

        // ──────────────────────────────────────────────
        //  Archetype Multipliers  (Effect × Target × Polarity)
        //  6 Effects × 4 Targets × 2 Polarities = 48 properties
        // ──────────────────────────────────────────────

        #region Damage

        /// <summary>MULTIPLIER_DAMAGE_SELF_POS</summary>
        public float MultiplierDamageSelfPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_DAMAGE_SELF_NEG</summary>
        public float MultiplierDamageSelfNeg { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_DAMAGE_ENEMY_POS</summary>
        public float MultiplierDamageEnemyPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_DAMAGE_ENEMY_NEG</summary>
        public float MultiplierDamageEnemyNeg { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_DAMAGE_ALLY_POS</summary>
        public float MultiplierDamageAllyPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_DAMAGE_ALLY_NEG – damaging allies looks stupid.</summary>
        public float MultiplierDamageAllyNeg { get; private set; } = 4.0f;

        /// <summary>MULTIPLIER_DAMAGE_NEUTRAL_POS</summary>
        public float MultiplierDamageNeutralPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_DAMAGE_NEUTRAL_NEG – avoid damaging neutrals.</summary>
        public float MultiplierDamageNeutralNeg { get; private set; } = 1.5f;

        #endregion

        #region Heal

        /// <summary>MULTIPLIER_HEAL_SELF_POS – generally want AI damaging, not healing.</summary>
        public float MultiplierHealSelfPos { get; private set; } = 0.75f;

        /// <summary>MULTIPLIER_HEAL_SELF_NEG</summary>
        public float MultiplierHealSelfNeg { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_HEAL_ENEMY_POS</summary>
        public float MultiplierHealEnemyPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_HEAL_ENEMY_NEG</summary>
        public float MultiplierHealEnemyNeg { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_HEAL_ALLY_POS – healing self is slightly prioritised over allies.</summary>
        public float MultiplierHealAllyPos { get; private set; } = 0.70f;

        /// <summary>MULTIPLIER_HEAL_ALLY_NEG</summary>
        public float MultiplierHealAllyNeg { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_HEAL_NEUTRAL_POS</summary>
        public float MultiplierHealNeutralPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_HEAL_NEUTRAL_NEG</summary>
        public float MultiplierHealNeutralNeg { get; private set; } = 1.0f;

        #endregion

        #region Dot

        /// <summary>MULTIPLIER_DOT_SELF_POS</summary>
        public float MultiplierDotSelfPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_DOT_SELF_NEG</summary>
        public float MultiplierDotSelfNeg { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_DOT_ENEMY_POS</summary>
        public float MultiplierDotEnemyPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_DOT_ENEMY_NEG</summary>
        public float MultiplierDotEnemyNeg { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_DOT_ALLY_POS</summary>
        public float MultiplierDotAllyPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_DOT_ALLY_NEG</summary>
        public float MultiplierDotAllyNeg { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_DOT_NEUTRAL_POS</summary>
        public float MultiplierDotNeutralPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_DOT_NEUTRAL_NEG</summary>
        public float MultiplierDotNeutralNeg { get; private set; } = 1.0f;

        #endregion

        #region Hot

        /// <summary>MULTIPLIER_HOT_SELF_POS</summary>
        public float MultiplierHotSelfPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_HOT_SELF_NEG</summary>
        public float MultiplierHotSelfNeg { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_HOT_ENEMY_POS</summary>
        public float MultiplierHotEnemyPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_HOT_ENEMY_NEG</summary>
        public float MultiplierHotEnemyNeg { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_HOT_ALLY_POS</summary>
        public float MultiplierHotAllyPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_HOT_ALLY_NEG</summary>
        public float MultiplierHotAllyNeg { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_HOT_NEUTRAL_POS</summary>
        public float MultiplierHotNeutralPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_HOT_NEUTRAL_NEG</summary>
        public float MultiplierHotNeutralNeg { get; private set; } = 1.0f;

        #endregion

        #region Control

        /// <summary>MULTIPLIER_CONTROL_SELF_POS</summary>
        public float MultiplierControlSelfPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_CONTROL_SELF_NEG – knocking yourself down always looks stupid.</summary>
        public float MultiplierControlSelfNeg { get; private set; } = 2.0f;

        /// <summary>MULTIPLIER_CONTROL_ENEMY_POS</summary>
        public float MultiplierControlEnemyPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_CONTROL_ENEMY_NEG</summary>
        public float MultiplierControlEnemyNeg { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_CONTROL_ALLY_POS</summary>
        public float MultiplierControlAllyPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_CONTROL_ALLY_NEG – knocking down allies is pretty stupid.</summary>
        public float MultiplierControlAllyNeg { get; private set; } = 2.0f;

        /// <summary>MULTIPLIER_CONTROL_NEUTRAL_POS</summary>
        public float MultiplierControlNeutralPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_CONTROL_NEUTRAL_NEG</summary>
        public float MultiplierControlNeutralNeg { get; private set; } = 1.0f;

        #endregion

        #region Boost

        /// <summary>MULTIPLIER_BOOST_SELF_POS</summary>
        public float MultiplierBoostSelfPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_BOOST_SELF_NEG</summary>
        public float MultiplierBoostSelfNeg { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_BOOST_ENEMY_POS</summary>
        public float MultiplierBoostEnemyPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_BOOST_ENEMY_NEG</summary>
        public float MultiplierBoostEnemyNeg { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_BOOST_ALLY_POS</summary>
        public float MultiplierBoostAllyPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_BOOST_ALLY_NEG</summary>
        public float MultiplierBoostAllyNeg { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_BOOST_NEUTRAL_POS</summary>
        public float MultiplierBoostNeutralPos { get; private set; } = 1.0f;

        /// <summary>MULTIPLIER_BOOST_NEUTRAL_NEG</summary>
        public float MultiplierBoostNeutralNeg { get; private set; } = 1.0f;

        #endregion

        // ──────────────────────────────────────────────
        //  Final Score Multipliers
        // ──────────────────────────────────────────────

        /// <summary>Used when an action is free (0 AP cost).</summary>
        public float MultiplierFreeAction { get; private set; } = 0.90f;

        /// <summary>Score penalty per cooldown turn remaining.</summary>
        public float MultiplierCooldownMultiplier { get; private set; } = 0.01f;

        /// <summary>Score when item amount is low.</summary>
        public float MultiplierLowItemAmountMultiplier { get; private set; } = 0.80f;

        /// <summary>Score when item amount is high.</summary>
        public float MultiplierHighItemAmountMultiplier { get; private set; } = 1.00f;

        /// <summary>Score penalty when action cannot execute this turn.</summary>
        public float MultiplierCannotExecuteThisTurn { get; private set; } = 0.20f;

        /// <summary>Boost the score of the action AI planned after using a move spell.</summary>
        public float MultiplierPlannedActionWithMoveSpell { get; private set; } = 1.50f;

        /// <summary>Scale effect of action resource costs.</summary>
        public float MultiplierActionResourceCost { get; private set; } = 0.01f;

        /// <summary>How much characters want to interact with usable items (e.g. exploding barrels).</summary>
        public float MultiplierUsableItem { get; private set; } = 1.00f;

        /// <summary>0.0 means AI will not avoid AoO.</summary>
        public float EnableMovementAvoidAOO { get; private set; } = 1.00f;

        // ──────────────────────────────────────────────
        //  Target Selection Multipliers
        // ──────────────────────────────────────────────

        /// <summary>Multiplier when target is the enemy of the source (source attacked target).</summary>
        public float MultiplierTargetMyEnemy { get; private set; } = 1.25f;

        /// <summary>Multiplier when source is the enemy of the target (target attacked source).</summary>
        public float MultiplierTargetMyHostile { get; private set; } = 1.50f;

        /// <summary>Target is a summon.</summary>
        public float MultiplierTargetSummon { get; private set; } = 0.50f;

        /// <summary>Target is aggro-marked.</summary>
        public float MultiplierTargetAggroMarked { get; private set; } = 5.00f;

        /// <summary>Only one hostile character.</summary>
        public float MultiplierTargetHostileCountOne { get; private set; } = 0.75f;

        /// <summary>Two or more hostile characters.</summary>
        public float MultiplierTargetHostileCountTwoOrMore { get; private set; } = 0.50f;

        /// <summary>Target is in sight.</summary>
        public float MultiplierTargetInSight { get; private set; } = 1.05f;

        /// <summary>Target is incapacitated.</summary>
        public float MultiplierTargetIncapacitated { get; private set; } = 1.00f;

        /// <summary>Target is knocked down.</summary>
        public float MultiplierTargetKnockedDown { get; private set; } = 1.25f;

        /// <summary>Target is preferred.</summary>
        public float MultiplierTargetPreferred { get; private set; } = 2.00f;

        /// <summary>Target is unpreferred.</summary>
        public float MultiplierTargetUnpreferred { get; private set; } = 0.50f;

        /// <summary>How important the current HP of a target is (lower HP = higher score).</summary>
        public float MultiplierTargetHealthBias { get; private set; } = 0.00f;

        /// <summary>Target is a downed enemy.</summary>
        public float MultiplierTargetEnemyDowned { get; private set; } = 0.10f;

        /// <summary>Target is a downed ally.</summary>
        public float MultiplierTargetAllyDowned { get; private set; } = 1.10f;

        /// <summary>Target is a downed neutral.</summary>
        public float MultiplierTargetNeutralDowned { get; private set; } = 1.00f;

        // ──────────────────────────────────────────────
        //  End-Position Multipliers
        // ──────────────────────────────────────────────

        /// <summary>Score for allies nearby at end position.</summary>
        public float MultiplierEndposAlliesNearby { get; private set; } = 0.00f;

        /// <summary>Distance at which being any closer to allies makes no difference.</summary>
        public float EndposAlliesNearbyMinDistance { get; private set; } = 1.90f;

        /// <summary>Ignore allied characters beyond this distance.</summary>
        public float EndposAlliesNearbyMaxDistance { get; private set; } = 6.00f;

        /// <summary>Score for enemies nearby at end position.</summary>
        public float MultiplierEndposEnemiesNearby { get; private set; } = 0.02f;

        /// <summary>Distance at which being any closer to enemies makes no difference.</summary>
        public float EndposEnemiesNearbyMinDistance { get; private set; } = 1.90f;

        /// <summary>Ignore enemy characters beyond this distance.</summary>
        public float EndposEnemiesNearbyMaxDistance { get; private set; } = 6.00f;

        /// <summary>Score for being flanked at end position.</summary>
        public float MultiplierEndposFlanked { get; private set; } = 0.05f;

        /// <summary>Score for height difference at end position.</summary>
        public float MultiplierEndposHeightDifference { get; private set; } = 0.00f;

        /// <summary>Score for turning invisible at end position.</summary>
        public float MultiplierEndposTurnedInvisible { get; private set; } = 0.01f;

        /// <summary>Score for not being in an AI hint area.</summary>
        public float MultiplierEndposNotInAihint { get; private set; } = 0.25f;

        /// <summary>Score for not being in smoke at end position.</summary>
        public float MultiplierEndposNotInSmoke { get; private set; } = 0.00f;

        /// <summary>Score for not being in a dangerous surface.</summary>
        public float MultiplierEndposNotInDangerousSurface { get; private set; } = 0.10f;

        /// <summary>Scoring for dangerous items nearby a character.</summary>
        public float DangerousItemNearby { get; private set; } = 0.00f;

        /// <summary>Score for height relative to the highest enemy.</summary>
        public float MultiplierEnemyHeightDifference { get; private set; } = 0.002f;

        /// <summary>Once AI is this much higher than all enemies, no point going higher.</summary>
        public float EnemyHeightDifferenceClamp { get; private set; } = 5.00f;

        /// <summary>Radius of enemies to check heights.</summary>
        public float EnemyHeightScoreRadiusXz { get; private set; } = 100.00f;

        /// <summary>Max distance to closest enemy before fallback kicks in.</summary>
        public float MaxDistanceToClosestEnemy { get; private set; } = 25.0f;

        /// <summary>Tiny score so only if nothing better to do, try getting closer.</summary>
        public float MultiplierNoEnemiesInMaxDistance { get; private set; } = 0.0001f;

        // ──────────────────────────────────────────────
        //  Fallback Settings
        // ──────────────────────────────────────────────

        /// <summary>Fallback score for allies nearby.</summary>
        public float MultiplierFallbackAlliesNearby { get; private set; } = 0.00f;

        /// <summary>Fallback: distance at which being any closer to allies makes no difference.</summary>
        public float FallbackAlliesNearbyMinDistance { get; private set; } = 1.90f;

        /// <summary>Fallback: ignore allied characters beyond this distance.</summary>
        public float FallbackAlliesNearbyMaxDistance { get; private set; } = 6.00f;

        /// <summary>Fallback score for enemies nearby.</summary>
        public float MultiplierFallbackEnemiesNearby { get; private set; } = 0.00f;

        /// <summary>Fallback: distance at which being any closer to enemies makes no difference.</summary>
        public float FallbackEnemiesNearbyMinDistance { get; private set; } = 1.90f;

        /// <summary>Fallback: ignore enemy characters beyond this distance.</summary>
        public float FallbackEnemiesNearbyMaxDistance { get; private set; } = 6.00f;

        /// <summary>Fallback height difference score.</summary>
        public float FallbackHeightDifference { get; private set; } = 0.00f;

        /// <summary>Prefer fallback jump forward if linear_distance + this &lt; walk_distance.</summary>
        public float FallbackJumpOverWalkPreferredDistance { get; private set; } = 2.00f;

        /// <summary>Any fallback jump action will score at least this value.</summary>
        public float FallbackJumpBaseScore { get; private set; } = 40.00f;

        /// <summary>Multiplier applied to regular fallback score when a fallback jump is possible.</summary>
        public float FallbackMultiplierVsFallbackJump { get; private set; } = 0.50f;

        /// <summary>Future action score during fallback.</summary>
        public float FallbackFutureScore { get; private set; } = 10f;

        /// <summary>Base score for attacking an item blocking the path to closest enemy.</summary>
        public float FallbackAttackBlockerScore { get; private set; } = 0.12f;

        // ──────────────────────────────────────────────
        //  General Score Multipliers
        // ──────────────────────────────────────────────

        /// <summary>Score modifier when targeting neutrals.</summary>
        public float MultiplierScoreOnNeutral { get; private set; } = -0.9f;

        /// <summary>Score modifier when targeting allies.</summary>
        public float MultiplierScoreOnAlly { get; private set; } = -1.1f;

        /// <summary>Score modifier when out of combat.</summary>
        public float MultiplierScoreOutOfCombat { get; private set; } = 0.25f;

        /// <summary>Heal urgency: closer to 1 = more urgent when near death.</summary>
        public float MaxHealMultiplier { get; private set; } = 0.1f;

        /// <summary>Self-heal urgency: closer to 1 = more urgent when near death.</summary>
        public float MaxHealSelfMultiplier { get; private set; } = 0.1f;

        // ──────────────────────────────────────────────
        //  Kill Multipliers
        // ──────────────────────────────────────────────

        /// <summary>Bonus when damage results in killing an enemy.</summary>
        public float MultiplierKillEnemy { get; private set; } = 1.25f;

        /// <summary>Bonus when damage results in killing an enemy summon.</summary>
        public float MultiplierKillEnemySummon { get; private set; } = 1.05f;

        /// <summary>Penalty when damage results in killing an ally.</summary>
        public float MultiplierKillAlly { get; private set; } = 1.50f;

        /// <summary>Penalty when damage results in killing an allied summon.</summary>
        public float MultiplierKillAllySummon { get; private set; } = 1.10f;

        /// <summary>How important target health is for kill scoring.</summary>
        public float MultiplierKillTargetHealthBias { get; private set; } = 0.0f;

        /// <summary>Base score for instakill evaluation.</summary>
        public float InstakillBaseScore { get; private set; } = 1.5f;

        /// <summary>Health bias for instakill scoring.</summary>
        public float MultiplierInstakillTargetHealthBias { get; private set; } = 0.025f;

        // ──────────────────────────────────────────────
        //  Status Multipliers
        // ──────────────────────────────────────────────

        /// <summary>Used when a status is removed.</summary>
        public float MultiplierStatusRemove { get; private set; } = 1.00f;

        /// <summary>Used when a status has failed to be set (minimum score).</summary>
        public float MultiplierStatusFailed { get; private set; } = 0.50f;

        /// <summary>Used when an action causes INVISIBLE to break.</summary>
        public float MultiplierStatusCancelInvisibility { get; private set; } = 0.50f;

        /// <summary>Used when a status is overwritten (minimum score).</summary>
        public float MultiplierStatusOverwrite { get; private set; } = 0.50f;

        /// <summary>Used when applying the OnRemove functors.</summary>
        public float MultiplierStatusRemoveFunctors { get; private set; } = 0.50f;

        /// <summary>Control stupidity modifier.</summary>
        public float ModifierControlStupidity { get; private set; } = 1.00f;

        /// <summary>Score for losing control of a character.</summary>
        public float MultiplierLoseControl { get; private set; } = 1.00f;

        /// <summary>Score for incapacitating a target.</summary>
        public float MultiplierIncapacitate { get; private set; } = 0.70f;

        /// <summary>Score for knocking down a target.</summary>
        public float MultiplierKnockdown { get; private set; } = 0.50f;

        /// <summary>Score for causing fear.</summary>
        public float MultiplierFear { get; private set; } = 1.00f;

        /// <summary>Score for blinding a target.</summary>
        public float MultiplierBlind { get; private set; } = 0.10f;

        /// <summary>Score for becoming invisible.</summary>
        public float MultiplierInvisible { get; private set; } = 0.20f;

        /// <summary>Score for resurrecting an ally.</summary>
        public float MultiplierResurrect { get; private set; } = 4.00f;

        // ──────────────────────────────────────────────
        //  Spell & Surface
        // ──────────────────────────────────────────────

        /// <summary>Minimum distance before preferring jump spells.</summary>
        public float SpellJumpMinimumDistance { get; private set; } = 6.00f;

        /// <summary>Minimum distance before preferring teleport spells.</summary>
        public float SpellTeleportMinimumDistance { get; private set; } = 6.00f;

        /// <summary>Score when a surface is removed.</summary>
        public float MultiplierSurfaceRemove { get; private set; } = 0.35f;

        /// <summary>Score for destroying interesting items.</summary>
        public float MultiplierDestroyInterestingItem { get; private set; } = 3.25f;

        // ──────────────────────────────────────────────
        //  Resistance & Ability
        // ──────────────────────────────────────────────

        /// <summary>How important resistances are for this character.</summary>
        public float MultiplierResistanceStupidity { get; private set; } = 1.00f;

        /// <summary>How important immunities are for this character.</summary>
        public float MultiplierImmunityStupidity { get; private set; } = 0.00f;

        /// <summary>Importance of boosting main ability.</summary>
        public float MultiplierMainAbility { get; private set; } = 1.00f;

        /// <summary>Importance of boosting secondary ability.</summary>
        public float MultiplierSecondaryAbility { get; private set; } = 0.50f;

        /// <summary>Limits turns calculated for surface/status/summon duration (prevents overvaluing).</summary>
        public float TurnsCap { get; private set; } = 4.0f;

        // ──────────────────────────────────────────────
        //  Boost Scoring
        // ──────────────────────────────────────────────

        /// <summary>Modifier for ArmorClass boost.</summary>
        public float MultiplierBoostAc { get; private set; } = 0.6f;

        /// <summary>Modifier for Ability boost.</summary>
        public float MultiplierBoostAbility { get; private set; } = 0.6f;

        /// <summary>Modifier for AbilityFailedSavingThrow boost.</summary>
        // Custom extension — not in BG3 base.txt
        public float MultiplierBoostAbilityFailedSavingThrow { get; private set; } = 0.085f;

        /// <summary>Modifier for ActionResource boost.</summary>
        public float MultiplierBoostActionResource { get; private set; } = 0.4f;

        /// <summary>Modifier for ActionResourceOverride boost.</summary>
        // Custom extension — not in BG3 base.txt
        public float MultiplierBoostActionResourceOverride { get; private set; } = 0.4f;

        /// <summary>Modifier for ActionResourceMultiplier boost.</summary>
        public float MultiplierBoostActionResourceMultiplier { get; private set; } = 0.25f;

        /// <summary>Modifier for ActionResourceBlock boost.</summary>
        // Custom extension — not in BG3 base.txt
        public float MultiplierBoostActionResourceBlock { get; private set; } = 0.4f;

        /// <summary>Modifier for IgnoreAOO boost.</summary>
        // Custom extension — not in BG3 base.txt
        public float MultiplierBoostIgnoreAoo { get; private set; } = 0.03f;

        /// <summary>Minimum movement left to consider the IgnoreAOO boost.</summary>
        public float BoostIgnoreAooMinMovement { get; private set; } = 2.0f;

        /// <summary>Modifier for IgnoreFallDamage boost.</summary>
        // Custom extension — not in BG3 base.txt
        public float MultiplierBoostIgnoreFallDamage { get; private set; } = -0.04f;

        /// <summary>Modifier for CannotHarmCauseEntity boost.</summary>
        // Custom extension — not in BG3 base.txt
        public float MultiplierBoostCannotHarmCauseEntity { get; private set; } = 0.1f;

        /// <summary>Modifier for CriticalHit Never boost.</summary>
        // Custom extension — not in BG3 base.txt
        public float MultiplierBoostCriticalHitNever { get; private set; } = 0.0125f;

        /// <summary>Modifier for CriticalHit Always boost.</summary>
        // Custom extension — not in BG3 base.txt
        public float MultiplierBoostCriticalHitAlways { get; private set; } = 0.065f;

        /// <summary>Modifier for BlockSpellCast boost.</summary>
        public float MultiplierBoostBlockSpellCast { get; private set; } = 0.25f;

        /// <summary>Modifier for BlockRegainHP boost.</summary>
        public float MultiplierBoostBlockRegainHp { get; private set; } = 0.07f;

        /// <summary>Modifier for HalveWeaponDamage boost.</summary>
        public float MultiplierBoostHalveWeaponDamage { get; private set; } = 0.07f;

        /// <summary>Modifier for WeaponDamage boost.</summary>
        public float MultiplierBoostWeaponDamage { get; private set; } = 0.07f;

        /// <summary>Modifier for BlockVerbalComponent boost.</summary>
        public float MultiplierBoostBlockVerbalComponent { get; private set; } = 0.125f;

        /// <summary>Modifier for BlockSomaticComponent boost.</summary>
        public float MultiplierBoostBlockSomaticComponent { get; private set; } = 0.125f;

        /// <summary>Modifier for SightRange boosts.</summary>
        public float MultiplierBoostSightRange { get; private set; } = 0.08f;

        /// <summary>Modifier for Resistance boosts.</summary>
        public float MultiplierBoostResistance { get; private set; } = 0.15f;

        /// <summary>Modifier for MovementSpeedBonus boosts.</summary>
        public float MultiplierBoostMovement { get; private set; } = 0.05f;

        /// <summary>Modifier for TemporaryHP boosts.</summary>
        public float MultiplierBoostTemporaryHp { get; private set; } = 0.10f;

        /// <summary>Modifier for DamageReduction boosts.</summary>
        public float MultiplierBoostDamageReduction { get; private set; } = 0.10f;

        /// <summary>Modifier for Initiative boosts.</summary>
        public float MultiplierBoostInitiative { get; private set; } = 0.05f;

        /// <summary>Modifier for SavingThrow boosts.</summary>
        public float MultiplierBoostSavingThrow { get; private set; } = 0.10f;

        /// <summary>Modifier for SpellSaveDC / spell resistance boosts.</summary>
        public float MultiplierBoostSpellResistance { get; private set; } = 0.10f;

        // ──────────────────────────────────────────────
        //  Roll Bonus Modifiers
        // ──────────────────────────────────────────────

        /// <summary>MODIFIER_BOOST_ROLLBONUS_ATTACK</summary>
        public float ModifierBoostRollbonusAttack { get; private set; } = 0.05f;

        /// <summary>MODIFIER_BOOST_ROLLBONUS_MELEEWEAPONATTACK</summary>
        public float ModifierBoostRollbonusMeleeweaponattack { get; private set; } = 0.05f;

        /// <summary>MODIFIER_BOOST_ROLLBONUS_RANGEDWEAPONATTACK</summary>
        public float ModifierBoostRollbonusRangedweaponattack { get; private set; } = 0.05f;

        /// <summary>MODIFIER_BOOST_ROLLBONUS_MELEESPELLATTACK</summary>
        public float ModifierBoostRollbonusMeleespellattack { get; private set; } = 0.05f;

        /// <summary>MODIFIER_BOOST_ROLLBONUS_RANGEDSPELLATTACK</summary>
        public float ModifierBoostRollbonusRangedspellattack { get; private set; } = 0.05f;

        /// <summary>MODIFIER_BOOST_ROLLBONUS_MELEEUNARMEDATTACK</summary>
        public float ModifierBoostRollbonusMeleeunarmedattack { get; private set; } = 0.02f;

        /// <summary>MODIFIER_BOOST_ROLLBONUS_RANGEDUNARMEDATTACK</summary>
        public float ModifierBoostRollbonusRangedunarmedattack { get; private set; } = 0.02f;

        /// <summary>MODIFIER_BOOST_ROLLBONUS_SKILL</summary>
        public float ModifierBoostRollbonusSkill { get; private set; } = 0.001f;

        /// <summary>MODIFIER_BOOST_ROLLBONUS_SAVINGTHROW</summary>
        public float ModifierBoostRollbonusSavingthrow { get; private set; } = 0.015f;

        /// <summary>MODIFIER_BOOST_ROLLBONUS_DAMAGE</summary>
        public float ModifierBoostRollbonusDamage { get; private set; } = 0.02f;

        /// <summary>MODIFIER_BOOST_ROLLBONUS_ABILITY</summary>
        public float ModifierBoostRollbonusAbility { get; private set; } = 0.03f;

        /// <summary>MODIFIER_BOOST_ROLLBONUS_MELEEOFFHANDWEAPONATTACK</summary>
        public float ModifierBoostRollbonusMeleeoffhandweaponattack { get; private set; } = 0.035f;

        /// <summary>MODIFIER_BOOST_ROLLBONUS_RANGEDOFFHANDWEAPONATTACK</summary>
        public float ModifierBoostRollbonusRangedoffhandweaponattack { get; private set; } = 0.035f;

        // ──────────────────────────────────────────────
        //  Advantage Scoring
        // ──────────────────────────────────────────────

        /// <summary>Modifier for Advantage on ability checks.</summary>
        public float MultiplierAdvantageAbility { get; private set; } = 0.25f;

        /// <summary>Modifier for Advantage on skill checks.</summary>
        public float MultiplierAdvantageSkill { get; private set; } = 0.20f;

        /// <summary>Modifier for Advantage on attack rolls.</summary>
        public float MultiplierAdvantageAttack { get; private set; } = 0.20f;

        // ──────────────────────────────────────────────
        //  Resource Replenishment Cost
        // ──────────────────────────────────────────────

        /// <summary>Cost multiplier: resource never replenishes.</summary>
        public float MultiplierResourceReplenishTypeNever { get; private set; } = 1.04f;

        /// <summary>Cost multiplier: resource replenishes on combat end.</summary>
        public float MultiplierResourceReplenishTypeCombat { get; private set; } = 1.03f;

        /// <summary>Cost multiplier: resource replenishes on long rest.</summary>
        public float MultiplierResourceReplenishTypeRest { get; private set; } = 1.02f;

        /// <summary>Cost multiplier: resource replenishes on short rest.</summary>
        public float MultiplierResourceReplenishTypeShortRest { get; private set; } = 1.01f;

        /// <summary>Cost multiplier: resource replenishes each turn.</summary>
        public float MultiplierResourceReplenishTypeTurn { get; private set; } = 1.00f;

        // ──────────────────────────────────────────────
        //  Seeking Hidden Characters
        // ──────────────────────────────────────────────

        /// <summary>Scaling the damage score for seeking a hidden character.</summary>
        public float MultiplierSeekHiddenDamage { get; private set; } = 0.02f;

        /// <summary>Minimum score for a seek action.</summary>
        public float ModifierSeekMinimalThreshold { get; private set; } = 0.15f;

        /// <summary>Scaling distance to last known position for seeking.</summary>
        public float MultiplierSeekHiddenDistance { get; private set; } = 0.001f;

        // ──────────────────────────────────────────────
        //  Concentration
        // ──────────────────────────────────────────────

        /// <summary>Score for removing the AI's own concentration.</summary>
        public float ModifierConcentrationRemoveSelf { get; private set; } = 8.0f;

        /// <summary>Score for removing a target's concentration (other than itself).</summary>
        public float ModifierConcentrationRemoveTarget { get; private set; } = 0.0f;

        // ──────────────────────────────────────────────
        //  Combos & Positioning
        // ──────────────────────────────────────────────

        /// <summary>Modifier for combo score interaction.</summary>
        public float MultiplierComboScoreInteraction { get; private set; } = 0.90f;

        /// <summary>Modifier for combo score positioning.</summary>
        public float MultiplierComboScorePositioning { get; private set; } = 0.00f;

        /// <summary>Score for the position being left after jump/teleport.</summary>
        public float MultiplierPositionLeave { get; private set; } = -1.0f;

        /// <summary>How important grounding someone is.</summary>
        public float MultiplierGrounded { get; private set; } = -0.05f;

        /// <summary>Path influence cost for summons.</summary>
        public float MultiplierSummonPathInfluences { get; private set; } = 0.04f;

        /// <summary>Max distance for buff distance falloff (100% → 0%).</summary>
        public float BuffDistMax { get; private set; } = 30.0f;

        /// <summary>Min distance for buff distance falloff (100% below this).</summary>
        public float BuffDistMin { get; private set; } = 10.0f;

        /// <summary>Multiplier for positive secondary surface scores on allies.</summary>
        public float MultiplierPosSecondarySurface { get; private set; } = 0.25f;

        /// <summary>General multiplier for all reflected damage.</summary>
        public float MultiplierReflectDamage { get; private set; } = 0.1f;

        /// <summary>Max consumables a character that lost control can use per turn.</summary>
        public float LoseControlMaxConsumablesPerTurn { get; private set; } = 1f;

        /// <summary>Multiplier for buffs as the first action.</summary>
        public float MultiplierFirstActionBuff { get; private set; } = 1.0f;

        /// <summary>Multiplier for invisibility as the first action.</summary>
        public float MultiplierFirstActionInvisibility { get; private set; } = 0.1f;

        // ──────────────────────────────────────────────
        //  Aura Modifiers
        // ──────────────────────────────────────────────

        /// <summary>Score for being in a positive aura.</summary>
        public float MultiplierPosInAura { get; private set; } = 0.3f;

        /// <summary>Score for own aura effect.</summary>
        public float ModifierOwnAura { get; private set; } = 0.3f;

        /// <summary>Turns cap for aura status evaluation.</summary>
        public float TurnsCapAurastatus { get; private set; } = 2f;

        /// <summary>Score modifier for moving into a dangerous aura.</summary>
        public float ModifierMoveIntoDangerousAura { get; private set; } = 0.5f;

        /// <summary>Score added to positions on climbable ledges (avoids floating feet).</summary>
        public float AvoidClimbableLedges { get; private set; } = 0.15f;

        // ──────────────────────────────────────────────
        //  Weapon Pickup
        // ──────────────────────────────────────────────

        /// <summary>Starting score for picking up weapons. 0 disables entirely.</summary>
        public float WeaponPickupModifier { get; private set; } = 0.3f;

        /// <summary>Max search radius for weapons to pick up.</summary>
        public float WeaponPickupRadius { get; private set; } = 12f;

        /// <summary>Enable preferring ranged weapons above melee (0 = off).</summary>
        public float WeaponPickupPreferRangedEnabled { get; private set; } = 0f;

        /// <summary>Score modifier when weapon is preferred.</summary>
        public float WeaponPickupModifierPreferred { get; private set; } = 1.25f;

        /// <summary>Score modifier when weapon was previously equipped.</summary>
        public float WeaponPickupModifierPreviouslyEquipped { get; private set; } = 1.25f;

        /// <summary>Score modifier based on weapon damage (tiebreaker).</summary>
        public float WeaponPickupModifierDamage { get; private set; } = 0.005f;

        /// <summary>Score modifier when source has no proficiency.</summary>
        public float WeaponPickupModifierNoProficiency { get; private set; } = 0.5f;

        /// <summary>Score modifier when weapon belonged to an allied party member.</summary>
        public float WeaponPickupModifierPartyAlly { get; private set; } = 0.0f;

        // ──────────────────────────────────────────────
        //  Item Usage & Throwing
        // ──────────────────────────────────────────────

        /// <summary>Starting score for using items. 0 disables.</summary>
        public float UseItemModifier { get; private set; } = 0.3f;

        /// <summary>Enable/disable using items from own inventory (1 = on).</summary>
        public float UseInventoryItemsEnabled { get; private set; } = 1f;

        /// <summary>Max search radius for items to use.</summary>
        public float UseItemRadius { get; private set; } = 18.0f;

        /// <summary>Score modifier when source cannot see the item.</summary>
        public float UseItemModifierNoVisibility { get; private set; } = 1.0f;

        /// <summary>Score adjustment when throw spell affects only self.</summary>
        public float MultiplierSelfOnlyThrow { get; private set; } = 0.8f;

        /// <summary>Limit on inventory items eligible to be thrown.</summary>
        public float ThrowInventoryItemLimit { get; private set; } = 100f;

        /// <summary>Fall damage score adjustment for self.</summary>
        public float MultiplierFallDamageSelf { get; private set; } = 1.0f;

        /// <summary>Fall damage score adjustment for enemies.</summary>
        public float MultiplierFallDamageEnemy { get; private set; } = 0.25f;

        /// <summary>Fall damage score adjustment for allies.</summary>
        public float MultiplierFallDamageAlly { get; private set; } = 1.0f;

        // ──────────────────────────────────────────────
        //  Darkness & Movement Surface
        // ──────────────────────────────────────────────

        /// <summary>Score modifier for clear darkness.</summary>
        public float MultiplierDarknessClear { get; private set; } = 0.0f;

        /// <summary>Score modifier for light darkness.</summary>
        public float MultiplierDarknessLight { get; private set; } = 0.0f;

        /// <summary>Score modifier for heavy darkness.</summary>
        public float MultiplierDarknessHeavy { get; private set; } = 0.0f;

        /// <summary>Modifier for surface score during movement.</summary>
        public float MultiplierMovementSurface { get; private set; } = 1.0f;

        /// <summary>Makes AI over/under-estimate chances of hitting (1.0 = accurate).</summary>
        public float ModifierHitChanceStupidity { get; private set; } = 1.0f;

        // ──────────────────────────────────────────────
        //  Disarm
        // ──────────────────────────────────────────────

        /// <summary>Modifier for DisarmWeapon stats functor.</summary>
        public float MultiplierStatsfunctorDisarmweapon { get; private set; } = 0.1f;

        /// <summary>Modifier for DisarmAndStealWeapon stats functor.</summary>
        public float MultiplierStatsfunctorDisarmandstealweapon { get; private set; } = 0.3f;

        // ──────────────────────────────────────────────
        //  Item Count
        // ──────────────────────────────────────────────

        /// <summary>Item count considered 'high' for low/high item amount multipliers.</summary>
        public float ItemHighCount { get; private set; } = 2f;

        // ══════════════════════════════════════════════
        //  Key ↔ Property Mapping
        // ══════════════════════════════════════════════

        private static readonly Dictionary<string, Action<BG3ArchetypeProfile, float>> s_setters =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // General
                ["SCORE_MOD"] = (p, v) => p.ScoreMod = v,
                ["BASE_NEARBY_SCORE"] = (p, v) => p.BaseNearbyScore = v,

                // Damage
                ["MULTIPLIER_DAMAGE_SELF_POS"] = (p, v) => p.MultiplierDamageSelfPos = v,
                ["MULTIPLIER_DAMAGE_SELF_NEG"] = (p, v) => p.MultiplierDamageSelfNeg = v,
                ["MULTIPLIER_DAMAGE_ENEMY_POS"] = (p, v) => p.MultiplierDamageEnemyPos = v,
                ["MULTIPLIER_DAMAGE_ENEMY_NEG"] = (p, v) => p.MultiplierDamageEnemyNeg = v,
                ["MULTIPLIER_DAMAGE_ALLY_POS"] = (p, v) => p.MultiplierDamageAllyPos = v,
                ["MULTIPLIER_DAMAGE_ALLY_NEG"] = (p, v) => p.MultiplierDamageAllyNeg = v,
                ["MULTIPLIER_DAMAGE_NEUTRAL_POS"] = (p, v) => p.MultiplierDamageNeutralPos = v,
                ["MULTIPLIER_DAMAGE_NEUTRAL_NEG"] = (p, v) => p.MultiplierDamageNeutralNeg = v,

                // Heal
                ["MULTIPLIER_HEAL_SELF_POS"] = (p, v) => p.MultiplierHealSelfPos = v,
                ["MULTIPLIER_HEAL_SELF_NEG"] = (p, v) => p.MultiplierHealSelfNeg = v,
                ["MULTIPLIER_HEAL_ENEMY_POS"] = (p, v) => p.MultiplierHealEnemyPos = v,
                ["MULTIPLIER_HEAL_ENEMY_NEG"] = (p, v) => p.MultiplierHealEnemyNeg = v,
                ["MULTIPLIER_HEAL_ALLY_POS"] = (p, v) => p.MultiplierHealAllyPos = v,
                ["MULTIPLIER_HEAL_ALLY_NEG"] = (p, v) => p.MultiplierHealAllyNeg = v,
                ["MULTIPLIER_HEAL_NEUTRAL_POS"] = (p, v) => p.MultiplierHealNeutralPos = v,
                ["MULTIPLIER_HEAL_NEUTRAL_NEG"] = (p, v) => p.MultiplierHealNeutralNeg = v,

                // Dot
                ["MULTIPLIER_DOT_SELF_POS"] = (p, v) => p.MultiplierDotSelfPos = v,
                ["MULTIPLIER_DOT_SELF_NEG"] = (p, v) => p.MultiplierDotSelfNeg = v,
                ["MULTIPLIER_DOT_ENEMY_POS"] = (p, v) => p.MultiplierDotEnemyPos = v,
                ["MULTIPLIER_DOT_ENEMY_NEG"] = (p, v) => p.MultiplierDotEnemyNeg = v,
                ["MULTIPLIER_DOT_ALLY_POS"] = (p, v) => p.MultiplierDotAllyPos = v,
                ["MULTIPLIER_DOT_ALLY_NEG"] = (p, v) => p.MultiplierDotAllyNeg = v,
                ["MULTIPLIER_DOT_NEUTRAL_POS"] = (p, v) => p.MultiplierDotNeutralPos = v,
                ["MULTIPLIER_DOT_NEUTRAL_NEG"] = (p, v) => p.MultiplierDotNeutralNeg = v,

                // Hot
                ["MULTIPLIER_HOT_SELF_POS"] = (p, v) => p.MultiplierHotSelfPos = v,
                ["MULTIPLIER_HOT_SELF_NEG"] = (p, v) => p.MultiplierHotSelfNeg = v,
                ["MULTIPLIER_HOT_ENEMY_POS"] = (p, v) => p.MultiplierHotEnemyPos = v,
                ["MULTIPLIER_HOT_ENEMY_NEG"] = (p, v) => p.MultiplierHotEnemyNeg = v,
                ["MULTIPLIER_HOT_ALLY_POS"] = (p, v) => p.MultiplierHotAllyPos = v,
                ["MULTIPLIER_HOT_ALLY_NEG"] = (p, v) => p.MultiplierHotAllyNeg = v,
                ["MULTIPLIER_HOT_NEUTRAL_POS"] = (p, v) => p.MultiplierHotNeutralPos = v,
                ["MULTIPLIER_HOT_NEUTRAL_NEG"] = (p, v) => p.MultiplierHotNeutralNeg = v,

                // Control
                ["MULTIPLIER_CONTROL_SELF_POS"] = (p, v) => p.MultiplierControlSelfPos = v,
                ["MULTIPLIER_CONTROL_SELF_NEG"] = (p, v) => p.MultiplierControlSelfNeg = v,
                ["MULTIPLIER_CONTROL_ENEMY_POS"] = (p, v) => p.MultiplierControlEnemyPos = v,
                ["MULTIPLIER_CONTROL_ENEMY_NEG"] = (p, v) => p.MultiplierControlEnemyNeg = v,
                ["MULTIPLIER_CONTROL_ALLY_POS"] = (p, v) => p.MultiplierControlAllyPos = v,
                ["MULTIPLIER_CONTROL_ALLY_NEG"] = (p, v) => p.MultiplierControlAllyNeg = v,
                ["MULTIPLIER_CONTROL_NEUTRAL_POS"] = (p, v) => p.MultiplierControlNeutralPos = v,
                ["MULTIPLIER_CONTROL_NEUTRAL_NEG"] = (p, v) => p.MultiplierControlNeutralNeg = v,

                // Boost
                ["MULTIPLIER_BOOST_SELF_POS"] = (p, v) => p.MultiplierBoostSelfPos = v,
                ["MULTIPLIER_BOOST_SELF_NEG"] = (p, v) => p.MultiplierBoostSelfNeg = v,
                ["MULTIPLIER_BOOST_ENEMY_POS"] = (p, v) => p.MultiplierBoostEnemyPos = v,
                ["MULTIPLIER_BOOST_ENEMY_NEG"] = (p, v) => p.MultiplierBoostEnemyNeg = v,
                ["MULTIPLIER_BOOST_ALLY_POS"] = (p, v) => p.MultiplierBoostAllyPos = v,
                ["MULTIPLIER_BOOST_ALLY_NEG"] = (p, v) => p.MultiplierBoostAllyNeg = v,
                ["MULTIPLIER_BOOST_NEUTRAL_POS"] = (p, v) => p.MultiplierBoostNeutralPos = v,
                ["MULTIPLIER_BOOST_NEUTRAL_NEG"] = (p, v) => p.MultiplierBoostNeutralNeg = v,

                // Final Score Multipliers
                ["MULTIPLIER_FREE_ACTION"] = (p, v) => p.MultiplierFreeAction = v,
                ["MULTIPLIER_COOLDOWN_MULTIPLIER"] = (p, v) => p.MultiplierCooldownMultiplier = v,
                ["MULTIPLIER_LOW_ITEM_AMOUNT_MULTIPLIER"] = (p, v) => p.MultiplierLowItemAmountMultiplier = v,
                ["MULTIPLIER_HIGH_ITEM_AMOUNT_MULTIPLIER"] = (p, v) => p.MultiplierHighItemAmountMultiplier = v,
                ["MULTIPLIER_CANNOT_EXECUTE_THIS_TURN"] = (p, v) => p.MultiplierCannotExecuteThisTurn = v,
                ["MULTIPLIER_PLANNED_ACTION_WITH_MOVE_SPELL"] = (p, v) => p.MultiplierPlannedActionWithMoveSpell = v,
                ["MULTIPLIER_ACTION_RESOURCE_COST"] = (p, v) => p.MultiplierActionResourceCost = v,
                ["MULTIPLIER_USABLE_ITEM"] = (p, v) => p.MultiplierUsableItem = v,
                ["ENABLE_MOVEMENT_AVOID_AOO"] = (p, v) => p.EnableMovementAvoidAOO = v,

                // Target Selection
                ["MULTIPLIER_TARGET_MY_ENEMY"] = (p, v) => p.MultiplierTargetMyEnemy = v,
                ["MULTIPLIER_TARGET_MY_HOSTILE"] = (p, v) => p.MultiplierTargetMyHostile = v,
                ["MULTIPLIER_TARGET_SUMMON"] = (p, v) => p.MultiplierTargetSummon = v,
                ["MULTIPLIER_TARGET_AGGRO_MARKED"] = (p, v) => p.MultiplierTargetAggroMarked = v,
                ["MULTIPLIER_TARGET_HOSTILE_COUNT_ONE"] = (p, v) => p.MultiplierTargetHostileCountOne = v,
                ["MULTIPLIER_TARGET_HOSTILE_COUNT_TWO_OR_MORE"] = (p, v) => p.MultiplierTargetHostileCountTwoOrMore = v,
                ["MULTIPLIER_TARGET_IN_SIGHT"] = (p, v) => p.MultiplierTargetInSight = v,
                ["MULTIPLIER_TARGET_INCAPACITATED"] = (p, v) => p.MultiplierTargetIncapacitated = v,
                ["MULTIPLIER_TARGET_KNOCKED_DOWN"] = (p, v) => p.MultiplierTargetKnockedDown = v,
                ["MULTIPLIER_TARGET_PREFERRED"] = (p, v) => p.MultiplierTargetPreferred = v,
                ["MULTIPLIER_TARGET_UNPREFERRED"] = (p, v) => p.MultiplierTargetUnpreferred = v,
                ["MULTIPLIER_TARGET_HEALTH_BIAS"] = (p, v) => p.MultiplierTargetHealthBias = v,
                ["MULTIPLIER_TARGET_ENEMY_DOWNED"] = (p, v) => p.MultiplierTargetEnemyDowned = v,
                ["MULTIPLIER_TARGET_ALLY_DOWNED"] = (p, v) => p.MultiplierTargetAllyDowned = v,
                ["MULTIPLIER_TARGET_NEUTRAL_DOWNED"] = (p, v) => p.MultiplierTargetNeutralDowned = v,

                // End Position
                ["MULTIPLIER_ENDPOS_ALLIES_NEARBY"] = (p, v) => p.MultiplierEndposAlliesNearby = v,
                ["ENDPOS_ALLIES_NEARBY_MIN_DISTANCE"] = (p, v) => p.EndposAlliesNearbyMinDistance = v,
                ["ENDPOS_ALLIES_NEARBY_MAX_DISTANCE"] = (p, v) => p.EndposAlliesNearbyMaxDistance = v,
                ["MULTIPLIER_ENDPOS_ENEMIES_NEARBY"] = (p, v) => p.MultiplierEndposEnemiesNearby = v,
                ["ENDPOS_ENEMIES_NEARBY_MIN_DISTANCE"] = (p, v) => p.EndposEnemiesNearbyMinDistance = v,
                ["ENDPOS_ENEMIES_NEARBY_MAX_DISTANCE"] = (p, v) => p.EndposEnemiesNearbyMaxDistance = v,
                ["MULTIPLIER_ENDPOS_FLANKED"] = (p, v) => p.MultiplierEndposFlanked = v,
                ["MULTIPLIER_ENDPOS_HEIGHT_DIFFERENCE"] = (p, v) => p.MultiplierEndposHeightDifference = v,
                ["MULTIPLIER_ENDPOS_TURNED_INVISIBLE"] = (p, v) => p.MultiplierEndposTurnedInvisible = v,
                ["MULTIPLIER_ENDPOS_NOT_IN_AIHINT"] = (p, v) => p.MultiplierEndposNotInAihint = v,
                ["MULTIPLIER_ENDPOS_NOT_IN_SMOKE"] = (p, v) => p.MultiplierEndposNotInSmoke = v,
                ["MULTIPLIER_ENDPOS_NOT_IN_DANGEROUS_SURFACE"] = (p, v) => p.MultiplierEndposNotInDangerousSurface = v,
                ["DANGEROUS_ITEM_NEARBY"] = (p, v) => p.DangerousItemNearby = v,
                ["MULTIPLIER_ENEMY_HEIGHT_DIFFERENCE"] = (p, v) => p.MultiplierEnemyHeightDifference = v,
                ["ENEMY_HEIGHT_DIFFERENCE_CLAMP"] = (p, v) => p.EnemyHeightDifferenceClamp = v,
                ["ENEMY_HEIGHT_SCORE_RADIUS_XZ"] = (p, v) => p.EnemyHeightScoreRadiusXz = v,
                ["MAX_DISTANCE_TO_CLOSEST_ENEMY"] = (p, v) => p.MaxDistanceToClosestEnemy = v,
                ["MULTIPLIER_NO_ENEMIES_IN_MAX_DISTANCE"] = (p, v) => p.MultiplierNoEnemiesInMaxDistance = v,

                // Fallback
                ["MULTIPLIER_FALLBACK_ALLIES_NEARBY"] = (p, v) => p.MultiplierFallbackAlliesNearby = v,
                ["FALLBACK_ALLIES_NEARBY_MIN_DISTANCE"] = (p, v) => p.FallbackAlliesNearbyMinDistance = v,
                ["FALLBACK_ALLIES_NEARBY_MAX_DISTANCE"] = (p, v) => p.FallbackAlliesNearbyMaxDistance = v,
                ["MULTIPLIER_FALLBACK_ENEMIES_NEARBY"] = (p, v) => p.MultiplierFallbackEnemiesNearby = v,
                ["FALLBACK_ENEMIES_NEARBY_MIN_DISTANCE"] = (p, v) => p.FallbackEnemiesNearbyMinDistance = v,
                ["FALLBACK_ENEMIES_NEARBY_MAX_DISTANCE"] = (p, v) => p.FallbackEnemiesNearbyMaxDistance = v,
                ["FALLBACK_HEIGHT_DIFFERENCE"] = (p, v) => p.FallbackHeightDifference = v,
                ["FALLBACK_JUMP_OVER_WALK_PREFERRED_DISTANCE"] = (p, v) => p.FallbackJumpOverWalkPreferredDistance = v,
                ["FALLBACK_JUMP_BASE_SCORE"] = (p, v) => p.FallbackJumpBaseScore = v,
                ["FALLBACK_MULTIPLIER_VS_FALLBACK_JUMP"] = (p, v) => p.FallbackMultiplierVsFallbackJump = v,
                ["FALLBACK_FUTURE_SCORE"] = (p, v) => p.FallbackFutureScore = v,
                ["FALLBACK_ATTACK_BLOCKER_SCORE"] = (p, v) => p.FallbackAttackBlockerScore = v,

                // General Score Multipliers
                ["MULTIPLIER_SCORE_ON_NEUTRAL"] = (p, v) => p.MultiplierScoreOnNeutral = v,
                ["MULTIPLIER_SCORE_ON_ALLY"] = (p, v) => p.MultiplierScoreOnAlly = v,
                ["MULTIPLIER_SCORE_OUT_OF_COMBAT"] = (p, v) => p.MultiplierScoreOutOfCombat = v,
                ["MAX_HEAL_MULTIPLIER"] = (p, v) => p.MaxHealMultiplier = v,
                ["MAX_HEAL_SELF_MULTIPLIER"] = (p, v) => p.MaxHealSelfMultiplier = v,

                // Kill
                ["MULTIPLIER_KILL_ENEMY"] = (p, v) => p.MultiplierKillEnemy = v,
                ["MULTIPLIER_KILL_ENEMY_SUMMON"] = (p, v) => p.MultiplierKillEnemySummon = v,
                ["MULTIPLIER_KILL_ALLY"] = (p, v) => p.MultiplierKillAlly = v,
                ["MULTIPLIER_KILL_ALLY_SUMMON"] = (p, v) => p.MultiplierKillAllySummon = v,
                ["MULTIPLIER_KILL_TARGET_HEALTH_BIAS"] = (p, v) => p.MultiplierKillTargetHealthBias = v,
                ["INSTAKILL_BASE_SCORE"] = (p, v) => p.InstakillBaseScore = v,
                ["MULTIPLIER_INSTAKILL_TARGET_HEALTH_BIAS"] = (p, v) => p.MultiplierInstakillTargetHealthBias = v,

                // Status
                ["MULTIPLIER_STATUS_REMOVE"] = (p, v) => p.MultiplierStatusRemove = v,
                ["MULTIPLIER_STATUS_FAILED"] = (p, v) => p.MultiplierStatusFailed = v,
                ["MULTIPLIER_STATUS_CANCEL_INVISIBILITY"] = (p, v) => p.MultiplierStatusCancelInvisibility = v,
                ["MULTIPLIER_STATUS_OVERWRITE"] = (p, v) => p.MultiplierStatusOverwrite = v,
                ["MULTIPLIER_STATUS_REMOVE_FUNCTORS"] = (p, v) => p.MultiplierStatusRemoveFunctors = v,
                ["MODIFIER_CONTROL_STUPIDITY"] = (p, v) => p.ModifierControlStupidity = v,
                ["MULTIPLIER_LOSE_CONTROL"] = (p, v) => p.MultiplierLoseControl = v,
                ["MULTIPLIER_INCAPACITATE"] = (p, v) => p.MultiplierIncapacitate = v,
                ["MULTIPLIER_KNOCKDOWN"] = (p, v) => p.MultiplierKnockdown = v,
                ["MULTIPLIER_FEAR"] = (p, v) => p.MultiplierFear = v,
                ["MULTIPLIER_BLIND"] = (p, v) => p.MultiplierBlind = v,
                ["MULTIPLIER_INVISIBLE"] = (p, v) => p.MultiplierInvisible = v,
                ["MULTIPLIER_RESURRECT"] = (p, v) => p.MultiplierResurrect = v,

                // Spell & Surface
                ["SPELL_JUMP_MINIMUM_DISTANCE"] = (p, v) => p.SpellJumpMinimumDistance = v,
                ["SPELL_TELEPORT_MINIMUM_DISTANCE"] = (p, v) => p.SpellTeleportMinimumDistance = v,
                ["MULTIPLIER_SURFACE_REMOVE"] = (p, v) => p.MultiplierSurfaceRemove = v,
                ["MULTIPLIER_DESTROY_INTERESTING_ITEM"] = (p, v) => p.MultiplierDestroyInterestingItem = v,

                // Resistance & Ability
                ["MULTIPLIER_RESISTANCE_STUPIDITY"] = (p, v) => p.MultiplierResistanceStupidity = v,
                ["MULTIPLIER_IMMUNITY_STUPIDITY"] = (p, v) => p.MultiplierImmunityStupidity = v,
                ["MULTIPLIER_MAIN_ABILITY"] = (p, v) => p.MultiplierMainAbility = v,
                ["MULTIPLIER_SECONDARY_ABILITY"] = (p, v) => p.MultiplierSecondaryAbility = v,
                ["TURNS_CAP"] = (p, v) => p.TurnsCap = v,

                // Boost Scoring
                ["MULTIPLIER_BOOST_AC"] = (p, v) => p.MultiplierBoostAc = v,
                ["MULTIPLIER_BOOST_ABILITY"] = (p, v) => p.MultiplierBoostAbility = v,
                ["MULTIPLIER_BOOST_ABILITY_FAILED_SAVING_THROW"] = (p, v) => p.MultiplierBoostAbilityFailedSavingThrow = v,
                ["MULTIPLIER_BOOST_ACTION_RESOURCE"] = (p, v) => p.MultiplierBoostActionResource = v,
                ["MULTIPLIER_BOOST_ACTION_RESOURCE_OVERRIDE"] = (p, v) => p.MultiplierBoostActionResourceOverride = v,
                ["MULTIPLIER_BOOST_ACTION_RESOURCE_MULTIPLIER"] = (p, v) => p.MultiplierBoostActionResourceMultiplier = v,
                ["MULTIPLIER_BOOST_ACTION_RESOURCE_BLOCK"] = (p, v) => p.MultiplierBoostActionResourceBlock = v,
                ["MULTIPLIER_BOOST_IGNORE_AOO"] = (p, v) => p.MultiplierBoostIgnoreAoo = v,
                ["BOOST_IGNORE_AOO_MIN_MOVEMENT"] = (p, v) => p.BoostIgnoreAooMinMovement = v,
                ["MULTIPLIER_BOOST_IGNORE_FALL_DAMAGE"] = (p, v) => p.MultiplierBoostIgnoreFallDamage = v,
                ["MULTIPLIER_BOOST_CANNOT_HARM_CAUSE_ENTITY"] = (p, v) => p.MultiplierBoostCannotHarmCauseEntity = v,
                ["MULTIPLIER_BOOST_CRITICAL_HIT_NEVER"] = (p, v) => p.MultiplierBoostCriticalHitNever = v,
                ["MULTIPLIER_BOOST_CRITICAL_HIT_ALWAYS"] = (p, v) => p.MultiplierBoostCriticalHitAlways = v,
                ["MULTIPLIER_BOOST_BLOCK_SPELL_CAST"] = (p, v) => p.MultiplierBoostBlockSpellCast = v,
                ["MULTIPLIER_BOOST_BLOCK_REGAIN_HP"] = (p, v) => p.MultiplierBoostBlockRegainHp = v,
                ["MULTIPLIER_BOOST_HALVE_WEAPON_DAMAGE"] = (p, v) => p.MultiplierBoostHalveWeaponDamage = v,
                ["MULTIPLIER_BOOST_WEAPON_DAMAGE"] = (p, v) => p.MultiplierBoostWeaponDamage = v,
                ["MULTIPLIER_BOOST_BLOCK_VERBAL_COMPONENT"] = (p, v) => p.MultiplierBoostBlockVerbalComponent = v,
                ["MULTIPLIER_BOOST_BLOCK_SOMATIC_COMPONENT"] = (p, v) => p.MultiplierBoostBlockSomaticComponent = v,
                ["MULTIPLIER_BOOST_SIGHT_RANGE"] = (p, v) => p.MultiplierBoostSightRange = v,
                ["MULTIPLIER_BOOST_RESISTANCE"] = (p, v) => p.MultiplierBoostResistance = v,
                ["MULTIPLIER_BOOST_MOVEMENT"] = (p, v) => p.MultiplierBoostMovement = v,
                ["MULTIPLIER_BOOST_TEMPORARY_HP"] = (p, v) => p.MultiplierBoostTemporaryHp = v,
                ["MULTIPLIER_BOOST_DAMAGE_REDUCTION"] = (p, v) => p.MultiplierBoostDamageReduction = v,
                ["MULTIPLIER_BOOST_INITIATIVE"] = (p, v) => p.MultiplierBoostInitiative = v,
                ["MULTIPLIER_BOOST_SAVING_THROW"] = (p, v) => p.MultiplierBoostSavingThrow = v,
                ["MULTIPLIER_BOOST_SPELL_RESISTANCE"] = (p, v) => p.MultiplierBoostSpellResistance = v,

                // Roll Bonus
                ["MODIFIER_BOOST_ROLLBONUS_ATTACK"] = (p, v) => p.ModifierBoostRollbonusAttack = v,
                ["MODIFIER_BOOST_ROLLBONUS_MELEEWEAPONATTACK"] = (p, v) => p.ModifierBoostRollbonusMeleeweaponattack = v,
                ["MODIFIER_BOOST_ROLLBONUS_RANGEDWEAPONATTACK"] = (p, v) => p.ModifierBoostRollbonusRangedweaponattack = v,
                ["MODIFIER_BOOST_ROLLBONUS_MELEESPELLATTACK"] = (p, v) => p.ModifierBoostRollbonusMeleespellattack = v,
                ["MODIFIER_BOOST_ROLLBONUS_RANGEDSPELLATTACK"] = (p, v) => p.ModifierBoostRollbonusRangedspellattack = v,
                ["MODIFIER_BOOST_ROLLBONUS_MELEEUNARMEDATTACK"] = (p, v) => p.ModifierBoostRollbonusMeleeunarmedattack = v,
                ["MODIFIER_BOOST_ROLLBONUS_RANGEDUNARMEDATTACK"] = (p, v) => p.ModifierBoostRollbonusRangedunarmedattack = v,
                ["MODIFIER_BOOST_ROLLBONUS_SKILL"] = (p, v) => p.ModifierBoostRollbonusSkill = v,
                ["MODIFIER_BOOST_ROLLBONUS_SAVINGTHROW"] = (p, v) => p.ModifierBoostRollbonusSavingthrow = v,
                ["MODIFIER_BOOST_ROLLBONUS_DAMAGE"] = (p, v) => p.ModifierBoostRollbonusDamage = v,
                ["MODIFIER_BOOST_ROLLBONUS_ABILITY"] = (p, v) => p.ModifierBoostRollbonusAbility = v,
                ["MODIFIER_BOOST_ROLLBONUS_MELEEOFFHANDWEAPONATTACK"] = (p, v) => p.ModifierBoostRollbonusMeleeoffhandweaponattack = v,
                ["MODIFIER_BOOST_ROLLBONUS_RANGEDOFFHANDWEAPONATTACK"] = (p, v) => p.ModifierBoostRollbonusRangedoffhandweaponattack = v,

                // Advantage
                ["MULTIPLIER_ADVANTAGE_ABILITY"] = (p, v) => p.MultiplierAdvantageAbility = v,
                ["MULTIPLIER_ADVANTAGE_SKILL"] = (p, v) => p.MultiplierAdvantageSkill = v,
                ["MULTIPLIER_ADVANTAGE_ATTACK"] = (p, v) => p.MultiplierAdvantageAttack = v,

                // Resource Replenishment
                ["MULTIPLIER_RESOURCE_REPLENISH_TYPE_NEVER"] = (p, v) => p.MultiplierResourceReplenishTypeNever = v,
                ["MULTIPLIER_RESOURCE_REPLENISH_TYPE_COMBAT"] = (p, v) => p.MultiplierResourceReplenishTypeCombat = v,
                ["MULTIPLIER_RESOURCE_REPLENISH_TYPE_REST"] = (p, v) => p.MultiplierResourceReplenishTypeRest = v,
                ["MULTIPLIER_RESOURCE_REPLENISH_TYPE_SHORT_REST"] = (p, v) => p.MultiplierResourceReplenishTypeShortRest = v,
                ["MULTIPLIER_RESOURCE_REPLENISH_TYPE_TURN"] = (p, v) => p.MultiplierResourceReplenishTypeTurn = v,

                // Seeking Hidden
                ["MULTIPLIER_SEEK_HIDDEN_DAMAGE"] = (p, v) => p.MultiplierSeekHiddenDamage = v,
                ["MODIFIER_SEEK_MINIMAL_THRESHOLD"] = (p, v) => p.ModifierSeekMinimalThreshold = v,
                ["MULTIPLIER_SEEK_HIDDEN_DISTANCE"] = (p, v) => p.MultiplierSeekHiddenDistance = v,

                // Concentration
                ["MODIFIER_CONCENTRATION_REMOVE_SELF"] = (p, v) => p.ModifierConcentrationRemoveSelf = v,
                ["MODIFIER_CONCENTRATION_REMOVE_TARGET"] = (p, v) => p.ModifierConcentrationRemoveTarget = v,

                // Combos & Positioning
                ["MULTIPLIER_COMBO_SCORE_INTERACTION"] = (p, v) => p.MultiplierComboScoreInteraction = v,
                ["MULTIPLIER_COMBO_SCORE_POSITIONING"] = (p, v) => p.MultiplierComboScorePositioning = v,
                ["MULTIPLIER_POSITION_LEAVE"] = (p, v) => p.MultiplierPositionLeave = v,
                ["MULTIPLIER_GROUNDED"] = (p, v) => p.MultiplierGrounded = v,
                ["MULTIPLIER_SUMMON_PATH_INFLUENCES"] = (p, v) => p.MultiplierSummonPathInfluences = v,
                ["BUFF_DIST_MAX"] = (p, v) => p.BuffDistMax = v,
                ["BUFF_DIST_MIN"] = (p, v) => p.BuffDistMin = v,
                ["MULTIPLIER_POS_SECONDARY_SURFACE"] = (p, v) => p.MultiplierPosSecondarySurface = v,
                ["MULTIPLIER_REFLECT_DAMAGE"] = (p, v) => p.MultiplierReflectDamage = v,
                ["LOSE_CONTROL_MAX_CONSUMABLES_PER_TURN"] = (p, v) => p.LoseControlMaxConsumablesPerTurn = v,
                ["MULTIPLIER_FIRST_ACTION_BUFF"] = (p, v) => p.MultiplierFirstActionBuff = v,
                ["MULTIPLIER_FIRST_ACTION_INVISIBILITY"] = (p, v) => p.MultiplierFirstActionInvisibility = v,

                // Aura
                ["MULTIPLIER_POS_IN_AURA"] = (p, v) => p.MultiplierPosInAura = v,
                ["MODIFIER_OWN_AURA"] = (p, v) => p.ModifierOwnAura = v,
                ["TURNS_CAP_AURASTATUS"] = (p, v) => p.TurnsCapAurastatus = v,
                ["MODIFIER_MOVE_INTO_DANGEROUS_AURA"] = (p, v) => p.ModifierMoveIntoDangerousAura = v,
                ["AVOID_CLIMBABLE_LEDGES"] = (p, v) => p.AvoidClimbableLedges = v,

                // Weapon Pickup
                ["WEAPON_PICKUP_MODIFIER"] = (p, v) => p.WeaponPickupModifier = v,
                ["WEAPON_PICKUP_RADIUS"] = (p, v) => p.WeaponPickupRadius = v,
                ["WEAPON_PICKUP_PREFER_RANGED_ENABLED"] = (p, v) => p.WeaponPickupPreferRangedEnabled = v,
                ["WEAPON_PICKUP_MODIFIER_PREFERRED"] = (p, v) => p.WeaponPickupModifierPreferred = v,
                ["WEAPON_PICKUP_MODIFIER_PREVIOUSLY_EQUIPPED"] = (p, v) => p.WeaponPickupModifierPreviouslyEquipped = v,
                ["WEAPON_PICKUP_MODIFIER_DAMAGE"] = (p, v) => p.WeaponPickupModifierDamage = v,
                ["WEAPON_PICKUP_MODIFIER_NO_PROFICIENCY"] = (p, v) => p.WeaponPickupModifierNoProficiency = v,
                ["WEAPON_PICKUP_MODIFIER_PARTY_ALLY"] = (p, v) => p.WeaponPickupModifierPartyAlly = v,

                // Item Usage & Throwing
                ["USE_ITEM_MODIFIER"] = (p, v) => p.UseItemModifier = v,
                ["USE_INVENTORY_ITEMS_ENABLED"] = (p, v) => p.UseInventoryItemsEnabled = v,
                ["USE_ITEM_RADIUS"] = (p, v) => p.UseItemRadius = v,
                ["USE_ITEM_MODIFIER_NO_VISIBILITY"] = (p, v) => p.UseItemModifierNoVisibility = v,
                ["MULTIPLIER_SELF_ONLY_THROW"] = (p, v) => p.MultiplierSelfOnlyThrow = v,
                ["THROW_INVENTORY_ITEM_LIMIT"] = (p, v) => p.ThrowInventoryItemLimit = v,
                ["MULTIPLIER_FALL_DAMAGE_SELF"] = (p, v) => p.MultiplierFallDamageSelf = v,
                ["MULTIPLIER_FALL_DAMAGE_ENEMY"] = (p, v) => p.MultiplierFallDamageEnemy = v,
                ["MULTIPLIER_FALL_DAMAGE_ALLY"] = (p, v) => p.MultiplierFallDamageAlly = v,

                // Darkness & Movement Surface
                ["MULTIPLIER_DARKNESS_CLEAR"] = (p, v) => p.MultiplierDarknessClear = v,
                ["MULTIPLIER_DARKNESS_LIGHT"] = (p, v) => p.MultiplierDarknessLight = v,
                ["MULTIPLIER_DARKNESS_HEAVY"] = (p, v) => p.MultiplierDarknessHeavy = v,
                ["MULTIPLIER_MOVEMENT_SURFACE"] = (p, v) => p.MultiplierMovementSurface = v,
                ["MODIFIER_HIT_CHANCE_STUPIDITY"] = (p, v) => p.ModifierHitChanceStupidity = v,

                // Disarm
                ["MULTIPLIER_STATSFUNCTOR_DISARMWEAPON"] = (p, v) => p.MultiplierStatsfunctorDisarmweapon = v,
                ["MULTIPLIER_STATSFUNCTOR_DISARMANDSTEALWEAPON"] = (p, v) => p.MultiplierStatsfunctorDisarmandstealweapon = v,

                // Item Count
                ["ITEM_HIGH_COUNT"] = (p, v) => p.ItemHighCount = v,
            };

        private static readonly Dictionary<string, Func<BG3ArchetypeProfile, float>> s_getters =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // General
                ["SCORE_MOD"] = p => p.ScoreMod,
                ["BASE_NEARBY_SCORE"] = p => p.BaseNearbyScore,

                // Damage
                ["MULTIPLIER_DAMAGE_SELF_POS"] = p => p.MultiplierDamageSelfPos,
                ["MULTIPLIER_DAMAGE_SELF_NEG"] = p => p.MultiplierDamageSelfNeg,
                ["MULTIPLIER_DAMAGE_ENEMY_POS"] = p => p.MultiplierDamageEnemyPos,
                ["MULTIPLIER_DAMAGE_ENEMY_NEG"] = p => p.MultiplierDamageEnemyNeg,
                ["MULTIPLIER_DAMAGE_ALLY_POS"] = p => p.MultiplierDamageAllyPos,
                ["MULTIPLIER_DAMAGE_ALLY_NEG"] = p => p.MultiplierDamageAllyNeg,
                ["MULTIPLIER_DAMAGE_NEUTRAL_POS"] = p => p.MultiplierDamageNeutralPos,
                ["MULTIPLIER_DAMAGE_NEUTRAL_NEG"] = p => p.MultiplierDamageNeutralNeg,

                // Heal
                ["MULTIPLIER_HEAL_SELF_POS"] = p => p.MultiplierHealSelfPos,
                ["MULTIPLIER_HEAL_SELF_NEG"] = p => p.MultiplierHealSelfNeg,
                ["MULTIPLIER_HEAL_ENEMY_POS"] = p => p.MultiplierHealEnemyPos,
                ["MULTIPLIER_HEAL_ENEMY_NEG"] = p => p.MultiplierHealEnemyNeg,
                ["MULTIPLIER_HEAL_ALLY_POS"] = p => p.MultiplierHealAllyPos,
                ["MULTIPLIER_HEAL_ALLY_NEG"] = p => p.MultiplierHealAllyNeg,
                ["MULTIPLIER_HEAL_NEUTRAL_POS"] = p => p.MultiplierHealNeutralPos,
                ["MULTIPLIER_HEAL_NEUTRAL_NEG"] = p => p.MultiplierHealNeutralNeg,

                // Dot
                ["MULTIPLIER_DOT_SELF_POS"] = p => p.MultiplierDotSelfPos,
                ["MULTIPLIER_DOT_SELF_NEG"] = p => p.MultiplierDotSelfNeg,
                ["MULTIPLIER_DOT_ENEMY_POS"] = p => p.MultiplierDotEnemyPos,
                ["MULTIPLIER_DOT_ENEMY_NEG"] = p => p.MultiplierDotEnemyNeg,
                ["MULTIPLIER_DOT_ALLY_POS"] = p => p.MultiplierDotAllyPos,
                ["MULTIPLIER_DOT_ALLY_NEG"] = p => p.MultiplierDotAllyNeg,
                ["MULTIPLIER_DOT_NEUTRAL_POS"] = p => p.MultiplierDotNeutralPos,
                ["MULTIPLIER_DOT_NEUTRAL_NEG"] = p => p.MultiplierDotNeutralNeg,

                // Hot
                ["MULTIPLIER_HOT_SELF_POS"] = p => p.MultiplierHotSelfPos,
                ["MULTIPLIER_HOT_SELF_NEG"] = p => p.MultiplierHotSelfNeg,
                ["MULTIPLIER_HOT_ENEMY_POS"] = p => p.MultiplierHotEnemyPos,
                ["MULTIPLIER_HOT_ENEMY_NEG"] = p => p.MultiplierHotEnemyNeg,
                ["MULTIPLIER_HOT_ALLY_POS"] = p => p.MultiplierHotAllyPos,
                ["MULTIPLIER_HOT_ALLY_NEG"] = p => p.MultiplierHotAllyNeg,
                ["MULTIPLIER_HOT_NEUTRAL_POS"] = p => p.MultiplierHotNeutralPos,
                ["MULTIPLIER_HOT_NEUTRAL_NEG"] = p => p.MultiplierHotNeutralNeg,

                // Control
                ["MULTIPLIER_CONTROL_SELF_POS"] = p => p.MultiplierControlSelfPos,
                ["MULTIPLIER_CONTROL_SELF_NEG"] = p => p.MultiplierControlSelfNeg,
                ["MULTIPLIER_CONTROL_ENEMY_POS"] = p => p.MultiplierControlEnemyPos,
                ["MULTIPLIER_CONTROL_ENEMY_NEG"] = p => p.MultiplierControlEnemyNeg,
                ["MULTIPLIER_CONTROL_ALLY_POS"] = p => p.MultiplierControlAllyPos,
                ["MULTIPLIER_CONTROL_ALLY_NEG"] = p => p.MultiplierControlAllyNeg,
                ["MULTIPLIER_CONTROL_NEUTRAL_POS"] = p => p.MultiplierControlNeutralPos,
                ["MULTIPLIER_CONTROL_NEUTRAL_NEG"] = p => p.MultiplierControlNeutralNeg,

                // Boost
                ["MULTIPLIER_BOOST_SELF_POS"] = p => p.MultiplierBoostSelfPos,
                ["MULTIPLIER_BOOST_SELF_NEG"] = p => p.MultiplierBoostSelfNeg,
                ["MULTIPLIER_BOOST_ENEMY_POS"] = p => p.MultiplierBoostEnemyPos,
                ["MULTIPLIER_BOOST_ENEMY_NEG"] = p => p.MultiplierBoostEnemyNeg,
                ["MULTIPLIER_BOOST_ALLY_POS"] = p => p.MultiplierBoostAllyPos,
                ["MULTIPLIER_BOOST_ALLY_NEG"] = p => p.MultiplierBoostAllyNeg,
                ["MULTIPLIER_BOOST_NEUTRAL_POS"] = p => p.MultiplierBoostNeutralPos,
                ["MULTIPLIER_BOOST_NEUTRAL_NEG"] = p => p.MultiplierBoostNeutralNeg,

                // Final Score Multipliers
                ["MULTIPLIER_FREE_ACTION"] = p => p.MultiplierFreeAction,
                ["MULTIPLIER_COOLDOWN_MULTIPLIER"] = p => p.MultiplierCooldownMultiplier,
                ["MULTIPLIER_LOW_ITEM_AMOUNT_MULTIPLIER"] = p => p.MultiplierLowItemAmountMultiplier,
                ["MULTIPLIER_HIGH_ITEM_AMOUNT_MULTIPLIER"] = p => p.MultiplierHighItemAmountMultiplier,
                ["MULTIPLIER_CANNOT_EXECUTE_THIS_TURN"] = p => p.MultiplierCannotExecuteThisTurn,
                ["MULTIPLIER_PLANNED_ACTION_WITH_MOVE_SPELL"] = p => p.MultiplierPlannedActionWithMoveSpell,
                ["MULTIPLIER_ACTION_RESOURCE_COST"] = p => p.MultiplierActionResourceCost,
                ["MULTIPLIER_USABLE_ITEM"] = p => p.MultiplierUsableItem,
                ["ENABLE_MOVEMENT_AVOID_AOO"] = p => p.EnableMovementAvoidAOO,

                // Target Selection
                ["MULTIPLIER_TARGET_MY_ENEMY"] = p => p.MultiplierTargetMyEnemy,
                ["MULTIPLIER_TARGET_MY_HOSTILE"] = p => p.MultiplierTargetMyHostile,
                ["MULTIPLIER_TARGET_SUMMON"] = p => p.MultiplierTargetSummon,
                ["MULTIPLIER_TARGET_AGGRO_MARKED"] = p => p.MultiplierTargetAggroMarked,
                ["MULTIPLIER_TARGET_HOSTILE_COUNT_ONE"] = p => p.MultiplierTargetHostileCountOne,
                ["MULTIPLIER_TARGET_HOSTILE_COUNT_TWO_OR_MORE"] = p => p.MultiplierTargetHostileCountTwoOrMore,
                ["MULTIPLIER_TARGET_IN_SIGHT"] = p => p.MultiplierTargetInSight,
                ["MULTIPLIER_TARGET_INCAPACITATED"] = p => p.MultiplierTargetIncapacitated,
                ["MULTIPLIER_TARGET_KNOCKED_DOWN"] = p => p.MultiplierTargetKnockedDown,
                ["MULTIPLIER_TARGET_PREFERRED"] = p => p.MultiplierTargetPreferred,
                ["MULTIPLIER_TARGET_UNPREFERRED"] = p => p.MultiplierTargetUnpreferred,
                ["MULTIPLIER_TARGET_HEALTH_BIAS"] = p => p.MultiplierTargetHealthBias,
                ["MULTIPLIER_TARGET_ENEMY_DOWNED"] = p => p.MultiplierTargetEnemyDowned,
                ["MULTIPLIER_TARGET_ALLY_DOWNED"] = p => p.MultiplierTargetAllyDowned,
                ["MULTIPLIER_TARGET_NEUTRAL_DOWNED"] = p => p.MultiplierTargetNeutralDowned,

                // End Position
                ["MULTIPLIER_ENDPOS_ALLIES_NEARBY"] = p => p.MultiplierEndposAlliesNearby,
                ["ENDPOS_ALLIES_NEARBY_MIN_DISTANCE"] = p => p.EndposAlliesNearbyMinDistance,
                ["ENDPOS_ALLIES_NEARBY_MAX_DISTANCE"] = p => p.EndposAlliesNearbyMaxDistance,
                ["MULTIPLIER_ENDPOS_ENEMIES_NEARBY"] = p => p.MultiplierEndposEnemiesNearby,
                ["ENDPOS_ENEMIES_NEARBY_MIN_DISTANCE"] = p => p.EndposEnemiesNearbyMinDistance,
                ["ENDPOS_ENEMIES_NEARBY_MAX_DISTANCE"] = p => p.EndposEnemiesNearbyMaxDistance,
                ["MULTIPLIER_ENDPOS_FLANKED"] = p => p.MultiplierEndposFlanked,
                ["MULTIPLIER_ENDPOS_HEIGHT_DIFFERENCE"] = p => p.MultiplierEndposHeightDifference,
                ["MULTIPLIER_ENDPOS_TURNED_INVISIBLE"] = p => p.MultiplierEndposTurnedInvisible,
                ["MULTIPLIER_ENDPOS_NOT_IN_AIHINT"] = p => p.MultiplierEndposNotInAihint,
                ["MULTIPLIER_ENDPOS_NOT_IN_SMOKE"] = p => p.MultiplierEndposNotInSmoke,
                ["MULTIPLIER_ENDPOS_NOT_IN_DANGEROUS_SURFACE"] = p => p.MultiplierEndposNotInDangerousSurface,
                ["DANGEROUS_ITEM_NEARBY"] = p => p.DangerousItemNearby,
                ["MULTIPLIER_ENEMY_HEIGHT_DIFFERENCE"] = p => p.MultiplierEnemyHeightDifference,
                ["ENEMY_HEIGHT_DIFFERENCE_CLAMP"] = p => p.EnemyHeightDifferenceClamp,
                ["ENEMY_HEIGHT_SCORE_RADIUS_XZ"] = p => p.EnemyHeightScoreRadiusXz,
                ["MAX_DISTANCE_TO_CLOSEST_ENEMY"] = p => p.MaxDistanceToClosestEnemy,
                ["MULTIPLIER_NO_ENEMIES_IN_MAX_DISTANCE"] = p => p.MultiplierNoEnemiesInMaxDistance,

                // Fallback
                ["MULTIPLIER_FALLBACK_ALLIES_NEARBY"] = p => p.MultiplierFallbackAlliesNearby,
                ["FALLBACK_ALLIES_NEARBY_MIN_DISTANCE"] = p => p.FallbackAlliesNearbyMinDistance,
                ["FALLBACK_ALLIES_NEARBY_MAX_DISTANCE"] = p => p.FallbackAlliesNearbyMaxDistance,
                ["MULTIPLIER_FALLBACK_ENEMIES_NEARBY"] = p => p.MultiplierFallbackEnemiesNearby,
                ["FALLBACK_ENEMIES_NEARBY_MIN_DISTANCE"] = p => p.FallbackEnemiesNearbyMinDistance,
                ["FALLBACK_ENEMIES_NEARBY_MAX_DISTANCE"] = p => p.FallbackEnemiesNearbyMaxDistance,
                ["FALLBACK_HEIGHT_DIFFERENCE"] = p => p.FallbackHeightDifference,
                ["FALLBACK_JUMP_OVER_WALK_PREFERRED_DISTANCE"] = p => p.FallbackJumpOverWalkPreferredDistance,
                ["FALLBACK_JUMP_BASE_SCORE"] = p => p.FallbackJumpBaseScore,
                ["FALLBACK_MULTIPLIER_VS_FALLBACK_JUMP"] = p => p.FallbackMultiplierVsFallbackJump,
                ["FALLBACK_FUTURE_SCORE"] = p => p.FallbackFutureScore,
                ["FALLBACK_ATTACK_BLOCKER_SCORE"] = p => p.FallbackAttackBlockerScore,

                // General Score Multipliers
                ["MULTIPLIER_SCORE_ON_NEUTRAL"] = p => p.MultiplierScoreOnNeutral,
                ["MULTIPLIER_SCORE_ON_ALLY"] = p => p.MultiplierScoreOnAlly,
                ["MULTIPLIER_SCORE_OUT_OF_COMBAT"] = p => p.MultiplierScoreOutOfCombat,
                ["MAX_HEAL_MULTIPLIER"] = p => p.MaxHealMultiplier,
                ["MAX_HEAL_SELF_MULTIPLIER"] = p => p.MaxHealSelfMultiplier,

                // Kill
                ["MULTIPLIER_KILL_ENEMY"] = p => p.MultiplierKillEnemy,
                ["MULTIPLIER_KILL_ENEMY_SUMMON"] = p => p.MultiplierKillEnemySummon,
                ["MULTIPLIER_KILL_ALLY"] = p => p.MultiplierKillAlly,
                ["MULTIPLIER_KILL_ALLY_SUMMON"] = p => p.MultiplierKillAllySummon,
                ["MULTIPLIER_KILL_TARGET_HEALTH_BIAS"] = p => p.MultiplierKillTargetHealthBias,
                ["INSTAKILL_BASE_SCORE"] = p => p.InstakillBaseScore,
                ["MULTIPLIER_INSTAKILL_TARGET_HEALTH_BIAS"] = p => p.MultiplierInstakillTargetHealthBias,

                // Status
                ["MULTIPLIER_STATUS_REMOVE"] = p => p.MultiplierStatusRemove,
                ["MULTIPLIER_STATUS_FAILED"] = p => p.MultiplierStatusFailed,
                ["MULTIPLIER_STATUS_CANCEL_INVISIBILITY"] = p => p.MultiplierStatusCancelInvisibility,
                ["MULTIPLIER_STATUS_OVERWRITE"] = p => p.MultiplierStatusOverwrite,
                ["MULTIPLIER_STATUS_REMOVE_FUNCTORS"] = p => p.MultiplierStatusRemoveFunctors,
                ["MODIFIER_CONTROL_STUPIDITY"] = p => p.ModifierControlStupidity,
                ["MULTIPLIER_LOSE_CONTROL"] = p => p.MultiplierLoseControl,
                ["MULTIPLIER_INCAPACITATE"] = p => p.MultiplierIncapacitate,
                ["MULTIPLIER_KNOCKDOWN"] = p => p.MultiplierKnockdown,
                ["MULTIPLIER_FEAR"] = p => p.MultiplierFear,
                ["MULTIPLIER_BLIND"] = p => p.MultiplierBlind,
                ["MULTIPLIER_INVISIBLE"] = p => p.MultiplierInvisible,
                ["MULTIPLIER_RESURRECT"] = p => p.MultiplierResurrect,

                // Spell & Surface
                ["SPELL_JUMP_MINIMUM_DISTANCE"] = p => p.SpellJumpMinimumDistance,
                ["SPELL_TELEPORT_MINIMUM_DISTANCE"] = p => p.SpellTeleportMinimumDistance,
                ["MULTIPLIER_SURFACE_REMOVE"] = p => p.MultiplierSurfaceRemove,
                ["MULTIPLIER_DESTROY_INTERESTING_ITEM"] = p => p.MultiplierDestroyInterestingItem,

                // Resistance & Ability
                ["MULTIPLIER_RESISTANCE_STUPIDITY"] = p => p.MultiplierResistanceStupidity,
                ["MULTIPLIER_IMMUNITY_STUPIDITY"] = p => p.MultiplierImmunityStupidity,
                ["MULTIPLIER_MAIN_ABILITY"] = p => p.MultiplierMainAbility,
                ["MULTIPLIER_SECONDARY_ABILITY"] = p => p.MultiplierSecondaryAbility,
                ["TURNS_CAP"] = p => p.TurnsCap,

                // Boost Scoring
                ["MULTIPLIER_BOOST_AC"] = p => p.MultiplierBoostAc,
                ["MULTIPLIER_BOOST_ABILITY"] = p => p.MultiplierBoostAbility,
                ["MULTIPLIER_BOOST_ABILITY_FAILED_SAVING_THROW"] = p => p.MultiplierBoostAbilityFailedSavingThrow,
                ["MULTIPLIER_BOOST_ACTION_RESOURCE"] = p => p.MultiplierBoostActionResource,
                ["MULTIPLIER_BOOST_ACTION_RESOURCE_OVERRIDE"] = p => p.MultiplierBoostActionResourceOverride,
                ["MULTIPLIER_BOOST_ACTION_RESOURCE_MULTIPLIER"] = p => p.MultiplierBoostActionResourceMultiplier,
                ["MULTIPLIER_BOOST_ACTION_RESOURCE_BLOCK"] = p => p.MultiplierBoostActionResourceBlock,
                ["MULTIPLIER_BOOST_IGNORE_AOO"] = p => p.MultiplierBoostIgnoreAoo,
                ["BOOST_IGNORE_AOO_MIN_MOVEMENT"] = p => p.BoostIgnoreAooMinMovement,
                ["MULTIPLIER_BOOST_IGNORE_FALL_DAMAGE"] = p => p.MultiplierBoostIgnoreFallDamage,
                ["MULTIPLIER_BOOST_CANNOT_HARM_CAUSE_ENTITY"] = p => p.MultiplierBoostCannotHarmCauseEntity,
                ["MULTIPLIER_BOOST_CRITICAL_HIT_NEVER"] = p => p.MultiplierBoostCriticalHitNever,
                ["MULTIPLIER_BOOST_CRITICAL_HIT_ALWAYS"] = p => p.MultiplierBoostCriticalHitAlways,
                ["MULTIPLIER_BOOST_BLOCK_SPELL_CAST"] = p => p.MultiplierBoostBlockSpellCast,
                ["MULTIPLIER_BOOST_BLOCK_REGAIN_HP"] = p => p.MultiplierBoostBlockRegainHp,
                ["MULTIPLIER_BOOST_HALVE_WEAPON_DAMAGE"] = p => p.MultiplierBoostHalveWeaponDamage,
                ["MULTIPLIER_BOOST_WEAPON_DAMAGE"] = p => p.MultiplierBoostWeaponDamage,
                ["MULTIPLIER_BOOST_BLOCK_VERBAL_COMPONENT"] = p => p.MultiplierBoostBlockVerbalComponent,
                ["MULTIPLIER_BOOST_BLOCK_SOMATIC_COMPONENT"] = p => p.MultiplierBoostBlockSomaticComponent,
                ["MULTIPLIER_BOOST_SIGHT_RANGE"] = p => p.MultiplierBoostSightRange,
                ["MULTIPLIER_BOOST_RESISTANCE"] = p => p.MultiplierBoostResistance,
                ["MULTIPLIER_BOOST_MOVEMENT"] = p => p.MultiplierBoostMovement,
                ["MULTIPLIER_BOOST_TEMPORARY_HP"] = p => p.MultiplierBoostTemporaryHp,
                ["MULTIPLIER_BOOST_DAMAGE_REDUCTION"] = p => p.MultiplierBoostDamageReduction,
                ["MULTIPLIER_BOOST_INITIATIVE"] = p => p.MultiplierBoostInitiative,
                ["MULTIPLIER_BOOST_SAVING_THROW"] = p => p.MultiplierBoostSavingThrow,
                ["MULTIPLIER_BOOST_SPELL_RESISTANCE"] = p => p.MultiplierBoostSpellResistance,

                // Roll Bonus
                ["MODIFIER_BOOST_ROLLBONUS_ATTACK"] = p => p.ModifierBoostRollbonusAttack,
                ["MODIFIER_BOOST_ROLLBONUS_MELEEWEAPONATTACK"] = p => p.ModifierBoostRollbonusMeleeweaponattack,
                ["MODIFIER_BOOST_ROLLBONUS_RANGEDWEAPONATTACK"] = p => p.ModifierBoostRollbonusRangedweaponattack,
                ["MODIFIER_BOOST_ROLLBONUS_MELEESPELLATTACK"] = p => p.ModifierBoostRollbonusMeleespellattack,
                ["MODIFIER_BOOST_ROLLBONUS_RANGEDSPELLATTACK"] = p => p.ModifierBoostRollbonusRangedspellattack,
                ["MODIFIER_BOOST_ROLLBONUS_MELEEUNARMEDATTACK"] = p => p.ModifierBoostRollbonusMeleeunarmedattack,
                ["MODIFIER_BOOST_ROLLBONUS_RANGEDUNARMEDATTACK"] = p => p.ModifierBoostRollbonusRangedunarmedattack,
                ["MODIFIER_BOOST_ROLLBONUS_SKILL"] = p => p.ModifierBoostRollbonusSkill,
                ["MODIFIER_BOOST_ROLLBONUS_SAVINGTHROW"] = p => p.ModifierBoostRollbonusSavingthrow,
                ["MODIFIER_BOOST_ROLLBONUS_DAMAGE"] = p => p.ModifierBoostRollbonusDamage,
                ["MODIFIER_BOOST_ROLLBONUS_ABILITY"] = p => p.ModifierBoostRollbonusAbility,
                ["MODIFIER_BOOST_ROLLBONUS_MELEEOFFHANDWEAPONATTACK"] = p => p.ModifierBoostRollbonusMeleeoffhandweaponattack,
                ["MODIFIER_BOOST_ROLLBONUS_RANGEDOFFHANDWEAPONATTACK"] = p => p.ModifierBoostRollbonusRangedoffhandweaponattack,

                // Advantage
                ["MULTIPLIER_ADVANTAGE_ABILITY"] = p => p.MultiplierAdvantageAbility,
                ["MULTIPLIER_ADVANTAGE_SKILL"] = p => p.MultiplierAdvantageSkill,
                ["MULTIPLIER_ADVANTAGE_ATTACK"] = p => p.MultiplierAdvantageAttack,

                // Resource Replenishment
                ["MULTIPLIER_RESOURCE_REPLENISH_TYPE_NEVER"] = p => p.MultiplierResourceReplenishTypeNever,
                ["MULTIPLIER_RESOURCE_REPLENISH_TYPE_COMBAT"] = p => p.MultiplierResourceReplenishTypeCombat,
                ["MULTIPLIER_RESOURCE_REPLENISH_TYPE_REST"] = p => p.MultiplierResourceReplenishTypeRest,
                ["MULTIPLIER_RESOURCE_REPLENISH_TYPE_SHORT_REST"] = p => p.MultiplierResourceReplenishTypeShortRest,
                ["MULTIPLIER_RESOURCE_REPLENISH_TYPE_TURN"] = p => p.MultiplierResourceReplenishTypeTurn,

                // Seeking Hidden
                ["MULTIPLIER_SEEK_HIDDEN_DAMAGE"] = p => p.MultiplierSeekHiddenDamage,
                ["MODIFIER_SEEK_MINIMAL_THRESHOLD"] = p => p.ModifierSeekMinimalThreshold,
                ["MULTIPLIER_SEEK_HIDDEN_DISTANCE"] = p => p.MultiplierSeekHiddenDistance,

                // Concentration
                ["MODIFIER_CONCENTRATION_REMOVE_SELF"] = p => p.ModifierConcentrationRemoveSelf,
                ["MODIFIER_CONCENTRATION_REMOVE_TARGET"] = p => p.ModifierConcentrationRemoveTarget,

                // Combos & Positioning
                ["MULTIPLIER_COMBO_SCORE_INTERACTION"] = p => p.MultiplierComboScoreInteraction,
                ["MULTIPLIER_COMBO_SCORE_POSITIONING"] = p => p.MultiplierComboScorePositioning,
                ["MULTIPLIER_POSITION_LEAVE"] = p => p.MultiplierPositionLeave,
                ["MULTIPLIER_GROUNDED"] = p => p.MultiplierGrounded,
                ["MULTIPLIER_SUMMON_PATH_INFLUENCES"] = p => p.MultiplierSummonPathInfluences,
                ["BUFF_DIST_MAX"] = p => p.BuffDistMax,
                ["BUFF_DIST_MIN"] = p => p.BuffDistMin,
                ["MULTIPLIER_POS_SECONDARY_SURFACE"] = p => p.MultiplierPosSecondarySurface,
                ["MULTIPLIER_REFLECT_DAMAGE"] = p => p.MultiplierReflectDamage,
                ["LOSE_CONTROL_MAX_CONSUMABLES_PER_TURN"] = p => p.LoseControlMaxConsumablesPerTurn,
                ["MULTIPLIER_FIRST_ACTION_BUFF"] = p => p.MultiplierFirstActionBuff,
                ["MULTIPLIER_FIRST_ACTION_INVISIBILITY"] = p => p.MultiplierFirstActionInvisibility,

                // Aura
                ["MULTIPLIER_POS_IN_AURA"] = p => p.MultiplierPosInAura,
                ["MODIFIER_OWN_AURA"] = p => p.ModifierOwnAura,
                ["TURNS_CAP_AURASTATUS"] = p => p.TurnsCapAurastatus,
                ["MODIFIER_MOVE_INTO_DANGEROUS_AURA"] = p => p.ModifierMoveIntoDangerousAura,
                ["AVOID_CLIMBABLE_LEDGES"] = p => p.AvoidClimbableLedges,

                // Weapon Pickup
                ["WEAPON_PICKUP_MODIFIER"] = p => p.WeaponPickupModifier,
                ["WEAPON_PICKUP_RADIUS"] = p => p.WeaponPickupRadius,
                ["WEAPON_PICKUP_PREFER_RANGED_ENABLED"] = p => p.WeaponPickupPreferRangedEnabled,
                ["WEAPON_PICKUP_MODIFIER_PREFERRED"] = p => p.WeaponPickupModifierPreferred,
                ["WEAPON_PICKUP_MODIFIER_PREVIOUSLY_EQUIPPED"] = p => p.WeaponPickupModifierPreviouslyEquipped,
                ["WEAPON_PICKUP_MODIFIER_DAMAGE"] = p => p.WeaponPickupModifierDamage,
                ["WEAPON_PICKUP_MODIFIER_NO_PROFICIENCY"] = p => p.WeaponPickupModifierNoProficiency,
                ["WEAPON_PICKUP_MODIFIER_PARTY_ALLY"] = p => p.WeaponPickupModifierPartyAlly,

                // Item Usage & Throwing
                ["USE_ITEM_MODIFIER"] = p => p.UseItemModifier,
                ["USE_INVENTORY_ITEMS_ENABLED"] = p => p.UseInventoryItemsEnabled,
                ["USE_ITEM_RADIUS"] = p => p.UseItemRadius,
                ["USE_ITEM_MODIFIER_NO_VISIBILITY"] = p => p.UseItemModifierNoVisibility,
                ["MULTIPLIER_SELF_ONLY_THROW"] = p => p.MultiplierSelfOnlyThrow,
                ["THROW_INVENTORY_ITEM_LIMIT"] = p => p.ThrowInventoryItemLimit,
                ["MULTIPLIER_FALL_DAMAGE_SELF"] = p => p.MultiplierFallDamageSelf,
                ["MULTIPLIER_FALL_DAMAGE_ENEMY"] = p => p.MultiplierFallDamageEnemy,
                ["MULTIPLIER_FALL_DAMAGE_ALLY"] = p => p.MultiplierFallDamageAlly,

                // Darkness & Movement Surface
                ["MULTIPLIER_DARKNESS_CLEAR"] = p => p.MultiplierDarknessClear,
                ["MULTIPLIER_DARKNESS_LIGHT"] = p => p.MultiplierDarknessLight,
                ["MULTIPLIER_DARKNESS_HEAVY"] = p => p.MultiplierDarknessHeavy,
                ["MULTIPLIER_MOVEMENT_SURFACE"] = p => p.MultiplierMovementSurface,
                ["MODIFIER_HIT_CHANCE_STUPIDITY"] = p => p.ModifierHitChanceStupidity,

                // Disarm
                ["MULTIPLIER_STATSFUNCTOR_DISARMWEAPON"] = p => p.MultiplierStatsfunctorDisarmweapon,
                ["MULTIPLIER_STATSFUNCTOR_DISARMANDSTEALWEAPON"] = p => p.MultiplierStatsfunctorDisarmandstealweapon,

                // Item Count
                ["ITEM_HIGH_COUNT"] = p => p.ItemHighCount,
            };

        // ══════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════

        /// <summary>
        /// Applies overrides from resolved BG3 archetype settings.
        /// Only keys present in <paramref name="settings"/> are applied; missing keys keep their defaults.
        /// Key lookup is case-insensitive.
        /// </summary>
        public void LoadFromSettings(IReadOnlyDictionary<string, float> settings)
        {
            if (settings == null) return;

            foreach (var kvp in settings)
            {
                if (s_setters.TryGetValue(kvp.Key, out var setter))
                {
                    setter(this, kvp.Value);
                }
            }
        }

        /// <summary>
        /// Reverse lookup: given a BG3 key string (e.g. "MULTIPLIER_DAMAGE_ENEMY_POS"),
        /// returns the corresponding property value.
        /// </summary>
        /// <param name="bg3Key">The BG3 parameter key (case-insensitive).</param>
        /// <returns>The current value, or 0f if the key is not recognized.</returns>
        public float GetValue(string bg3Key)
        {
            if (bg3Key != null && s_getters.TryGetValue(bg3Key, out var getter))
            {
                return getter(this);
            }
            return 0f;
        }

        /// <summary>
        /// Returns true if the given BG3 key is a recognized parameter name.
        /// </summary>
        public static bool IsKnownKey(string bg3Key)
        {
            return bg3Key != null && s_getters.ContainsKey(bg3Key);
        }

        /// <summary>
        /// Total number of mapped BG3 parameters.
        /// </summary>
        public static int ParameterCount => s_getters.Count;
    }
}
