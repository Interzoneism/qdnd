using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Conditions;
using QDND.Combat.Statuses;
using QDND.Data;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Actions
{
    public class CombatRollResolver
    {
        public QDND.Combat.Services.ICombatContext CombatContext { get; set; }
        public CharacterDataRegistry CharacterDataRegistry { get; set; }
        public StatusManager Statuses { get; set; }
        public Func<IEnumerable<Combatant>> GetCombatants { get; set; }

        public CombatRollResolver(
            QDND.Combat.Services.ICombatContext combatContext,
            StatusManager statuses,
            Func<IEnumerable<Combatant>> getCombatants)
        {
            CombatContext = combatContext;
            Statuses = statuses;
            GetCombatants = getCombatants;
        }

        public static AbilityType? ParseAbilityType(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
                return null;

            return actionName.Trim().ToLowerInvariant() switch
            {
                // Core ability scores
                "str" or "strength" => AbilityType.Strength,
                "dex" or "dexterity" => AbilityType.Dexterity,
                "con" or "constitution" => AbilityType.Constitution,
                "int" or "intelligence" => AbilityType.Intelligence,
                "wis" or "wisdom" => AbilityType.Wisdom,
                "cha" or "charisma" => AbilityType.Charisma,
                // Skill names -> underlying ability (for contested checks like Shove)
                "athletics" => AbilityType.Strength,
                "acrobatics" or "sleight_of_hand" or "stealth" => AbilityType.Dexterity,
                "arcana" or "history" or "investigation" or "nature" or "religion" => AbilityType.Intelligence,
                "animal_handling" or "insight" or "medicine" or "perception" or "survival" => AbilityType.Wisdom,
                "deception" or "intimidation" or "performance" or "persuasion" => AbilityType.Charisma,
                _ => null
            };
        }

        public static int GetAbilityModifier(Combatant combatant, AbilityType action)
        {
            if (combatant == null)
                return 0;

            return combatant.GetAbilityModifier(action);
        }

        public int GetAttackRollBonus(Combatant source, ActionDefinition action, HashSet<string> effectiveTags)
        {
            if (source == null || source.ResolvedCharacter == null)
                return 0;

            int proficiency = Math.Max(0, source.ProficiencyBonus);
            int abilityMod = 0;
            int flatRollBonus = 0;
            int enchantmentBonus = 0;

            if (action.AttackType.HasValue)
            {
                switch (action.AttackType.Value)
                {
                    case AttackType.MeleeWeapon:
                    {
                        var weapon = source.MainHandWeapon;
                        bool isFinesse = weapon?.IsFinesse == true || effectiveTags.Contains("finesse");
                        bool isMonk = string.Equals(source.ResolvedCharacter?.Sheet?.StartingClassId, "Monk", StringComparison.OrdinalIgnoreCase);
                        abilityMod = (isFinesse || isMonk)
                            ? Math.Max(source.GetAbilityModifier(AbilityType.Strength), source.GetAbilityModifier(AbilityType.Dexterity))
                            : source.GetAbilityModifier(AbilityType.Strength);

                        // Check weapon proficiency
                        if (weapon != null && !IsWeaponProficient(source, weapon))
                            proficiency = 0;

                        enchantmentBonus = weapon?.EnchantmentBonus ?? 0;

                        // Apply flat RollBonus modifiers (e.g. GWM -5 penalty)
                        var gwmContext = ConditionContext.ForAttackRoll(source, null, isMelee: true, isWeapon: true);
                        flatRollBonus = QDND.Combat.Rules.Boosts.BoostEvaluator.GetAttackRollPenalty(source, "MeleeWeaponAttack", gwmContext);
                        break;
                    }
                    case AttackType.RangedWeapon:
                    {
                        var weapon = source.MainHandWeapon;
                        // Try to find the ranged weapon
                        if (weapon != null && !weapon.IsRanged && source.OffHandWeapon?.IsRanged == true)
                            weapon = source.OffHandWeapon;

                        abilityMod = source.GetAbilityModifier(AbilityType.Dexterity);

                        // Thrown weapons use STR
                        if (weapon?.IsThrown == true && !weapon.IsRanged)
                            abilityMod = source.GetAbilityModifier(AbilityType.Strength);

                        // Check weapon proficiency
                        if (weapon != null && !IsWeaponProficient(source, weapon))
                            proficiency = 0;

                        enchantmentBonus = weapon?.EnchantmentBonus ?? 0;

                        // Apply flat RollBonus modifiers (e.g. Sharpshooter -5 penalty)
                        var ssContext = ConditionContext.ForAttackRoll(source, null, isMelee: false, isWeapon: true);
                        flatRollBonus = QDND.Combat.Rules.Boosts.BoostEvaluator.GetAttackRollPenalty(source, "RangedWeaponAttack", ssContext);
                        break;
                    }
                    case AttackType.MeleeSpell:
                    case AttackType.RangedSpell:
                        abilityMod = GetSpellcastingAbilityModifier(source);
                        break;
                }
            }

            return abilityMod + proficiency + flatRollBonus + enchantmentBonus;
        }

        /// <summary>
        /// Check if a combatant is proficient with a specific weapon.
        /// </summary>
        public bool IsWeaponProficient(Combatant combatant, WeaponDefinition weapon)
        {
            if (combatant.ResolvedCharacter?.Proficiencies == null)
                return true; // Old-style units are always proficient

            var profs = combatant.ResolvedCharacter.Proficiencies;

            // Check category proficiency (Simple, Martial)
            if (profs.IsProficientWithWeaponCategory(weapon.Category))
                return true;

            // Check specific weapon proficiency
            if (profs.IsProficientWithWeapon(weapon.WeaponType))
                return true;

            return false;
        }

        public int GetSavingThrowBonus(Combatant target, string saveType)
        {
            if (target == null)
                return 0;

            var action = ParseAbilityType(saveType);
            if (!action.HasValue)
                return 0;

            int bonus = target.GetAbilityModifier(action.Value);

            // If no resolved character is present, bonus stays at 0
            if (target.ResolvedCharacter?.Proficiencies.IsProficientInSave(action.Value) == true)
            {
                bonus += Math.Max(0, target.ProficiencyBonus);
            }

            return bonus;
        }

        /// <summary>
        /// Get the skill check bonus for a contested check participant.
        /// </summary>
        public int GetContestSkillBonus(Combatant combatant, string skillName)
        {
            if (combatant == null || string.IsNullOrEmpty(skillName))
                return 0;

            if (Enum.TryParse<Skill>(skillName, true, out var skill))
            {
                return combatant.GetSkillBonus(skill);
            }

            // Fallback: treat as raw ability
            var ability = ParseAbilityType(skillName);
            return ability.HasValue ? combatant.GetAbilityModifier(ability.Value) : 0;
        }

        /// <summary>
        /// Get the best skill bonus for the defender from a comma-separated list of skills.
        /// BG3 Shove: defender uses max(Athletics, Acrobatics).
        /// </summary>
        public int GetBestContestSkillBonus(Combatant combatant, string skillNames)
        {
            if (combatant == null || string.IsNullOrEmpty(skillNames))
                return 0;

            int best = int.MinValue;
            foreach (var name in skillNames.Split(','))
            {
                int bonus = GetContestSkillBonus(combatant, name.Trim());
                if (bonus > best) best = bonus;
            }

            return best == int.MinValue ? 0 : best;
        }

        public int ComputeSaveDC(Combatant source, ActionDefinition action, HashSet<string> effectiveTags)
        {
            // Summoned entities inherit their caster's spell save DC
            if (source?.OwnerSpellSaveDC.HasValue == true)
                return source.OwnerSpellSaveDC.Value;

            if (source?.ResolvedCharacter == null)
                return 10;

            int proficiency = Math.Max(0, source?.ProficiencyBonus ?? 0);
            bool isSpell = effectiveTags.Contains("spell") || effectiveTags.Contains("magic");

            if (isSpell)
            {
                return 8 + proficiency + GetSpellcastingAbilityModifier(source);
            }

            if (action.AttackType == AttackType.MeleeWeapon || action.AttackType == AttackType.RangedWeapon)
            {
                int strMod = source.GetAbilityModifier(AbilityType.Strength);
                int dexMod = source.GetAbilityModifier(AbilityType.Dexterity);
                return 8 + proficiency + Math.Max(strMod, dexMod);
            }

            return 8 + proficiency + GetSpellcastingAbilityModifier(source);
        }

        public int GetSpellcastingAbilityModifier(Combatant source)
        {
            if (source?.ResolvedCharacter?.Sheet?.ClassLevels == null) return 0;
            var registry = CharacterDataRegistry;
            if (registry != null)
            {
                foreach (var cl in source.ResolvedCharacter.Sheet.ClassLevels)
                {
                    var classDef = registry.GetClass(cl.ClassId);
                    if (!string.IsNullOrEmpty(classDef?.SpellcastingAbility) &&
                        Enum.TryParse<AbilityType>(classDef.SpellcastingAbility, true, out var ability))
                        return source.GetAbilityModifier(ability);
                }
                return 0;
            }
            // Fallback if registry unavailable — use first caster class (same order as primary path)
            foreach (var cl in source.ResolvedCharacter.Sheet.ClassLevels)
            {
                string classId = cl.ClassId?.ToLowerInvariant();
                switch (classId)
                {
                    case "wizard": return source.GetAbilityModifier(AbilityType.Intelligence);
                    case "cleric" or "druid" or "ranger" or "monk": return source.GetAbilityModifier(AbilityType.Wisdom);
                    case "bard" or "sorcerer" or "warlock" or "paladin": return source.GetAbilityModifier(AbilityType.Charisma);
                }
            }
            return 0;
        }

        public static int GetCriticalThreshold(Combatant source, bool isSpellAttack)
        {
            if (source?.ResolvedCharacter?.Features == null)
                return 20;

            bool hasImprovedCritical = source.ResolvedCharacter.Features.Any(f =>
                string.Equals(f.Id, "improved_critical", StringComparison.OrdinalIgnoreCase));
            bool hasSpellSniper = source.ResolvedCharacter.Sheet?.FeatIds?.Any(f =>
                string.Equals(f, "spell_sniper", StringComparison.OrdinalIgnoreCase)) == true;

            if (!isSpellAttack && hasImprovedCritical)
                return 19;
            if (isSpellAttack && hasSpellSniper)
                return 19;

            return 20;
        }

        public static void ApplyWindowRollSources(QueryInput query, RuleEventContext windowContext)
        {
            if (query == null || windowContext == null)
                return;

            MergeParameterSources(query.Parameters, "statusAdvantageSources", windowContext.AdvantageSources);
            MergeParameterSources(query.Parameters, "statusDisadvantageSources", windowContext.DisadvantageSources);
        }

        public static void MergeParameterSources(Dictionary<string, object> parameters, string key, List<string> toAdd)
        {
            if (parameters == null || toAdd == null || toAdd.Count == 0)
                return;

            var merged = new List<string>();
            if (parameters.TryGetValue(key, out var existing))
            {
                switch (existing)
                {
                    case IEnumerable<string> list:
                        merged.AddRange(list.Where(v => !string.IsNullOrWhiteSpace(v)));
                        break;
                    case string single when !string.IsNullOrWhiteSpace(single):
                        merged.Add(single);
                        break;
                }
            }

            merged.AddRange(toAdd.Where(v => !string.IsNullOrWhiteSpace(v)));
            parameters[key] = merged.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Check if a combatant is within melee range (1.5m) of any hostile combatant.
        /// </summary>
        public bool IsWithinHostileMeleeRange(Combatant combatant)
        {
            if (GetCombatants == null)
                return false;

            foreach (var other in GetCombatants())
            {
                // Skip self
                if (other.Id == combatant.Id)
                    continue;

                // Skip non-hostile (same faction or inactive)
                if (other.Faction == combatant.Faction || !other.IsActive)
                    continue;

                // Check distance
                float dist = combatant.Position.DistanceTo(other.Position);
                if (dist <= CombatRules.GetMeleeReach(other))
                    return true;
            }

            return false;
        }

        public static bool ShouldApplyMeleeAutoCrit(bool targetMeleeAutocritFlag, bool isMeleeAttack, float attackDistanceMeters)
        {
            if (!targetMeleeAutocritFlag || !isMeleeAttack)
            {
                return false;
            }

            return attackDistanceMeters <= CombatRules.MeleeAutocritRangeMeters;
        }

        public bool ShouldAutoFailSave(Combatant target, string saveType)
        {
            if (Statuses == null || target == null || string.IsNullOrWhiteSpace(saveType))
                return false;
            string normalized = saveType.Trim().ToLowerInvariant();

            // Boost-based auto-fail (any ability type, e.g. AbilityFailedSavingThrow from Hold Person)
            if (Enum.TryParse<AbilityType>(normalized, ignoreCase: true, out var abilityType))
            {
                if (QDND.Combat.Rules.Boosts.BoostEvaluator.ShouldAutoFailSaveFromBoosts(target, abilityType))
                    return true;
            }

            if (normalized != "strength" && normalized != "dexterity")
                return false;

            var activeStatuses = Statuses.GetStatuses(target.Id);
            if (normalized == "strength" && activeStatuses.Any(s => s?.Definition?.Tags?.Contains("auto_fail_save_strength") == true))
                return true;
            if (normalized == "dexterity" && activeStatuses.Any(s => s?.Definition?.Tags?.Contains("auto_fail_save_dexterity") == true))
                return true;

            var tgtIds = activeStatuses.SelectMany(s => new[] { s.Definition.Id }.Concat(s.Definition.Tags ?? Enumerable.Empty<string>()));
            var tgtEffects = ConditionEffects.GetAggregateEffects(tgtIds);
            return tgtEffects.AutoFailStrDexSaves;
        }
    }
}