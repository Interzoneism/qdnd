using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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

                // Preserve known conditional wrappers (e.g., IF(SpellFail())) so they don't become implicit always-true.
                var wrappedCondition = ExtractWrapperCondition(trimmed);

                // Skip conditional wrappers (IF, TARGET, GROUND, etc.) - extract inner content
                trimmed = UnwrapConditionals(trimmed);

                // Parse individual functor
                var effect = ParseSingleEffect(trimmed);
                if (effect != null)
                {
                    if (!string.IsNullOrWhiteSpace(wrappedCondition) && string.IsNullOrWhiteSpace(effect.Condition))
                    {
                        effect.Condition = wrappedCondition;
                    }

                    // Mark fail effects appropriately
                    if (isFailEffect)
                    {
                        if (string.IsNullOrWhiteSpace(effect.Condition))
                        {
                            effect.Condition = "on_miss";
                        }
                        
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

            // ParseSingleEffect may be called directly by tests/tools; unwrap simple wrappers first.
            functor = UnwrapConditionals(functor);

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
            // Also supports target-first and extra-arg variants:
            // - ApplyStatus(TARGET, STATUS, 100, 2)
            // - ApplyStatus(STATUS, 100, 2, extra...)
            if (TryGetFunctorArguments(functor, "ApplyStatus", out var applyStatusArgs))
            {
                int statusArgIndex = 0;
                if (applyStatusArgs.Count > 1 && IsTargetQualifier(applyStatusArgs[0]))
                {
                    statusArgIndex = 1;
                }

                if (statusArgIndex >= applyStatusArgs.Count)
                {
                    return null;
                }

                string statusId = NormalizeFunctorToken(applyStatusArgs[statusArgIndex]);
                if (string.IsNullOrWhiteSpace(statusId))
                {
                    return null;
                }

                int duration = 1;
                bool consumedChance = false;

                for (int i = statusArgIndex + 1; i < applyStatusArgs.Count; i++)
                {
                    if (!TryParseIntArgument(applyStatusArgs[i], out var parsedInt))
                    {
                        continue;
                    }

                    if (!consumedChance)
                    {
                        consumedChance = true;
                        continue;
                    }

                    duration = parsedInt;
                    break;
                }

                return new EffectDefinition
                {
                    Type = "apply_status",
                    StatusId = statusId.ToLowerInvariant(),
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
            // Also supports multi-arg variants (target-first/flags): RemoveStatus(TARGET, STATUS, ...)
            if (TryGetFunctorArguments(functor, "RemoveStatus", out var removeStatusArgs))
            {
                int statusArgIndex = 0;
                if (removeStatusArgs.Count > 1 && IsTargetQualifier(removeStatusArgs[0]))
                {
                    statusArgIndex = 1;
                }

                if (statusArgIndex >= removeStatusArgs.Count)
                {
                    return null;
                }

                string statusId = NormalizeFunctorToken(removeStatusArgs[statusArgIndex]);
                if (string.IsNullOrWhiteSpace(statusId))
                {
                    return null;
                }

                return new EffectDefinition
                {
                    Type = "remove_status",
                    StatusId = statusId.ToLowerInvariant()
                };
            }

            // Force(distance) - push effect
            // Also supports multi-arg variants like Force(TARGET, 6, ...)
            if (TryGetFunctorArguments(functor, "Force", out var forceArgs))
            {
                float distance = 0f;
                bool foundDistance = false;
                string direction = "away";

                foreach (var arg in forceArgs)
                {
                    if (!foundDistance && TryParseFloatArgument(arg, out var parsedDistance, out _))
                    {
                        distance = parsedDistance;
                        foundDistance = true;
                        continue;
                    }

                    string normalizedArg = NormalizeFunctorToken(arg).ToLowerInvariant();
                    if (normalizedArg.Contains("toward", StringComparison.OrdinalIgnoreCase) ||
                        normalizedArg.Contains("pull", StringComparison.OrdinalIgnoreCase))
                    {
                        direction = "toward";
                    }
                    else if (normalizedArg.Contains("away", StringComparison.OrdinalIgnoreCase) ||
                             normalizedArg.Contains("push", StringComparison.OrdinalIgnoreCase))
                    {
                        direction = "away";
                    }
                }

                if (!foundDistance)
                {
                    return null;
                }

                return new EffectDefinition
                {
                    Type = "forced_move",
                    Value = distance,
                    Parameters = new Dictionary<string, object> { { "direction", direction } }
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
            // Alias support: SummonInInventory(itemId[, count])
            List<string> inventoryArgs;
            if (TryGetFunctorArguments(functor, "SpawnInInventory", out inventoryArgs) ||
                TryGetFunctorArguments(functor, "SummonInInventory", out inventoryArgs))
            {
                if (inventoryArgs.Count == 0)
                {
                    return null;
                }

                string itemId = NormalizeFunctorToken(inventoryArgs[0]);
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    return null;
                }

                int count = 1;
                if (inventoryArgs.Count > 1)
                {
                    TryParseIntArgument(inventoryArgs[1], out count);
                }

                return new EffectDefinition
                {
                    Type = "spawn_inventory_item",
                    Value = count,
                    Parameters = new Dictionary<string, object>
                    {
                        { "item_id", itemId },
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
            // Supports percentage amounts: RestoreResource(Resource, 100%)
            if (TryGetFunctorArguments(functor, "RestoreResource", out var restoreResourceArgs) &&
                restoreResourceArgs.Count >= 2)
            {
                var resourceName = NormalizeFunctorToken(restoreResourceArgs[0]);
                if (string.IsNullOrWhiteSpace(resourceName))
                {
                    return null;
                }

                if (!TryParseFloatArgument(restoreResourceArgs[1], out var amount, out var isPercent))
                {
                    return null;
                }

                int level = 0;
                if (restoreResourceArgs.Count > 2)
                {
                    TryParseIntArgument(restoreResourceArgs[2], out level);
                }

                var parameters = new Dictionary<string, object>
                {
                    { "resource_name", resourceName.ToLowerInvariant() },
                    { "level", level }
                };

                if (isPercent)
                {
                    parameters["is_percent"] = true;
                }

                return new EffectDefinition
                {
                    Type = "restore_resource",
                    Value = amount,
                    Parameters = parameters
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

            // Resurrect([hp][, ...])
            // Supports 2-arg forms while preserving default behavior.
            if (TryGetFunctorArguments(functor, "Resurrect", out var resurrectArgs))
            {
                int hp = 1; // Default HP
                if (resurrectArgs.Count > 0 && TryParseIntArgument(resurrectArgs[0], out var parsedHP))
                {
                    hp = parsedHP;
                }

                var effect = new EffectDefinition
                {
                    Type = "resurrect",
                    Value = hp
                };

                if (resurrectArgs.Count > 1)
                {
                    effect.Parameters["arg2"] = NormalizeFunctorToken(resurrectArgs[1]);
                }

                return effect;
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
            if (string.IsNullOrWhiteSpace(functor))
                return functor;

            string current = functor.Trim();

            while (true)
            {
                // Remove prefixes like TARGET:, GROUND:, SELF:
                var prefixMatch = Regex.Match(current, @"^(TARGET|GROUND|SELF|SOURCE):\s*(.+)$", RegexOptions.IgnoreCase);
                if (prefixMatch.Success)
                {
                    current = prefixMatch.Groups[2].Value.Trim();
                    continue;
                }

                if (TryUnwrapIfCondition(current, out _, out var innerFunctor))
                {
                    current = innerFunctor;
                    continue;
                }

                break;
            }

            return current;
        }

        private static string ExtractWrapperCondition(string functor)
        {
            if (string.IsNullOrWhiteSpace(functor))
                return null;

            string current = functor.Trim();

            while (true)
            {
                var prefixMatch = Regex.Match(current, @"^(TARGET|GROUND|SELF|SOURCE):\s*(.+)$", RegexOptions.IgnoreCase);
                if (prefixMatch.Success)
                {
                    current = prefixMatch.Groups[2].Value.Trim();
                    continue;
                }

                if (!TryUnwrapIfCondition(current, out var conditionExpr, out var innerFunctor))
                {
                    return null;
                }

                var mapped = MapConditionExpression(conditionExpr);
                if (!string.IsNullOrWhiteSpace(mapped))
                {
                    return mapped;
                }

                current = innerFunctor;
            }
        }

        private static string MapConditionExpression(string conditionExpression)
        {
            if (string.IsNullOrWhiteSpace(conditionExpression))
                return null;

            string normalized = Regex.Replace(conditionExpression, @"\s+", string.Empty).ToLowerInvariant();

            if (normalized.Contains("spellfail()") ||
                normalized.Contains("damageflags.miss") ||
                normalized.Contains("hasdamageeffectflag(miss") ||
                normalized.Contains("hasdamageeffectflag(damageflags.miss"))
            {
                return "on_miss";
            }

            if (normalized.Contains("spellsuccess()") ||
                normalized.Contains("damageflags.hit") ||
                normalized.Contains("hasdamageeffectflag(hit") ||
                normalized.Contains("hasdamageeffectflag(damageflags.hit"))
            {
                return "on_hit";
            }

            if (normalized.Contains("failedsavingthrow") || normalized.Contains("savefail"))
            {
                return "on_save_fail";
            }

            if (normalized.Contains("savesuccess") ||
                normalized.Contains("passedsavingthrow") ||
                normalized.Contains("succeededsavingthrow"))
            {
                return "on_save_success";
            }

            return null;
        }

        private static bool TryUnwrapIfCondition(string functor, out string conditionExpression, out string innerFunctor)
        {
            conditionExpression = null;
            innerFunctor = functor;

            if (string.IsNullOrWhiteSpace(functor))
                return false;

            int index = 0;
            while (index < functor.Length && char.IsWhiteSpace(functor[index]))
                index++;

            if (index + 1 >= functor.Length ||
                !functor.AsSpan(index).StartsWith("IF", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            index += 2;
            while (index < functor.Length && char.IsWhiteSpace(functor[index]))
                index++;

            if (index >= functor.Length || functor[index] != '(')
                return false;

            int openIndex = index;
            int depth = 0;
            int closeIndex = -1;

            for (int i = openIndex; i < functor.Length; i++)
            {
                char c = functor[i];
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeIndex = i;
                        break;
                    }
                }
            }

            if (closeIndex <= openIndex)
                return false;

            conditionExpression = functor.Substring(openIndex + 1, closeIndex - openIndex - 1).Trim();

            int colonIndex = functor.IndexOf(':', closeIndex + 1);
            if (colonIndex < 0)
                return false;

            innerFunctor = functor.Substring(colonIndex + 1).Trim();
            return !string.IsNullOrWhiteSpace(innerFunctor);
        }

        private static bool TryGetFunctorArguments(string functor, string functorName, out List<string> args)
        {
            args = null;

            if (string.IsNullOrWhiteSpace(functor) || string.IsNullOrWhiteSpace(functorName))
                return false;

            string trimmed = functor.Trim();
            if (!trimmed.StartsWith(functorName, StringComparison.OrdinalIgnoreCase))
                return false;

            int index = functorName.Length;
            while (index < trimmed.Length && char.IsWhiteSpace(trimmed[index]))
                index++;

            if (index >= trimmed.Length || trimmed[index] != '(')
                return false;

            int openIndex = index;
            int depth = 0;
            int closeIndex = -1;

            for (int i = openIndex; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeIndex = i;
                        break;
                    }
                }
            }

            if (closeIndex < 0)
                return false;

            string trailing = trimmed.Substring(closeIndex + 1).Trim();
            if (!string.IsNullOrEmpty(trailing))
                return false;

            string argsSlice = trimmed.Substring(openIndex + 1, closeIndex - openIndex - 1);
            args = SplitArguments(argsSlice);
            return true;
        }

        private static List<string> SplitArguments(string argsSlice)
        {
            var args = new List<string>();
            var current = new StringBuilder();
            int depth = 0;
            char quote = '\0';

            for (int i = 0; i < argsSlice.Length; i++)
            {
                char c = argsSlice[i];

                if (quote != '\0')
                {
                    current.Append(c);
                    if (c == quote)
                    {
                        quote = '\0';
                    }
                    continue;
                }

                if (c == '\'' || c == '"')
                {
                    quote = c;
                    current.Append(c);
                    continue;
                }

                if (c == '(')
                {
                    depth++;
                    current.Append(c);
                    continue;
                }

                if (c == ')')
                {
                    depth = Math.Max(0, depth - 1);
                    current.Append(c);
                    continue;
                }

                if (c == ',' && depth == 0)
                {
                    args.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            args.Add(current.ToString().Trim());
            return args;
        }

        private static bool IsTargetQualifier(string value)
        {
            string normalized = NormalizeFunctorToken(value).ToLowerInvariant();
            return normalized is "target" or "targets" or "self" or "source" or "ground" or "context";
        }

        private static string NormalizeFunctorToken(string token)
        {
            if (token == null)
                return string.Empty;

            return token.Trim().Trim('\'', '"');
        }

        private static bool TryParseIntArgument(string token, out int value)
        {
            value = 0;
            string normalized = NormalizeFunctorToken(token);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ||
                   int.TryParse(normalized, out value);
        }

        private static bool TryParseFloatArgument(string token, out float value, out bool isPercent)
        {
            value = 0f;
            isPercent = false;

            string normalized = NormalizeFunctorToken(token);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            if (normalized.EndsWith("%", StringComparison.Ordinal))
            {
                isPercent = true;
                normalized = normalized.Substring(0, normalized.Length - 1).Trim();
            }

            return float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                   float.TryParse(normalized, out value);
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
                "summonininventory" => true,
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

        #region Dynamic Formula Resolution

        /// <summary>
        /// Resolve dynamic formulas at runtime based on caster properties.
        /// Replaces BG3-style formula variables with actual values.
        /// Examples:
        /// - "SpellcastingAbilityModifier" → caster's spellcasting modifier
        /// - "MainMeleeWeapon" → caster's weapon damage dice
        /// - "1d6+SpellcastingAbilityModifier" → "1d6+3" (if WIS mod is +3)
        /// </summary>
        public static string ResolveDynamicFormula(string formula, QDND.Combat.Entities.Combatant caster)
        {
            if (string.IsNullOrWhiteSpace(formula) || caster == null)
                return formula;

            string resolved = formula;

            // Replace SpellcastingAbilityModifier with actual value
            if (resolved.Contains("SpellcastingAbilityModifier", StringComparison.OrdinalIgnoreCase))
            {
                int spellMod = GetSpellcastingModifier(caster);
                resolved = System.Text.RegularExpressions.Regex.Replace(
                    resolved,
                    @"SpellcastingAbilityModifier",
                    spellMod.ToString(),
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Replace SpellCastingAbility (alternative spelling)
            if (resolved.Contains("SpellCastingAbility", StringComparison.OrdinalIgnoreCase))
            {
                int spellMod = GetSpellcastingModifier(caster);
                resolved = System.Text.RegularExpressions.Regex.Replace(
                    resolved,
                    @"SpellCastingAbility",
                    spellMod.ToString(),
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Replace MainMeleeWeapon with actual weapon dice
            if (resolved.Contains("MainMeleeWeapon", StringComparison.OrdinalIgnoreCase))
            {
                string weaponDice = GetWeaponDamageDice(caster, isMelee: true);
                resolved = System.Text.RegularExpressions.Regex.Replace(
                    resolved,
                    @"MainMeleeWeapon",
                    weaponDice,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Replace MainRangedWeapon with actual weapon dice
            if (resolved.Contains("MainRangedWeapon", StringComparison.OrdinalIgnoreCase))
            {
                string weaponDice = GetWeaponDamageDice(caster, isMelee: false);
                resolved = System.Text.RegularExpressions.Regex.Replace(
                    resolved,
                    @"MainRangedWeapon",
                    weaponDice,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Replace OffhandMeleeWeapon with offhand weapon dice
            if (resolved.Contains("OffhandMeleeWeapon", StringComparison.OrdinalIgnoreCase))
            {
                string offhandDice = GetOffhandWeaponDamageDice(caster);
                resolved = System.Text.RegularExpressions.Regex.Replace(
                    resolved,
                    @"OffhandMeleeWeapon",
                    offhandDice,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Replace CharacterLevel with actual level
            if (resolved.Contains("CharacterLevel", StringComparison.OrdinalIgnoreCase))
            {
                int level = caster.ResolvedCharacter?.Sheet?.TotalLevel ?? 1;
                resolved = System.Text.RegularExpressions.Regex.Replace(
                    resolved,
                    @"CharacterLevel",
                    level.ToString(),
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Replace ClassLevel with actual class level (use highest class level if multiclassed)
            if (resolved.Contains("ClassLevel", StringComparison.OrdinalIgnoreCase))
            {
                int classLevel = GetHighestClassLevel(caster);
                resolved = System.Text.RegularExpressions.Regex.Replace(
                    resolved,
                    @"ClassLevel",
                    classLevel.ToString(),
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Handle LevelMapValue(X) formulas - maps character level to values
            // Example: LevelMapValue(1d6:1d8:2d6) → 1d6 at L1-4, 1d8 at L5-10, 2d6 at L11+
            var levelMapMatch = System.Text.RegularExpressions.Regex.Match(resolved, 
                @"LevelMapValue\(([^)]+)\)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (levelMapMatch.Success)
            {
                string mapValues = levelMapMatch.Groups[1].Value;
                int level = caster.ResolvedCharacter?.Sheet?.TotalLevel ?? 1;
                string mappedValue = ResolveLevelMapValue(mapValues, level);
                resolved = resolved.Replace(levelMapMatch.Value, mappedValue);
            }

            return resolved;
        }

        /// <summary>
        /// Get the spellcasting ability modifier for a combatant.
        /// </summary>
        private static int GetSpellcastingModifier(QDND.Combat.Entities.Combatant caster)
        {
            if (caster?.Stats == null)
                return 0;

            // Determine spellcasting ability based on class
            var resolved = caster.ResolvedCharacter;
            if (resolved?.Sheet?.ClassLevels != null && resolved.Sheet.ClassLevels.Count > 0)
            {
                string primaryClass = resolved.Sheet.ClassLevels[0].ClassId.ToLowerInvariant();
                return primaryClass switch
                {
                    "wizard" => caster.Stats.IntelligenceModifier,
                    "cleric" or "druid" or "ranger" or "monk" => caster.Stats.WisdomModifier,
                    "bard" or "sorcerer" or "warlock" or "paladin" => caster.Stats.CharismaModifier,
                    _ => Math.Max(caster.Stats.IntelligenceModifier, 
                                  Math.Max(caster.Stats.WisdomModifier, caster.Stats.CharismaModifier))
                };
            }

            // Fallback: use highest mental stat
            return Math.Max(caster.Stats.IntelligenceModifier, 
                           Math.Max(caster.Stats.WisdomModifier, caster.Stats.CharismaModifier));
        }

        /// <summary>
        /// Get weapon damage dice for a combatant's equipped weapon.
        /// </summary>
        private static string GetWeaponDamageDice(QDND.Combat.Entities.Combatant caster, bool isMelee)
        {
            var weapon = caster.MainHandWeapon;
            if (weapon == null)
                return "1d4"; // Unarmed strike default

            // Choose correct weapon based on type
            if (isMelee && weapon.IsRanged && caster.OffHandWeapon?.IsRanged == false)
            {
                weapon = caster.OffHandWeapon;
            }
            else if (!isMelee && !weapon.IsRanged && caster.OffHandWeapon?.IsRanged == true)
            {
                weapon = caster.OffHandWeapon;
            }

            return weapon.DamageDice ?? "1d6";
        }

        /// <summary>
        /// Get offhand weapon damage dice.
        /// </summary>
        private static string GetOffhandWeaponDamageDice(QDND.Combat.Entities.Combatant caster)
        {
            var offhand = caster.OffHandWeapon;
            return offhand?.DamageDice ?? "1d4";
        }

        /// <summary>
        /// Get the highest class level for a combatant (for multiclass).
        /// </summary>
        private static int GetHighestClassLevel(QDND.Combat.Entities.Combatant caster)
        {
            var resolved = caster.ResolvedCharacter;
            if (resolved?.Sheet?.ClassLevels == null || resolved.Sheet.ClassLevels.Count == 0)
                return caster.ResolvedCharacter?.Sheet?.TotalLevel ?? 1;

            // Group by class and get the max level
            var classCounts = new Dictionary<string, int>();
            foreach (var cl in resolved.Sheet.ClassLevels)
            {
                if (!classCounts.ContainsKey(cl.ClassId))
                    classCounts[cl.ClassId] = 0;
                classCounts[cl.ClassId]++;
            }

            return classCounts.Values.Max();
        }

        /// <summary>
        /// Resolve a LevelMapValue formula to the appropriate value based on character level.
        /// Example: "1d6:1d8:2d6" at level 1 = "1d6", at level 5 = "1d8", at level 11 = "2d6"
        /// </summary>
        private static string ResolveLevelMapValue(string mapValues, int level)
        {
            var values = mapValues.Split(':');
            if (values.Length == 0)
                return "0";

            // Standard breakpoints: 1-4, 5-10, 11-16, 17+
            int index = level switch
            {
                <= 4 => 0,
                <= 10 => 1,
                <= 16 => 2,
                _ => 3
            };

            // Clamp to available values
            index = Math.Min(index, values.Length - 1);
            return values[index].Trim();
        }

        #endregion
    }
}
