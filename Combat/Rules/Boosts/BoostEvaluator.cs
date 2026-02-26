using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules.Conditions;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Rules.Boosts
{
    /// <summary>
    /// Evaluates active boosts to answer questions about a combatant's current state.
    /// 
    /// Core responsibilities:
    /// - Check for advantage/disadvantage on rolls
    /// - Calculate AC bonuses
    /// - Determine damage resistance levels
    /// - Check status immunities
    /// - Calculate damage bonuses
    /// 
    /// This is the primary interface for combat systems to query boost effects.
    /// </summary>
    public static class BoostEvaluator
    {
        /// <summary>
        /// Checks if a combatant has advantage on a specific type of roll.
        /// </summary>
        /// <param name="combatant">The combatant to check</param>
        /// <param name="rollType">The type of roll being made</param>
        /// <param name="ability">Optional: specific ability for ability checks/saving throws</param>
        /// <param name="target">Optional: target of the action (for conditional boosts)</param>
        /// <returns>True if the combatant has advantage on this roll</returns>
        public static bool HasAdvantage(Combatant combatant, RollType rollType, AbilityType? ability = null, Combatant target = null)
        {
            if (combatant == null)
                return false;

            var query = new BoostQuery(BoostType.Advantage, rollType, ability, actor: combatant, target: target);
            var relevantBoosts = QueryBoosts(combatant, query);

            return relevantBoosts.Any();
        }

        /// <summary>
        /// Checks if a combatant has advantage on a specific type of roll,
        /// including conditional boosts evaluated against the given context.
        /// </summary>
        /// <param name="combatant">The combatant to check</param>
        /// <param name="rollType">The type of roll being made</param>
        /// <param name="context">Combat context for evaluating conditional boosts</param>
        /// <param name="ability">Optional: specific ability for ability checks/saving throws</param>
        /// <returns>True if the combatant has advantage on this roll</returns>
        public static bool HasAdvantage(Combatant combatant, RollType rollType, ConditionContext context, AbilityType? ability = null)
        {
            if (combatant == null)
                return false;

            var query = new BoostQuery(BoostType.Advantage, rollType, ability, actor: combatant, target: context?.Target);
            var relevantBoosts = QueryBoosts(combatant, query, context);

            return relevantBoosts.Any();
        }

        /// <summary>
        /// Checks if a combatant has disadvantage on a specific type of roll.
        /// </summary>
        /// <param name="combatant">The combatant to check</param>
        /// <param name="rollType">The type of roll being made</param>
        /// <param name="ability">Optional: specific ability for ability checks/saving throws</param>
        /// <param name="target">Optional: target of the action (for conditional boosts)</param>
        /// <returns>True if the combatant has disadvantage on this roll</returns>
        public static bool HasDisadvantage(Combatant combatant, RollType rollType, AbilityType? ability = null, Combatant target = null)
        {
            if (combatant == null)
                return false;

            var query = new BoostQuery(BoostType.Disadvantage, rollType, ability, actor: combatant, target: target);
            var relevantBoosts = QueryBoosts(combatant, query);

            return relevantBoosts.Any();
        }

        /// <summary>
        /// Checks if a combatant has disadvantage on a specific type of roll,
        /// including conditional boosts evaluated against the given context.
        /// </summary>
        /// <param name="combatant">The combatant to check</param>
        /// <param name="rollType">The type of roll being made</param>
        /// <param name="context">Combat context for evaluating conditional boosts</param>
        /// <param name="ability">Optional: specific ability for ability checks/saving throws</param>
        /// <returns>True if the combatant has disadvantage on this roll</returns>
        public static bool HasDisadvantage(Combatant combatant, RollType rollType, ConditionContext context, AbilityType? ability = null)
        {
            if (combatant == null)
                return false;

            var query = new BoostQuery(BoostType.Disadvantage, rollType, ability, actor: combatant, target: context?.Target);
            var relevantBoosts = QueryBoosts(combatant, query, context);

            return relevantBoosts.Any();
        }

        /// <summary>
        /// Checks if the combatant has an AC override formula active (e.g. Unarmored Defense).
        /// Returns (true, bestAC) if any ACOverrideFormula boost applies, otherwise (false, 0).
        /// Syntax: ACOverrideFormula(baseAC, addDexterity, ...additionalAbilities)
        /// </summary>
        public static (bool HasOverride, int OverrideAC) GetACOverride(Combatant combatant)
        {
            if (combatant == null) return (false, 0);

            var query = new BoostQuery(BoostType.ACOverrideFormula);
            var boosts = QueryBoosts(combatant, query);
            if (boosts.Count == 0) return (false, 0);

            int best = int.MinValue;
            foreach (var boost in boosts)
            {
                int ac = boost.Definition.GetIntParameter(0, 10);
                // Parameter 1 is BG3-internal metadata flag — not an implicit DEX add.
                // All modifying abilities (including Dexterity) are listed explicitly from parameter 2 onward.
                for (int i = 2; i < (boost.Definition.Parameters?.Length ?? 0); i++)
                {
                    var abilityName = boost.Definition.GetStringParameter(i, "");
                    if (!string.IsNullOrEmpty(abilityName) &&
                        Enum.TryParse<AbilityType>(abilityName, true, out var extraAbility))
                        ac += combatant.GetAbilityModifier(extraAbility);
                }

                if (ac > best) best = ac;
            }
            return (true, best);
        }

        /// <summary>
        /// Calculates the total AC bonus from all active boosts.
        /// </summary>
        /// <param name="combatant">The combatant to check</param>
        /// <returns>Total AC bonus (can be negative)</returns>
        public static int GetACBonus(Combatant combatant)
        {
            if (combatant == null)
                return 0;

            var query = new BoostQuery(BoostType.AC);
            var relevantBoosts = QueryBoosts(combatant, query);

            int total = 0;
            foreach (var boost in relevantBoosts)
            {
                total += boost.Definition.GetIntParameter(0, 0);
            }

            return total;
        }

        /// <summary>
        /// Calculates the total AC bonus from all active boosts,
        /// including conditional AC boosts evaluated against the given context.
        /// </summary>
        /// <param name="combatant">The combatant to check</param>
        /// <param name="context">Combat context for evaluating conditional boosts</param>
        /// <returns>Total AC bonus (can be negative)</returns>
        public static int GetACBonus(Combatant combatant, ConditionContext context)
        {
            if (combatant == null)
                return 0;

            var query = new BoostQuery(BoostType.AC, actor: combatant);
            var relevantBoosts = QueryBoosts(combatant, query, context);

            int total = 0;
            foreach (var boost in relevantBoosts)
            {
                total += boost.Definition.GetIntParameter(0, 0);
            }

            return total;
        }

        /// <summary>
        /// Calculates the total damage bonus for a specific damage type.
        /// </summary>
        /// <param name="combatant">The combatant to check</param>
        /// <param name="damageType">The type of damage being dealt</param>
        /// <param name="target">Optional: target of the attack (for conditional boosts)</param>
        /// <returns>Total damage bonus</returns>
        public static int GetDamageBonus(Combatant combatant, DamageType damageType, Combatant target = null)
        {
            if (combatant == null)
                return 0;

            var query = new BoostQuery(BoostType.DamageBonus, damageType: damageType, actor: combatant, target: target);
            var relevantBoosts = QueryBoosts(combatant, query);

            int total = 0;
            foreach (var boost in relevantBoosts)
            {
                // DamageBonus(value, DamageType)
                // Check if this boost's damage type matches
                var boostDamageType = boost.Definition.GetStringParameter(1, "");
                if (string.IsNullOrEmpty(boostDamageType) || 
                    boostDamageType.Equals(damageType.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    total += boost.Definition.GetIntParameter(0, 0);
                }
            }

            return total;
        }

        /// <summary>
        /// Determines the resistance level for a specific damage type.
        /// If multiple resistances apply, the most protective one wins (Immune > Resistant > Normal > Vulnerable).
        /// </summary>
        /// <param name="combatant">The combatant to check</param>
        /// <param name="damageType">The type of damage being taken</param>
        /// <returns>The effective resistance level</returns>
        public static ResistanceLevel GetResistanceLevel(Combatant combatant, DamageType damageType)
        {
            if (combatant == null)
                return ResistanceLevel.Normal;

            var query = new BoostQuery(BoostType.Resistance, damageType: damageType, actor: combatant);
            var relevantBoosts = QueryBoosts(combatant, query);

            ResistanceLevel bestResistance = ResistanceLevel.Normal;

            foreach (var boost in relevantBoosts)
            {
                // Resistance(DamageType, ResistanceLevel)
                var boostDamageType = boost.Definition.GetStringParameter(0, "");
                var boostResistanceLevel = boost.Definition.GetStringParameter(1, "Normal");

                if (!boostDamageType.Equals(damageType.ToString(), StringComparison.OrdinalIgnoreCase))
                    continue;

                if (Enum.TryParse<ResistanceLevel>(boostResistanceLevel, ignoreCase: true, out var level))
                {
                    // Immune is best, then Resistant, then Normal, then Vulnerable
                    if (level == ResistanceLevel.Immune)
                        return ResistanceLevel.Immune; // Short-circuit - can't get better than immune

                    if (level == ResistanceLevel.Resistant && bestResistance != ResistanceLevel.Immune)
                        bestResistance = ResistanceLevel.Resistant;

                    if (level == ResistanceLevel.Vulnerable && bestResistance == ResistanceLevel.Normal)
                        bestResistance = ResistanceLevel.Vulnerable;
                }
            }

            return bestResistance;
        }

        /// <summary>
        /// Gets all status IDs that the combatant is immune to.
        /// </summary>
        /// <param name="combatant">The combatant to check</param>
        /// <returns>Set of status IDs the combatant is immune to</returns>
        public static HashSet<string> GetStatusImmunities(Combatant combatant)
        {
            var immunities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (combatant == null)
                return immunities;

            var query = new BoostQuery(BoostType.StatusImmunity);
            var relevantBoosts = QueryBoosts(combatant, query);

            foreach (var boost in relevantBoosts)
            {
                // StatusImmunity(StatusID)
                var statusId = boost.Definition.GetStringParameter(0, "");
                if (!string.IsNullOrEmpty(statusId))
                {
                    immunities.Add(statusId);
                }
            }

            return immunities;
        }

        // ============================================================
        // TIER 2: ACTION ECONOMY EVALUATORS
        // ============================================================

        /// <summary>
        /// Checks if a specific action resource is blocked for the combatant.
        /// For example, an Entangled creature may have ActionResourceBlock(Movement).
        /// </summary>
        /// <param name="combatant">The combatant to check.</param>
        /// <param name="resourceName">The resource name to check (e.g., "Movement", "BonusAction", "Reaction").</param>
        /// <returns>True if the resource is blocked by any active boost.</returns>
        public static bool IsResourceBlocked(Combatant combatant, string resourceName)
        {
            if (combatant == null || string.IsNullOrEmpty(resourceName))
                return false;

            var query = new BoostQuery(BoostType.ActionResourceBlock);
            var relevantBoosts = QueryBoosts(combatant, query);

            foreach (var boost in relevantBoosts)
            {
                // ActionResourceBlock(ResourceType)
                var blockedResource = boost.Definition.GetStringParameter(0, "");
                if (blockedResource.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the effective movement multiplier from ActionResourceMultiplier boosts.
        /// A multiplier of 2 means Dash (doubles movement). Multipliers stack multiplicatively.
        /// </summary>
        /// <param name="combatant">The combatant to check.</param>
        /// <returns>The combined movement multiplier (1.0 = normal, 2.0 = Dash, etc.).</returns>
        public static float GetMovementMultiplier(Combatant combatant)
        {
            if (combatant == null)
                return 1.0f;

            var query = new BoostQuery(BoostType.ActionResourceMultiplier);
            var relevantBoosts = QueryBoosts(combatant, query);

            float multiplier = 1.0f;
            foreach (var boost in relevantBoosts)
            {
                // ActionResourceMultiplier(ResourceType, multiplier, base)
                var resourceType = boost.Definition.GetStringParameter(0, "");
                if (resourceType.Equals("Movement", StringComparison.OrdinalIgnoreCase))
                {
                    var mult = boost.Definition.GetFloatParameter(1, 1.0f);
                    multiplier *= mult;
                }
            }

            return multiplier;
        }

        /// <summary>
        /// Gets the flat resource modifier for a given action resource.
        /// Used for boosts like ActionResource(Movement, 30, 0) that add extra movement.
        /// </summary>
        /// <param name="combatant">The combatant to check.</param>
        /// <param name="resourceName">The resource name (e.g., "Movement", "SpellSlot").</param>
        /// <returns>Total additive modifier to the resource.</returns>
        public static int GetResourceModifier(Combatant combatant, string resourceName)
        {
            if (combatant == null || string.IsNullOrEmpty(resourceName))
                return 0;

            var query = new BoostQuery(BoostType.ActionResource);
            var relevantBoosts = QueryBoosts(combatant, query);

            int total = 0;
            foreach (var boost in relevantBoosts)
            {
                // ActionResource(ResourceType, value, base)
                var resourceType = boost.Definition.GetStringParameter(0, "");
                if (resourceType.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
                {
                    total += boost.Definition.GetIntParameter(1, 0);
                }
            }

            return total;
        }

        // ============================================================
        // TIER 3: ADVANCED MECHANIC EVALUATORS
        // ============================================================

        /// <summary>
        /// Gets all spell IDs unlocked by active UnlockSpell boosts.
        /// </summary>
        /// <param name="combatant">The combatant to check.</param>
        /// <returns>List of unlocked spell IDs.</returns>
        public static List<string> GetUnlockedSpells(Combatant combatant)
        {
            var spells = new List<string>();
            if (combatant == null)
                return spells;

            var query = new BoostQuery(BoostType.UnlockSpell);
            var relevantBoosts = QueryBoosts(combatant, query);

            foreach (var boost in relevantBoosts)
            {
                // UnlockSpell(SpellID)
                var spellId = boost.Definition.GetStringParameter(0, "");
                if (!string.IsNullOrEmpty(spellId))
                    spells.Add(spellId);
            }

            return spells;
        }

        /// <summary>
        /// Gets all interrupt/reaction IDs unlocked by active UnlockInterrupt boosts.
        /// </summary>
        /// <param name="combatant">The combatant to check.</param>
        /// <returns>List of unlocked interrupt IDs.</returns>
        public static List<string> GetUnlockedInterrupts(Combatant combatant)
        {
            var interrupts = new List<string>();
            if (combatant == null)
                return interrupts;

            var query = new BoostQuery(BoostType.UnlockInterrupt);
            var relevantBoosts = QueryBoosts(combatant, query);

            foreach (var boost in relevantBoosts)
            {
                // UnlockInterrupt(InterruptID)
                var interruptId = boost.Definition.GetStringParameter(0, "");
                if (!string.IsNullOrEmpty(interruptId))
                    interrupts.Add(interruptId);
            }

            return interrupts;
        }

        /// <summary>
        /// Gets the flat integer attack roll modifier from RollBonus boosts for a specific
        /// weapon attack type name (e.g. "MeleeWeaponAttack" for GWM -5 penalty,
        /// "RangedWeaponAttack" for Sharpshooter -5 penalty).
        /// Returns a negative number for a penalty, positive for a bonus.
        /// </summary>
        /// <param name="combatant">The combatant to check.</param>
        /// <param name="attackTypeName">The attack type string as used in RollBonus parameters (e.g. "MeleeWeaponAttack").</param>
        /// <returns>Total flat integer roll bonus (sum of all matching boosts whose value parses as int).</returns>
        public static int GetAttackRollPenalty(Combatant combatant, string attackTypeName, ConditionContext context = null)
        {
            if (combatant == null || string.IsNullOrEmpty(attackTypeName))
                return 0;

            var query = new BoostQuery(BoostType.RollBonus);
            var relevantBoosts = QueryBoosts(combatant, query, context);

            int total = 0;
            foreach (var boost in relevantBoosts)
            {
                var boostRollType = boost.Definition.GetStringParameter(0, "");
                if (!boostRollType.Equals(attackTypeName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var valueStr = boost.Definition.GetStringParameter(1, "");
                if (int.TryParse(valueStr, out int val))
                    total += val;
            }

            return total;
        }

        /// <summary>
        /// Gets the total roll bonus dice formulas for a specific roll type.
        /// Returns the raw dice expressions (e.g., "1d4" from Bless) so the caller can
        /// evaluate them with DiceRoller. Multiple bonuses are returned separately.
        /// </summary>
        /// <param name="combatant">The combatant to check.</param>
        /// <param name="rollType">The roll type (Attack, SavingThrow, etc.).</param>
        /// <returns>List of dice formula strings (e.g., ["1d4", "1d6"]).</returns>
        public static List<string> GetRollBonusDice(Combatant combatant, RollType rollType)
        {
            var formulas = new List<string>();
            if (combatant == null)
                return formulas;

            var query = new BoostQuery(BoostType.RollBonus);
            var relevantBoosts = QueryBoosts(combatant, query);

            foreach (var boost in relevantBoosts)
            {
                // RollBonus(RollType, dice) — e.g., RollBonus(Attack, 1d4)
                var boostRollType = boost.Definition.GetStringParameter(0, "");

                // Match the roll type: exact match, or "Attack" matches AttackRoll
                bool matches = boostRollType.Equals(rollType.ToString(), StringComparison.OrdinalIgnoreCase);
                if (!matches && rollType == Boosts.RollType.AttackRoll)
                    matches = boostRollType.Equals("Attack", StringComparison.OrdinalIgnoreCase);
                // Weapon-attack-specific roll bonuses (e.g. Archery +2 stored as RangedWeaponAttack)
                if (!matches && rollType == Boosts.RollType.AttackRoll)
                    matches = boostRollType.Equals("RangedWeaponAttack", StringComparison.OrdinalIgnoreCase);
                if (!matches && rollType == Boosts.RollType.AttackRoll)
                    matches = boostRollType.Equals("MeleeWeaponAttack", StringComparison.OrdinalIgnoreCase);
                if (!matches && rollType == Boosts.RollType.SavingThrow)
                    matches = boostRollType.Equals("SavingThrow", StringComparison.OrdinalIgnoreCase);
                if (!matches && rollType == Boosts.RollType.DeathSave)
                    matches = boostRollType.Equals("DeathSavingThrow", StringComparison.OrdinalIgnoreCase);

                if (matches)
                {
                    var dice = boost.Definition.GetStringParameter(1, "");
                    if (!string.IsNullOrEmpty(dice))
                        formulas.Add(dice);
                }
            }

            return formulas;
        }

        /// <summary>
        /// Checks if the combatant has proficiency in a specific type and name.
        /// Queries ProficiencyBonus boosts (e.g., ProficiencyBonus(Skill, Perception)).
        /// </summary>
        /// <param name="combatant">The combatant to check.</param>
        /// <param name="proficiencyType">The proficiency type (e.g., "Skill", "Weapon", "Armor", "SavingThrow").</param>
        /// <param name="proficiencyName">The specific proficiency name (e.g., "Perception", "Longsword", "HeavyArmor").</param>
        /// <returns>True if the combatant has the specified proficiency.</returns>
        public static bool HasProficiency(Combatant combatant, string proficiencyType, string proficiencyName)
        {
            if (combatant == null || string.IsNullOrEmpty(proficiencyType) || string.IsNullOrEmpty(proficiencyName))
                return false;

            var query = new BoostQuery(BoostType.ProficiencyBonus);
            var relevantBoosts = QueryBoosts(combatant, query);

            foreach (var boost in relevantBoosts)
            {
                // ProficiencyBonus(Type, Name)
                var type = boost.Definition.GetStringParameter(0, "");
                var name = boost.Definition.GetStringParameter(1, "");

                if (type.Equals(proficiencyType, StringComparison.OrdinalIgnoreCase) &&
                    name.Equals(proficiencyName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the critical hit modifier for the combatant.
        /// Returns whether the crit range is expanded, attacks auto-crit, or crits are disabled.
        /// </summary>
        /// <param name="combatant">The combatant to check.</param>
        /// <returns>
        /// A <see cref="CriticalHitInfo"/> describing the active crit modifiers:
        /// AutoCrit is true if any CriticalHit(_, Success) boost is active;
        /// NeverCrit is true if any CriticalHit(_, Never) boost is active.
        /// </returns>
        public static CriticalHitInfo GetCriticalHitModifier(Combatant combatant)
        {
            var result = new CriticalHitInfo();
            if (combatant == null)
                return result;

            var query = new BoostQuery(BoostType.CriticalHit);
            var relevantBoosts = QueryBoosts(combatant, query);

            foreach (var boost in relevantBoosts)
            {
                // CriticalHit(RollType, Success|Never)
                var mode = boost.Definition.GetStringParameter(1, "");
                if (mode.Equals("Success", StringComparison.OrdinalIgnoreCase))
                    result.AutoCrit = true;
                else if (mode.Equals("Never", StringComparison.OrdinalIgnoreCase))
                    result.NeverCrit = true;
            }

            return result;
        }

        /// <summary>
        /// Checks if the combatant has a specific attribute flag
        /// (e.g., Grounded, Invulnerable, FreezeImmunity).
        /// </summary>
        /// <param name="combatant">The combatant to check.</param>
        /// <param name="attributeName">The attribute name to look for (e.g., "Grounded", "Invulnerable").</param>
        /// <returns>True if the combatant has the specified attribute.</returns>
        public static bool HasAttribute(Combatant combatant, string attributeName)
        {
            if (combatant == null || string.IsNullOrEmpty(attributeName))
                return false;

            var query = new BoostQuery(BoostType.Attribute);
            var relevantBoosts = QueryBoosts(combatant, query);

            foreach (var boost in relevantBoosts)
            {
                // Attribute(AttributeName)
                var attr = boost.Definition.GetStringParameter(0, "");
                if (attr.Equals(attributeName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        // ============================================================
        // TIER 4: EXTENDED MECHANIC EVALUATORS
        // ============================================================

        /// <summary>
        /// Gets additional weapon damage dice from WeaponDamage boosts.
        /// Returns tuples of (diceExpression, damageType).
        /// </summary>
        public static List<(string DiceExpression, string DamageType)> GetWeaponDamageBonus(Combatant combatant)
        {
            var result = new List<(string, string)>();
            if (combatant == null) return result;

            var query = new BoostQuery(BoostType.WeaponDamage);
            var relevantBoosts = QueryBoosts(combatant, query);

            foreach (var boost in relevantBoosts)
            {
                // WeaponDamage(dice, DamageType)
                var dice = boost.Definition.GetStringParameter(0, "");
                var dmgType = boost.Definition.GetStringParameter(1, "");
                if (!string.IsNullOrEmpty(dice))
                    result.Add((dice, dmgType));
            }

            return result;
        }

        /// <summary>
        /// Gets the ability score modifier from Ability boosts.
        /// Returns the total modifier for the specified ability.
        /// </summary>
        public static int GetAbilityModifier(Combatant combatant, AbilityType ability)
        {
            if (combatant == null) return 0;

            var query = new BoostQuery(BoostType.Ability);
            var relevantBoosts = QueryBoosts(combatant, query);

            int total = 0;
            foreach (var boost in relevantBoosts)
            {
                // Ability(AbilityName, modifier)
                var abilityName = boost.Definition.GetStringParameter(0, "");
                if (abilityName.Equals(ability.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    total += boost.Definition.GetIntParameter(1, 0);
                }
            }

            return total;
        }

        /// <summary>
        /// Gets the spell save DC modifier from SpellSaveDC boosts.
        /// </summary>
        public static int GetSpellSaveDCModifier(Combatant combatant)
        {
            if (combatant == null) return 0;

            var query = new BoostQuery(BoostType.SpellSaveDC);
            var relevantBoosts = QueryBoosts(combatant, query);

            int total = 0;
            foreach (var boost in relevantBoosts)
            {
                total += boost.Definition.GetIntParameter(0, 0);
            }

            return total;
        }

        /// <summary>
        /// Gets the max HP increase from IncreaseMaxHP boosts.
        /// </summary>
        public static int GetMaxHPIncrease(Combatant combatant)
        {
            if (combatant == null) return 0;

            var query = new BoostQuery(BoostType.IncreaseMaxHP);
            var relevantBoosts = QueryBoosts(combatant, query);

            int total = 0;
            foreach (var boost in relevantBoosts)
            {
                // IncreaseMaxHP(value) — flat increase; percentage TBD
                total += boost.Definition.GetIntParameter(0, 0);
            }

            return total;
        }

        /// <summary>
        /// Gets the total temporary HP value from TemporaryHP boosts.
        /// Note: the actual HP grant happens immediately in BoostApplicator.ApplyBoosts
        /// (because TempHP is non-stacking — takes the higher value). This query method
        /// allows other systems to inspect how much TempHP is sourced from boosts.
        /// </summary>
        public static int GetTemporaryHP(Combatant combatant)
        {
            if (combatant == null) return 0;

            var query = new BoostQuery(BoostType.TemporaryHP);
            var relevantBoosts = QueryBoosts(combatant, query);

            int total = 0;
            foreach (var boost in relevantBoosts)
            {
                total += boost.Definition.GetIntParameter(0, 0);
            }

            return total;
        }

        /// <summary>
        /// Gets tags granted by Tag boosts.
        /// </summary>
        public static HashSet<string> GetGrantedTags(Combatant combatant)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (combatant == null) return tags;

            var query = new BoostQuery(BoostType.Tag);
            var relevantBoosts = QueryBoosts(combatant, query);

            foreach (var boost in relevantBoosts)
            {
                var tag = boost.Definition.GetStringParameter(0, "");
                if (!string.IsNullOrEmpty(tag))
                    tags.Add(tag);
            }

            return tags;
        }

        /// <summary>
        /// Checks if non-lethal mode is enabled for the combatant.
        /// </summary>
        public static bool IsNonLethal(Combatant combatant)
        {
            if (combatant == null) return false;

            var query = new BoostQuery(BoostType.NonLethal);
            var relevantBoosts = QueryBoosts(combatant, query);

            return relevantBoosts.Any();
        }

        /// <summary>
        /// Gets the initiative modifier from Initiative boosts.
        /// </summary>
        public static int GetInitiativeModifier(Combatant combatant)
        {
            if (combatant == null) return 0;

            var query = new BoostQuery(BoostType.Initiative);
            var relevantBoosts = QueryBoosts(combatant, query);

            int total = 0;
            foreach (var boost in relevantBoosts)
            {
                total += boost.Definition.GetIntParameter(0, 0);
            }

            return total;
        }

        /// <summary>
        /// Gets the movement speed bonus from MovementSpeedBonus boosts.
        /// </summary>
        public static int GetMovementSpeedBonus(Combatant combatant)
        {
            if (combatant == null) return 0;

            var query = new BoostQuery(BoostType.MovementSpeedBonus);
            var relevantBoosts = QueryBoosts(combatant, query);

            int total = 0;
            foreach (var boost in relevantBoosts)
            {
                total += boost.Definition.GetIntParameter(0, 0);
            }

            return total;
        }

        /// <summary>
        /// Gets the minimum roll result value for a given roll type.
        /// Returns 0 if no MinimumRollResult boost applies.
        /// </summary>
        public static int GetMinimumRollResult(Combatant combatant, RollType rollType)
        {
            if (combatant == null) return 0;

            var query = new BoostQuery(BoostType.MinimumRollResult);
            var relevantBoosts = QueryBoosts(combatant, query);

            int best = 0;
            foreach (var boost in relevantBoosts)
            {
                var boostRollType = boost.Definition.GetStringParameter(0, "");
                if (boostRollType.Equals(rollType.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    boostRollType.Equals("Attack", StringComparison.OrdinalIgnoreCase) && rollType == RollType.AttackRoll)
                {
                    int min = boost.Definition.GetIntParameter(1, 0);
                    if (min > best)
                        best = min;
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the ability score minimum override for a given ability.
        /// Returns 0 if no override applies (meaning no minimum enforced).
        /// </summary>
        public static int GetAbilityOverrideMinimum(Combatant combatant, AbilityType ability)
        {
            if (combatant == null) return 0;

            var query = new BoostQuery(BoostType.AbilityOverrideMinimum);
            var relevantBoosts = QueryBoosts(combatant, query);

            int best = 0;
            foreach (var boost in relevantBoosts)
            {
                var abilityName = boost.Definition.GetStringParameter(0, "");
                if (abilityName.Equals(ability.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    int min = boost.Definition.GetIntParameter(1, 0);
                    if (min > best)
                        best = min;
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the flat damage reduction for a given damage type.
        /// </summary>
        public static int GetDamageReduction(Combatant combatant, DamageType damageType)
        {
            if (combatant == null) return 0;

            var query = new BoostQuery(BoostType.DamageReduction);
            var relevantBoosts = QueryBoosts(combatant, query);

            int total = 0;
            foreach (var boost in relevantBoosts)
            {
                var boostDamageType = boost.Definition.GetStringParameter(0, "");
                if (boostDamageType.Equals("All", StringComparison.OrdinalIgnoreCase) ||
                    boostDamageType.Equals(damageType.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    total += boost.Definition.GetIntParameter(1, 0);
                }
            }

            return total;
        }

        /// <summary>
        /// Gets the critical hit range expansion value.
        /// Returns 0 for normal crit range (20 only), 1 for 19-20, etc.
        /// </summary>
        public static int GetCriticalHitExtraRange(Combatant combatant)
        {
            if (combatant == null) return 0;

            var query = new BoostQuery(BoostType.CriticalHitExtraRange);
            var relevantBoosts = QueryBoosts(combatant, query);

            int total = 0;
            foreach (var boost in relevantBoosts)
            {
                total += boost.Definition.GetIntParameter(0, 0);
            }

            return total;
        }

        /// <summary>
        /// Gets the extra critical hit damage dice count.
        /// </summary>
        public static int GetCriticalHitExtraDice(Combatant combatant)
        {
            if (combatant == null) return 0;

            var query = new BoostQuery(BoostType.CriticalHitExtraDice);
            var relevantBoosts = QueryBoosts(combatant, query);

            int total = 0;
            foreach (var boost in relevantBoosts)
            {
                total += boost.Definition.GetIntParameter(0, 0);
            }

            return total;
        }

        // ============================================================
        // TIER 5: ADVANCED COMBAT MECHANIC EVALUATORS
        // ============================================================

        /// <summary>
        /// Gets all CharacterWeaponDamage boost parameter strings for the combatant.
        /// Each entry is a raw expression such as "2", "1d6", or "LevelMapValue(RageDamage)".
        /// The caller is responsible for resolving LevelMapValue references.
        /// Conditional boosts (with IF clauses) are skipped — use the overload with ConditionContext.
        /// </summary>
        public static List<string> GetCharacterWeaponDamageBonus(Combatant combatant)
        {
            var result = new List<string>();
            if (combatant == null) return result;

            var query = new BoostQuery(BoostType.CharacterWeaponDamage);
            var relevantBoosts = QueryBoosts(combatant, query);

            foreach (var boost in relevantBoosts)
            {
                // CharacterWeaponDamage(expression)
                var param = boost.Definition.GetStringParameter(0, "");
                if (!string.IsNullOrEmpty(param))
                    result.Add(param);
            }

            return result;
        }

        /// <summary>
        /// Gets all CharacterWeaponDamage boost parameter strings for the combatant,
        /// evaluating conditional boosts against the provided <see cref="ConditionContext"/>.
        /// Each entry is a raw expression such as "2", "1d6", or "LevelMapValue(RageDamage)".
        /// The caller is responsible for resolving LevelMapValue references.
        /// </summary>
        public static List<string> GetCharacterWeaponDamageBonus(Combatant combatant, ConditionContext context)
        {
            var result = new List<string>();
            if (combatant == null) return result;

            var query = new BoostQuery(BoostType.CharacterWeaponDamage);
            var relevantBoosts = QueryBoosts(combatant, query, context);

            foreach (var boost in relevantBoosts)
            {
                // CharacterWeaponDamage(expression)
                var param = boost.Definition.GetStringParameter(0, "");
                if (!string.IsNullOrEmpty(param))
                    result.Add(param);
            }

            return result;
        }

        /// <summary>
        /// Returns true if the combatant has any active TwoWeaponFighting boost.
        /// </summary>
        public static bool HasTwoWeaponFighting(Combatant combatant)
        {
            if (combatant == null) return false;

            var query = new BoostQuery(BoostType.TwoWeaponFighting);
            var relevantBoosts = QueryBoosts(combatant, query);

            return relevantBoosts.Any();
        }

        /// <summary>
        /// Gets all Reroll boost rules for the combatant.
        /// Each tuple contains: (rollType, minValue, keepHigher).
        /// Example: Reroll(Attack, 1, true) → ("Attack", 1, true)
        /// </summary>
        public static List<(string RollType, int MinValue, bool KeepHigher)> GetRerollRules(Combatant combatant)
        {
            var result = new List<(string, int, bool)>();
            if (combatant == null) return result;

            var query = new BoostQuery(BoostType.Reroll);
            var relevantBoosts = QueryBoosts(combatant, query);

            foreach (var boost in relevantBoosts)
            {
                // Reroll(RollType, minValue, keepHigher)
                var rollType = boost.Definition.GetStringParameter(0, "");
                var minValue = boost.Definition.GetIntParameter(1, 1);
                var keepHigherStr = boost.Definition.GetStringParameter(2, "true");
                bool keepHigher = keepHigherStr.Equals("true", StringComparison.OrdinalIgnoreCase);

                result.Add((rollType, minValue, keepHigher));
            }

            return result;
        }

        /// <summary>
        /// Queries active boosts on a combatant that match the specified criteria.
        /// Conditional boosts (with IF clauses) are skipped when no context is provided.
        /// Use the overload with <see cref="ConditionContext"/> to include conditional boosts.
        /// </summary>
        /// <param name="combatant">The combatant to query</param>
        /// <param name="query">The query parameters</param>
        /// <returns>List of relevant active boosts</returns>
        public static List<ActiveBoost> QueryBoosts(Combatant combatant, BoostQuery query)
        {
            return QueryBoosts(combatant, query, context: null);
        }

        /// <summary>
        /// Queries active boosts on a combatant that match the specified criteria,
        /// evaluating conditional boosts against the provided <see cref="ConditionContext"/>.
        /// </summary>
        /// <param name="combatant">The combatant to query</param>
        /// <param name="query">The query parameters</param>
        /// <param name="context">Optional combat context. If null, conditional boosts are excluded.</param>
        /// <returns>List of relevant active boosts whose conditions are met</returns>
        public static List<ActiveBoost> QueryBoosts(Combatant combatant, BoostQuery query, ConditionContext context)
        {
            if (combatant == null || query == null)
                return new List<ActiveBoost>();

            var results = new List<ActiveBoost>();
            var evaluator = ConditionEvaluator.Instance;

            foreach (var boost in combatant.Boosts.AllBoosts)
            {
                // Evaluate conditional boosts if context is provided; skip otherwise
                if (boost.IsConditional)
                {
                    if (context == null)
                        continue;
                    if (!evaluator.Evaluate(boost.Definition.Condition, context))
                        continue;
                }

                // Check if boost type matches
                if (boost.Definition.Type != query.BoostType)
                    continue;

                // Additional filtering based on boost type
                if (!IsBoostRelevant(boost, query))
                    continue;

                results.Add(boost);
            }

            return results;
        }

        /// <summary>
        /// Checks if a specific boost is relevant to the query context.
        /// Handles parameter matching for Advantage, Disadvantage, Resistance, etc.
        /// </summary>
        private static bool IsBoostRelevant(ActiveBoost boost, BoostQuery query)
        {
            switch (boost.Definition.Type)
            {
                case BoostType.Advantage:
                case BoostType.Disadvantage:
                    return IsAdvantageDisadvantageRelevant(boost, query);

                case BoostType.Resistance:
                    // Resistance boosts are filtered by damage type in GetResistanceLevel
                    return true;

                case BoostType.DamageBonus:
                    // DamageBonus boosts are filtered by damage type in GetDamageBonus
                    return true;

                case BoostType.AC:
                case BoostType.StatusImmunity:
                    // Always relevant for their respective queries
                    return true;

                default:
                    // Other boost types pass through for now
                    return true;
            }
        }

        /// <summary>
        /// Checks if an Advantage/Disadvantage boost is relevant to the query.
        /// Handles RollType and Ability matching.
        /// </summary>
        private static bool IsAdvantageDisadvantageRelevant(ActiveBoost boost, BoostQuery query)
        {
            // Advantage/Disadvantage(RollType) or Advantage/Disadvantage(RollType, Ability)
            var rollTypeParam = boost.Definition.GetStringParameter(0, "");

            // Check for special keywords
            if (rollTypeParam.Equals("AllSavingThrows", StringComparison.OrdinalIgnoreCase))
            {
                return query.RollType == Boosts.RollType.SavingThrow;
            }

            if (rollTypeParam.Equals("AllAbilities", StringComparison.OrdinalIgnoreCase) ||
                rollTypeParam.Equals("AllAbilityChecks", StringComparison.OrdinalIgnoreCase))
            {
                return query.RollType == Boosts.RollType.AbilityCheck || 
                       query.RollType == Boosts.RollType.SkillCheck;
            }

            // Match roll type
            if (query.RollType.HasValue && 
                Enum.TryParse<RollType>(rollTypeParam, ignoreCase: true, out var boostRollType))
            {
                if (boostRollType != query.RollType.Value)
                    return false;

                // If ability is specified in boost, check if it matches
                var abilityParam = boost.Definition.GetStringParameter(1, "");
                if (!string.IsNullOrEmpty(abilityParam) && query.Ability.HasValue)
                {
                    if (Enum.TryParse<AbilityType>(abilityParam, ignoreCase: true, out var boostAbility))
                    {
                        return boostAbility == query.Ability.Value;
                    }
                }

                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Describes the active critical hit modifiers on a combatant.
    /// Retrieved via <see cref="BoostEvaluator.GetCriticalHitModifier"/>.
    /// </summary>
    public class CriticalHitInfo
    {
        /// <summary>
        /// If true, attacks automatically count as critical hits (e.g., CriticalHit(AttackRoll, Success)).
        /// </summary>
        public bool AutoCrit { get; set; }

        /// <summary>
        /// If true, the combatant can never roll a critical hit (e.g., CriticalHit(AttackRoll, Never)).
        /// </summary>
        public bool NeverCrit { get; set; }

        /// <summary>
        /// The number of extra values in the crit range (0 = normal 20 only, 1 = 19-20, etc.).
        /// </summary>
        public int ExtraRange { get; set; }

        /// <summary>
        /// The number of additional damage dice rolled on a critical hit.
        /// </summary>
        public int ExtraDice { get; set; }

        /// <summary>
        /// Returns true if any critical hit modifier is active.
        /// </summary>
        public bool HasModifier => AutoCrit || NeverCrit || ExtraRange > 0 || ExtraDice > 0;

        public override string ToString()
        {
            if (AutoCrit) return "AutoCrit";
            if (NeverCrit) return "NeverCrit";
            return "Normal";
        }
    }
}
