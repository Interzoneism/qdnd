using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using QDND.Combat.Actions;

namespace QDND.Data.Actions
{
    /// <summary>
    /// Parses BG3 spell formula strings (SpellSuccess, SpellFail, SpellRoll) into EffectDefinition objects.
    /// Handles the conversion of BG3's functor-based formulas into the game's effect system.
    /// </summary>
    public static class SpellEffectConverter
    {
        /// <summary>
        /// Parse a BG3 SpellSuccess or SpellFail formula string into a list of EffectDefinitions.
        /// </summary>
        /// <param name="formulaString">The raw BG3 formula string (e.g., "DealDamage(1d10, Fire);ApplyStatus(BURNING,100,2)")</param>
        /// <param name="isFailEffect">If true, marks effects as fail-only (e.g., half damage on save)</param>
        /// <returns>List of parsed EffectDefinitions</returns>
        public static List<EffectDefinition> ParseEffects(string formulaString, bool isFailEffect = false)
        {
            var effects = new List<EffectDefinition>();

            if (string.IsNullOrWhiteSpace(formulaString))
                return effects;

            // Split by semicolon for multiple effects
            var functorCalls = SplitFunctors(formulaString);

            foreach (var functorCall in functorCalls)
            {
                var trimmed = functorCall.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // Skip conditional wrappers (IF, TARGET, GROUND, etc.) - extract inner content
                trimmed = UnwrapConditionals(trimmed);

                // Parse individual functor
                var effect = ParseSingleEffect(trimmed);
                if (effect != null)
                {
                    // Mark fail effects appropriately
                    if (isFailEffect)
                    {
                        effect.Condition = "on_miss";
                        
                        // For damage effects on save fail, typically half damage is applied
                        if (effect.Type == "damage")
                        {
                            effect.SaveTakesHalf = true;
                        }
                    }
                    
                    effects.Add(effect);
                }
            }

            return effects;
        }

        /// <summary>
        /// Parse a single BG3 functor call into an EffectDefinition.
        /// </summary>
        /// <param name="functor">A single functor string like "DealDamage(1d10, Fire, Magical)"</param>
        /// <returns>Parsed EffectDefinition or null if parsing failed</returns>
        public static EffectDefinition ParseSingleEffect(string functor)
        {
            if (string.IsNullOrWhiteSpace(functor))
                return null;

            functor = functor.Trim();

            // Check for :Half modifier (typically for save spells)
            bool halfOnSave = false;
            if (functor.EndsWith(":Half", StringComparison.OrdinalIgnoreCase))
            {
                halfOnSave = true;
                functor = functor.Substring(0, functor.Length - 5).Trim();
            }

            // DealDamage(dice, damageType, [flags])
            var damageMatch = Regex.Match(functor, 
                @"DealDamage\s*\(\s*([^,]+)\s*,\s*(\w+)\s*(?:,\s*(\w+))?\s*\)", 
                RegexOptions.IgnoreCase);
            if (damageMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "damage",
                    DiceFormula = CleanDiceFormula(damageMatch.Groups[1].Value),
                    DamageType = damageMatch.Groups[2].Value.Trim().ToLowerInvariant(),
                    SaveTakesHalf = halfOnSave,
                    Condition = null // Will be set by caller if needed
                };
            }

            // ApplyStatus(statusId, chance, duration)
            var statusMatch = Regex.Match(functor,
                @"ApplyStatus\s*\(\s*(\w+)\s*(?:,\s*(\d+)\s*)?(?:,\s*(-?\d+)\s*)?\)",
                RegexOptions.IgnoreCase);
            if (statusMatch.Success)
            {
                int duration = 1;
                if (statusMatch.Groups[3].Success)
                {
                    int.TryParse(statusMatch.Groups[3].Value, out duration);
                }

                return new EffectDefinition
                {
                    Type = "apply_status",
                    StatusId = statusMatch.Groups[1].Value.ToLowerInvariant(),
                    StatusDuration = duration,
                    StatusStacks = 1
                };
            }

            // RegainHitPoints(formula) - healing
            var healMatch = Regex.Match(functor,
                @"(?:RegainHitPoints|Heal)\s*\(\s*([^)]+)\s*\)",
                RegexOptions.IgnoreCase);
            if (healMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "heal",
                    DiceFormula = CleanDiceFormula(healMatch.Groups[1].Value)
                };
            }

            // RemoveStatus(statusId)
            var removeStatusMatch = Regex.Match(functor,
                @"RemoveStatus\s*\(\s*(\w+)\s*\)",
                RegexOptions.IgnoreCase);
            if (removeStatusMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "remove_status",
                    StatusId = removeStatusMatch.Groups[1].Value.ToLowerInvariant()
                };
            }

            // Force(distance) - push effect
            var forceMatch = Regex.Match(functor,
                @"Force\s*\(\s*(\d+\.?\d*)\s*\)",
                RegexOptions.IgnoreCase);
            if (forceMatch.Success)
            {
                float.TryParse(forceMatch.Groups[1].Value, out var distance);
                return new EffectDefinition
                {
                    Type = "forced_move",
                    Value = distance,
                    Parameters = new Dictionary<string, object> { { "direction", "away" } }
                };
            }

            // Teleport(distance)
            var teleportMatch = Regex.Match(functor,
                @"Teleport\s*\(\s*(\d+\.?\d*)\s*\)",
                RegexOptions.IgnoreCase);
            if (teleportMatch.Success)
            {
                float.TryParse(teleportMatch.Groups[1].Value, out var distance);
                return new EffectDefinition
                {
                    Type = "teleport",
                    Value = distance
                };
            }

            // SummonCreature(templateId, duration, [hp])
            var summonMatch = Regex.Match(functor,
                @"SummonCreature\s*\(\s*([^,]+)\s*(?:,\s*(\d+)\s*)?(?:,\s*(\d+)\s*)?\)",
                RegexOptions.IgnoreCase);
            if (summonMatch.Success)
            {
                var effect = new EffectDefinition
                {
                    Type = "summon",
                    Parameters = new Dictionary<string, object>
                    {
                        { "templateId", summonMatch.Groups[1].Value.Trim() }
                    }
                };

                if (summonMatch.Groups[2].Success && int.TryParse(summonMatch.Groups[2].Value, out var duration))
                {
                    effect.StatusDuration = duration;
                }

                if (summonMatch.Groups[3].Success && int.TryParse(summonMatch.Groups[3].Value, out var hp))
                {
                    effect.Parameters["hp"] = hp;
                }

                return effect;
            }

            // CreateSurface(surfaceType, radius, duration)
            var surfaceMatch = Regex.Match(functor,
                @"CreateSurface\s*\(\s*([^,]+)\s*(?:,\s*(\d+\.?\d*)\s*)?(?:,\s*(\d+)\s*)?\)",
                RegexOptions.IgnoreCase);
            if (surfaceMatch.Success)
            {
                var effect = new EffectDefinition
                {
                    Type = "spawn_surface",
                    Parameters = new Dictionary<string, object>
                    {
                        { "surface_type", surfaceMatch.Groups[1].Value.Trim().ToLowerInvariant() }
                    }
                };

                if (surfaceMatch.Groups[2].Success && float.TryParse(surfaceMatch.Groups[2].Value, out var radius))
                {
                    effect.Value = radius;
                }

                if (surfaceMatch.Groups[3].Success && int.TryParse(surfaceMatch.Groups[3].Value, out var duration))
                {
                    effect.StatusDuration = duration;
                }

                return effect;
            }

            // If we couldn't parse it, log a warning but don't fail
            if (!IsIgnorableFunc(functor))
            {
                // Use Console for headless/test environments where Godot isn't available
                try
                {
                    Godot.GD.PrintErr($"[SpellEffectConverter] Could not parse functor: {functor}");
                }
                catch
                {
                    Console.WriteLine($"[SpellEffectConverter] Could not parse functor: {functor}");
                }
            }

            return null;
        }

        /// <summary>
        /// Parse BG3 SpellRoll field to extract attack type and save information.
        /// </summary>
        /// <param name="spellRoll">Raw SpellRoll string (e.g., "Attack(AttackType.MeleeSpellAttack)" or "not SavingThrow(Ability.Dexterity, SourceSpellDC())")</param>
        /// <param name="attackType">Output: parsed attack type</param>
        /// <param name="saveType">Output: parsed save ability type</param>
        /// <param name="saveDC">Output: parsed save DC (null if using caster's spell DC)</param>
        public static void ParseSpellRoll(string spellRoll, out AttackType? attackType, out string saveType, out int? saveDC)
        {
            attackType = null;
            saveType = null;
            saveDC = null;

            if (string.IsNullOrWhiteSpace(spellRoll))
                return;

            // Parse Attack(AttackType.X)
            var attackMatch = Regex.Match(spellRoll,
                @"Attack\s*\(\s*AttackType\.(\w+)\s*\)",
                RegexOptions.IgnoreCase);
            if (attackMatch.Success)
            {
                var attackTypeStr = attackMatch.Groups[1].Value;
                attackType = ParseAttackType(attackTypeStr);
            }

            // Parse SavingThrow(Ability.X, DC)
            var saveMatch = Regex.Match(spellRoll,
                @"SavingThrow\s*\(\s*Ability\.(\w+)\s*(?:,\s*([^)]+))?\s*\)",
                RegexOptions.IgnoreCase);
            if (saveMatch.Success)
            {
                saveType = saveMatch.Groups[1].Value.ToLowerInvariant();

                // Check if DC is a number or SourceSpellDC()
                if (saveMatch.Groups[2].Success)
                {
                    var dcStr = saveMatch.Groups[2].Value.Trim();
                    if (int.TryParse(dcStr, out var dc))
                    {
                        saveDC = dc;
                    }
                    // SourceSpellDC() means use caster's spell DC, so leave saveDC as null
                }
            }
        }

        #region Helper Methods

        /// <summary>
        /// Split a formula string by semicolons, respecting nested parentheses.
        /// </summary>
        private static List<string> SplitFunctors(string formula)
        {
            var result = new List<string>();
            int depth = 0;
            int start = 0;

            for (int i = 0; i < formula.Length; i++)
            {
                char c = formula[i];
                
                if (c == '(')
                    depth++;
                else if (c == ')')
                    depth--;
                else if (c == ';' && depth == 0)
                {
                    result.Add(formula.Substring(start, i - start));
                    start = i + 1;
                }
            }

            // Add remaining
            if (start < formula.Length)
            {
                result.Add(formula.Substring(start));
            }

            return result;
        }

        /// <summary>
        /// Remove BG3 conditional wrappers like IF(), TARGET:, GROUND:, etc.
        /// </summary>
        private static string UnwrapConditionals(string functor)
        {
            // Remove prefixes like TARGET:, GROUND:, SELF:
            var prefixMatch = Regex.Match(functor, @"^(TARGET|GROUND|SELF|SOURCE):\s*(.+)$", RegexOptions.IgnoreCase);
            if (prefixMatch.Success)
            {
                functor = prefixMatch.Groups[2].Value;
            }

            // Remove IF() wrappers - extract the inner effect
            var ifMatch = Regex.Match(functor, @"^IF\s*\([^)]+\)\s*:\s*(.+)$", RegexOptions.IgnoreCase);
            if (ifMatch.Success)
            {
                functor = ifMatch.Groups[1].Value;
            }

            return functor;
        }

        /// <summary>
        /// Clean up dice formula strings (remove BG3-specific modifiers we can't evaluate yet).
        /// </summary>
        private static string CleanDiceFormula(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
                return formula;

            formula = formula.Trim();

            // Keep formulas with SpellcastingAbilityModifier, SpellCastingAbility, etc. as-is
            // The DiceRoller or evaluation system will handle these later
            
            // Remove division operators for now (e.g., "MainMeleeWeapon/2" from Cleave)
            // Convert to multiplication by 0.5? For now, just warn and keep as-is
            if (formula.Contains("/"))
            {
                // Use Console for headless/test environments where Godot isn't available
                try
                {
                    Godot.GD.Print($"[SpellEffectConverter] Warning: Formula contains division: {formula}");
                }
                catch
                {
                    Console.WriteLine($"[SpellEffectConverter] Warning: Formula contains division: {formula}");
                }
            }

            // Handle max() function (e.g., "max(1, OffhandMeleeWeapon)")
            var maxMatch = Regex.Match(formula, @"max\s*\(\s*(\d+)\s*,\s*([^)]+)\s*\)", RegexOptions.IgnoreCase);
            if (maxMatch.Success)
            {
                // For now, just use the inner formula and ignore the min value
                formula = maxMatch.Groups[2].Value.Trim();
            }

            return formula;
        }

        /// <summary>
        /// Parse BG3 AttackType string to AttackType enum.
        /// </summary>
        private static AttackType? ParseAttackType(string attackTypeStr)
        {
            return attackTypeStr.ToLowerInvariant() switch
            {
                "meleespellattack" => AttackType.MeleeSpell,
                "rangedspellattack" => AttackType.RangedSpell,
                "meleeweaponattack" => AttackType.MeleeWeapon,
                "rangedweaponattack" => AttackType.RangedWeapon,
                "meleeoffhandweaponattack" => AttackType.MeleeWeapon, // Offhand attacks still use MeleeWeapon
                "rangedoffhandweaponattack" => AttackType.RangedWeapon,
                "meleeunarmedattack" => AttackType.MeleeWeapon, // Unarmed counts as melee weapon
                _ => null
            };
        }

        /// <summary>
        /// Check if a functor is ignorable (utility/cosmetic functions we don't need to convert).
        /// </summary>
        private static bool IsIgnorableFunc(string functor)
        {
            var ignorableFuncs = new[]
            {
                "ExecuteWeaponFunctors",
                "CreateExplosion",
                "SurfaceChange",
                "CastOffhand",
                "SpawnExtraProjectiles",
                "RestoreResource",
                "ApplyEquipmentStatus"
            };

            foreach (var func in ignorableFuncs)
            {
                if (functor.StartsWith(func, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        #endregion
    }
}
