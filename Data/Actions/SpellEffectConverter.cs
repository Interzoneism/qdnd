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

            // Summon(templateId, duration, [hp]) - BG3 alias for SummonCreature
            var summonAliasMatch = Regex.Match(functor,
                @"Summon\s*\(\s*([^,]+)\s*(?:,\s*(\d+)\s*)?(?:,\s*(\d+)\s*)?\)",
                RegexOptions.IgnoreCase);
            if (summonAliasMatch.Success)
            {
                var effect = new EffectDefinition
                {
                    Type = "summon",
                    Parameters = new Dictionary<string, object>
                    {
                        { "templateId", summonAliasMatch.Groups[1].Value.Trim() }
                    }
                };

                if (summonAliasMatch.Groups[2].Success && int.TryParse(summonAliasMatch.Groups[2].Value, out var duration))
                {
                    effect.StatusDuration = duration;
                }

                if (summonAliasMatch.Groups[3].Success && int.TryParse(summonAliasMatch.Groups[3].Value, out var hp))
                {
                    effect.Parameters["hp"] = hp;
                }

                return effect;
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

            // SpawnExtraProjectiles(count)
            var spawnExtraProjectilesMatch = Regex.Match(functor,
                @"SpawnExtraProjectiles\s*\(\s*(\d+)\s*\)",
                RegexOptions.IgnoreCase);
            if (spawnExtraProjectilesMatch.Success)
            {
                int.TryParse(spawnExtraProjectilesMatch.Groups[1].Value, out var count);
                return new EffectDefinition
                {
                    Type = "spawn_extra_projectiles",
                    Value = count,
                    Parameters = new Dictionary<string, object>
                    {
                        { "count", count }
                    }
                };
            }

            // ApplyEquipmentStatus(statusId[, duration])
            var applyEquipmentStatusMatch = Regex.Match(functor,
                @"ApplyEquipmentStatus\s*\(\s*(\w+)\s*(?:,\s*(-?\d+)\s*)?\)",
                RegexOptions.IgnoreCase);
            if (applyEquipmentStatusMatch.Success)
            {
                int duration = -1;
                if (applyEquipmentStatusMatch.Groups[2].Success)
                {
                    int.TryParse(applyEquipmentStatusMatch.Groups[2].Value, out duration);
                }

                return new EffectDefinition
                {
                    Type = "apply_status",
                    StatusId = applyEquipmentStatusMatch.Groups[1].Value.ToLowerInvariant(),
                    StatusDuration = duration,
                    StatusStacks = 1
                };
            }

            // Douse(surfaceType)
            var douseMatch = Regex.Match(functor,
                @"Douse\s*\(\s*([^)]+)\s*\)",
                RegexOptions.IgnoreCase);
            if (douseMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "douse",
                    Parameters = new Dictionary<string, object>
                    {
                        { "target", douseMatch.Groups[1].Value.Trim().ToLowerInvariant() }
                    }
                };
            }

            // SpawnInInventory(itemId[, count])
            var spawnInInventoryMatch = Regex.Match(functor,
                @"SpawnInInventory\s*\(\s*([^,\)]+)\s*(?:,\s*(\d+)\s*)?\)",
                RegexOptions.IgnoreCase);
            if (spawnInInventoryMatch.Success)
            {
                int count = 1;
                if (spawnInInventoryMatch.Groups[2].Success)
                {
                    int.TryParse(spawnInInventoryMatch.Groups[2].Value, out count);
                }

                return new EffectDefinition
                {
                    Type = "spawn_inventory_item",
                    Value = count,
                    Parameters = new Dictionary<string, object>
                    {
                        { "item_id", spawnInInventoryMatch.Groups[1].Value.Trim() },
                        { "count", count }
                    }
                };
            }

            // FireProjectile(projectileId[, mode])
            var fireProjectileMatch = Regex.Match(functor,
                @"FireProjectile\s*\(\s*([^,\)]+)\s*(?:,\s*([^)]+))?\)",
                RegexOptions.IgnoreCase);
            if (fireProjectileMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "fire_projectile",
                    Parameters = new Dictionary<string, object>
                    {
                        { "projectile_id", fireProjectileMatch.Groups[1].Value.Trim() },
                        { "mode", fireProjectileMatch.Groups[2].Success ? fireProjectileMatch.Groups[2].Value.Trim() : "default" }
                    }
                };
            }

            // Equalize(resource)
            var equalizeMatch = Regex.Match(functor,
                @"Equalize\s*\(\s*([^)]+)\s*\)",
                RegexOptions.IgnoreCase);
            if (equalizeMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "equalize",
                    Parameters = new Dictionary<string, object>
                    {
                        { "resource", equalizeMatch.Groups[1].Value.Trim().ToLowerInvariant() }
                    }
                };
            }

            // SetStatusDuration(statusId, duration)
            var setStatusDurationMatch = Regex.Match(functor,
                @"SetStatusDuration\s*\(\s*(\w+)\s*,\s*(-?\d+)\s*\)",
                RegexOptions.IgnoreCase);
            if (setStatusDurationMatch.Success)
            {
                int.TryParse(setStatusDurationMatch.Groups[2].Value, out var duration);
                return new EffectDefinition
                {
                    Type = "set_status_duration",
                    StatusId = setStatusDurationMatch.Groups[1].Value.ToLowerInvariant(),
                    StatusDuration = duration
                };
            }

            // PickupEntity(entityId)
            var pickupEntityMatch = Regex.Match(functor,
                @"PickupEntity\s*\(\s*([^)]+)\s*\)",
                RegexOptions.IgnoreCase);
            if (pickupEntityMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "pickup_entity",
                    Parameters = new Dictionary<string, object>
                    {
                        { "entity_id", pickupEntityMatch.Groups[1].Value.Trim() }
                    }
                };
            }

            // SwapPlaces()
            var swapPlacesMatch = Regex.Match(functor,
                @"SwapPlaces\s*\(\s*\)",
                RegexOptions.IgnoreCase);
            if (swapPlacesMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "swap_places"
                };
            }

            // CreateZoneCloud(radius[, duration[, surfaceType]])
            var createZoneCloudMatch = Regex.Match(functor,
                @"CreateZoneCloud\s*\(\s*(\d+\.?\d*)\s*(?:,\s*(-?\d+)\s*)?(?:,\s*([^)]+)\s*)?\)",
                RegexOptions.IgnoreCase);
            if (createZoneCloudMatch.Success)
            {
                float.TryParse(createZoneCloudMatch.Groups[1].Value, out var radius);
                int duration = 0;
                if (createZoneCloudMatch.Groups[2].Success)
                {
                    int.TryParse(createZoneCloudMatch.Groups[2].Value, out duration);
                }

                string surfaceType = createZoneCloudMatch.Groups[3].Success
                    ? createZoneCloudMatch.Groups[3].Value.Trim().ToLowerInvariant()
                    : "cloud";

                return new EffectDefinition
                {
                    Type = "spawn_surface",
                    Value = radius,
                    StatusDuration = duration,
                    Parameters = new Dictionary<string, object>
                    {
                        { "surface_type", surfaceType }
                    }
                };
            }

            // Grant(resourceOrFeature[, amount])
            var grantMatch = Regex.Match(functor,
                @"Grant\s*\(\s*([^,\)]+)\s*(?:,\s*(\d+\.?\d*))?\s*\)",
                RegexOptions.IgnoreCase);
            if (grantMatch.Success)
            {
                var grantTarget = grantMatch.Groups[1].Value.Trim();
                float amount = 1f;
                if (grantMatch.Groups[2].Success)
                {
                    float.TryParse(grantMatch.Groups[2].Value, out amount);
                }

                if (grantTarget.Equals("ActionPoint", StringComparison.OrdinalIgnoreCase) ||
                    grantTarget.Equals("BonusActionPoint", StringComparison.OrdinalIgnoreCase))
                {
                    return new EffectDefinition
                    {
                        Type = "restore_resource",
                        Value = amount,
                        Parameters = new Dictionary<string, object>
                        {
                            { "resource_name", grantTarget.ToLowerInvariant() },
                            { "level", 0 }
                        }
                    };
                }

                return new EffectDefinition
                {
                    Type = "grant",
                    Value = amount,
                    Parameters = new Dictionary<string, object>
                    {
                        { "grant_id", grantTarget }
                    }
                };
            }

            // UseSpell(spellId)
            var useSpellMatch = Regex.Match(functor,
                @"UseSpell\s*\(\s*([^)]+)\s*\)",
                RegexOptions.IgnoreCase);
            if (useSpellMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "use_spell",
                    Parameters = new Dictionary<string, object>
                    {
                        { "spell_id", useSpellMatch.Groups[1].Value.Trim() }
                    }
                };
            }

            // CreateSurface(radius, duration, surfaceType) - BG3 arg order
            var surfaceMatch = Regex.Match(functor,
                @"CreateSurface\s*\(\s*(\d+\.?\d*)\s*,\s*(-?\d*)\s*,\s*([^,\)]+)\s*(?:,\s*[^\)]*)?\)",
                RegexOptions.IgnoreCase);
            if (surfaceMatch.Success)
            {
                var effect = new EffectDefinition
                {
                    Type = "spawn_surface",
                    Parameters = new Dictionary<string, object>
                    {
                        { "surface_type", surfaceMatch.Groups[3].Value.Trim().ToLowerInvariant() }
                    }
                };

                // Group 1 = radius
                if (float.TryParse(surfaceMatch.Groups[1].Value, out var radius))
                {
                    effect.Value = radius;
                }

                // Group 2 = duration (can be empty string or negative)
                string durationStr = surfaceMatch.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(durationStr) && int.TryParse(durationStr, out var duration))
                {
                    effect.StatusDuration = duration;
                }
                else
                {
                    effect.StatusDuration = 0; // Empty duration defaults to 0
                }

                return effect;
            }

            // RestoreResource(resourceName, amount, [level])
            var restoreResourceMatch = Regex.Match(functor,
                @"RestoreResource\s*\(\s*([^,]+)\s*,\s*(\d+\.?\d*)\s*(?:,\s*(\d+))?\s*\)",
                RegexOptions.IgnoreCase);
            if (restoreResourceMatch.Success)
            {
                var resourceName = restoreResourceMatch.Groups[1].Value.Trim();
                float.TryParse(restoreResourceMatch.Groups[2].Value, out var amount);
                int level = 0;
                if (restoreResourceMatch.Groups[3].Success)
                {
                    int.TryParse(restoreResourceMatch.Groups[3].Value, out level);
                }

                return new EffectDefinition
                {
                    Type = "restore_resource",
                    Value = amount,
                    Parameters = new Dictionary<string, object>
                    {
                        { "resource_name", resourceName.ToLowerInvariant() },
                        { "level", level }
                    }
                };
            }

            // BreakConcentration()
            var breakConcentrationMatch = Regex.Match(functor,
                @"BreakConcentration\s*\(\s*\)",
                RegexOptions.IgnoreCase);
            if (breakConcentrationMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "break_concentration"
                };
            }

            // GainTemporaryHitPoints(formula)
            var gainTempHPMatch = Regex.Match(functor,
                @"GainTemporaryHitPoints\s*\(\s*([^)]+)\s*\)",
                RegexOptions.IgnoreCase);
            if (gainTempHPMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "gain_temp_hp",
                    DiceFormula = CleanDiceFormula(gainTempHPMatch.Groups[1].Value)
                };
            }

            // CreateExplosion(spellId, [position])
            var createExplosionMatch = Regex.Match(functor,
                @"CreateExplosion\s*\(\s*([^,]+)\s*(?:,\s*([^)]+))?\s*\)",
                RegexOptions.IgnoreCase);
            if (createExplosionMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "create_explosion",
                    Parameters = new Dictionary<string, object>
                    {
                        { "spell_id", createExplosionMatch.Groups[1].Value.Trim() },
                        { "position", createExplosionMatch.Groups[2].Success ? createExplosionMatch.Groups[2].Value.Trim() : "target" }
                    }
                };
            }

            // SwitchDeathType(deathType)
            var switchDeathTypeMatch = Regex.Match(functor,
                @"SwitchDeathType\s*\(\s*(\w+)\s*\)",
                RegexOptions.IgnoreCase);
            if (switchDeathTypeMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "switch_death_type",
                    Parameters = new Dictionary<string, object>
                    {
                        { "death_type", switchDeathTypeMatch.Groups[1].Value.Trim().ToLowerInvariant() }
                    }
                };
            }

            // ExecuteWeaponFunctors([damageType])
            var executeWeaponMatch = Regex.Match(functor,
                @"ExecuteWeaponFunctors\s*\(\s*(?:(\w+))?\s*\)",
                RegexOptions.IgnoreCase);
            if (executeWeaponMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "execute_weapon_functors",
                    Parameters = new Dictionary<string, object>
                    {
                        { "damage_type", executeWeaponMatch.Groups[1].Success ? executeWeaponMatch.Groups[1].Value.Trim().ToLowerInvariant() : "physical" }
                    }
                };
            }

            // SurfaceChange(surfaceType, radius, lifetime) - 3 args
            var surfaceChangeMatch = Regex.Match(functor,
                @"SurfaceChange\s*\(\s*(\w+)\s*,\s*(\d+\.?\d*)\s*,\s*(\d+)\s*\)",
                RegexOptions.IgnoreCase);
            if (surfaceChangeMatch.Success)
            {
                float.TryParse(surfaceChangeMatch.Groups[2].Value, out var radius);
                int.TryParse(surfaceChangeMatch.Groups[3].Value, out var lifetime);

                return new EffectDefinition
                {
                    Type = "surface_change",
                    Value = radius,
                    StatusDuration = lifetime,
                    Parameters = new Dictionary<string, object>
                    {
                        { "surface_type", surfaceChangeMatch.Groups[1].Value.Trim().ToLowerInvariant() }
                    }
                };
            }

            // SurfaceChange(surfaceType) - single arg, fallback
            var surfaceChangeSingleMatch = Regex.Match(functor,
                @"SurfaceChange\s*\(\s*(\w+)\s*\)",
                RegexOptions.IgnoreCase);
            if (surfaceChangeSingleMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "surface_change",
                    Value = 0, // Default radius
                    StatusDuration = 0, // Default lifetime
                    Parameters = new Dictionary<string, object>
                    {
                        { "surface_type", surfaceChangeSingleMatch.Groups[1].Value.Trim().ToLowerInvariant() }
                    }
                };
            }

            // Stabilize()
            var stabilizeMatch = Regex.Match(functor,
                @"Stabilize\s*\(\s*\)",
                RegexOptions.IgnoreCase);
            if (stabilizeMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "stabilize"
                };
            }

            // Resurrect([hp])
            var resurrectMatch = Regex.Match(functor,
                @"Resurrect\s*\(\s*(?:(\d+))?\s*\)",
                RegexOptions.IgnoreCase);
            if (resurrectMatch.Success)
            {
                int hp = 1; // Default HP
                if (resurrectMatch.Groups[1].Success && int.TryParse(resurrectMatch.Groups[1].Value, out var parsedHP))
                {
                    hp = parsedHP;
                }

                return new EffectDefinition
                {
                    Type = "resurrect",
                    Value = hp
                };
            }

            // RemoveStatusByGroup(groupId)
            var removeStatusByGroupMatch = Regex.Match(functor,
                @"RemoveStatusByGroup\s*\(\s*(\w+)\s*\)",
                RegexOptions.IgnoreCase);
            if (removeStatusByGroupMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "remove_status_by_group",
                    Parameters = new Dictionary<string, object>
                    {
                        { "group_id", removeStatusByGroupMatch.Groups[1].Value.Trim() }
                    }
                };
            }

            // Counterspell()
            var counterspellMatch = Regex.Match(functor,
                @"Counterspell\s*\(\s*\)",
                RegexOptions.IgnoreCase);
            if (counterspellMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "counter"
                };
            }

            // SetAdvantage()
            var setAdvantageMatch = Regex.Match(functor,
                @"SetAdvantage\s*\(\s*\)",
                RegexOptions.IgnoreCase);
            if (setAdvantageMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "set_advantage"
                };
            }

            // SetDisadvantage()
            var setDisadvantageMatch = Regex.Match(functor,
                @"SetDisadvantage\s*\(\s*\)",
                RegexOptions.IgnoreCase);
            if (setDisadvantageMatch.Success)
            {
                return new EffectDefinition
                {
                    Type = "set_disadvantage"
                };
            }

            // If we couldn't parse it, log a warning but don't fail
            if (!IsIgnorableFunc(functor))
            {
                RuntimeSafety.LogError($"[SpellEffectConverter] Could not parse functor: {functor}");
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
                RuntimeSafety.Log($"[SpellEffectConverter] Warning: Formula contains division: {formula}");
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
                "CastOffhand",
                "CameraWait"
            };

            foreach (var func in ignorableFuncs)
            {
                if (functor.StartsWith(func, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true when a functor name is supported by the converter.
        /// Used by parity/functor coverage gates.
        /// </summary>
        public static bool SupportsFunctorName(string functorName)
        {
            if (string.IsNullOrWhiteSpace(functorName))
                return false;

            return functorName.Trim().ToLowerInvariant() switch
            {
                "dealdamage" => true,
                "applystatus" => true,
                "regainhitpoints" => true,
                "heal" => true,
                "removestatus" => true,
                "force" => true,
                "teleport" => true,
                "summon" => true,
                "summoncreature" => true,
                "createsurface" => true,
                "restoreresource" => true,
                "breakconcentration" => true,
                "gaintemporaryhitpoints" => true,
                "createexplosion" => true,
                "switchdeathtype" => true,
                "executeweaponfunctors" => true,
                "surfacechange" => true,
                "stabilize" => true,
                "resurrect" => true,
                "removestatusbygroup" => true,
                "counterspell" => true,
                "setadvantage" => true,
                "setdisadvantage" => true,
                "spawnextraprojectiles" => true,
                "applyequipmentstatus" => true,
                "douse" => true,
                "spawnininventory" => true,
                "fireprojectile" => true,
                "equalize" => true,
                "setstatusduration" => true,
                "pickupentity" => true,
                "swapplaces" => true,
                "createzonecloud" => true,
                "grant" => true,
                "usespell" => true,
                "castoffhand" => true,
                "camerawait" => true,
                _ => false
            };
        }

        #endregion
    }
}
