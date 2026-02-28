using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using QDND.Combat.Actions;
using QDND.Data.CharacterModel;

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

                // GROUND: prefix in BG3 means "apply to the ground/surface tile".
                // Weapon/damage functors must be skipped — they would fire as unconditional damage.
                // Other functors (e.g. CreateSurface) carry meaningful effect data and must parse.
                if (Regex.IsMatch(trimmed,
                    @"^GROUND:\s*(DealDamage|ExecuteWeaponFunctors)\s*\(",
                    RegexOptions.IgnoreCase))
                    continue;
                // Strip GROUND: prefix so remaining functors (e.g. CreateSurface) parse normally.
                if (trimmed.StartsWith("GROUND:", StringComparison.OrdinalIgnoreCase))
                    trimmed = trimmed.Substring(7).TrimStart();

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

            // Guard against bracket-only artifacts from wrapper tokenization (e.g., "]").
            if (string.IsNullOrWhiteSpace(functor) ||
                functor.All(c => c == '[' || c == ']' || char.IsWhiteSpace(c)))
            {
                return null;
            }

            // DealDamage(0) — single-arg zero-damage form (used to trigger on-hit reactions)
            if (TryGetFunctorArguments(functor, "DealDamage", out var dealDamageArgs))
            {
                if (dealDamageArgs.Count == 1 &&
                    NormalizeFunctorToken(dealDamageArgs[0]).Equals("0", StringComparison.Ordinal))
                {
                    return new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "0",
                        DamageType = "none",
                        SaveTakesHalf = halfOnSave
                    };
                }

                // DealDamage(dice, damageType[, flags...]) — supports nested formulas such as:
                // DealDamage(max(1,1d6+UnarmedMeleeAbilityModifier),Piercing,Magical)
                if (dealDamageArgs.Count >= 2)
                {
                    string diceFormula = CleanDiceFormula(dealDamageArgs[0]);
                    string damageType = NormalizeFunctorToken(dealDamageArgs[1]).ToLowerInvariant();

                    if (!string.IsNullOrWhiteSpace(diceFormula) && !string.IsNullOrWhiteSpace(damageType))
                    {
                        return new EffectDefinition
                        {
                            Type = "damage",
                            DiceFormula = diceFormula,
                            DamageType = damageType,
                            SaveTakesHalf = halfOnSave,
                            Condition = null // Will be set by caller if needed
                        };
                    }
                }
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

            // Teleport(distance) or TeleportSource() — teleports caster/source
            var teleportMatch = Regex.Match(functor,
                @"Teleport(?:Source)?\s*\(\s*(\d+\.?\d*)?\s*\)",
                RegexOptions.IgnoreCase);
            if (teleportMatch.Success)
            {
                float distance = 0f;
                if (teleportMatch.Groups[1].Success)
                    float.TryParse(teleportMatch.Groups[1].Value, out distance);
                return new EffectDefinition
                {
                    Type = "teleport",
                    Value = distance
                };
            }

            // Summon(templateId[, duration[, extra...]]) - BG3 alias for SummonCreature
            // Duration may be -1 (permanent). Extra args (projectile templates etc.) are ignored.
            if (TryGetFunctorArguments(functor, "Summon", out var summonAliasArgs) && summonAliasArgs.Count >= 1)
            {
                var effect = new EffectDefinition
                {
                    Type = "summon",
                    Parameters = new Dictionary<string, object>
                    {
                        { "templateId", NormalizeFunctorToken(summonAliasArgs[0]) }
                    }
                };

                if (summonAliasArgs.Count >= 2 && TryParseIntArgument(summonAliasArgs[1], out var summonDuration))
                {
                    effect.StatusDuration = summonDuration;
                }

                if (summonAliasArgs.Count >= 3 && TryParseIntArgument(summonAliasArgs[2], out var summonHp))
                {
                    effect.Parameters["hp"] = summonHp;
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

            // SpawnExtraProjectiles(countOrProjectileId)
            // BG3 passes either an integer count or a projectile template name like Projectile_MainHandAttack
            var spawnExtraProjectilesMatch = Regex.Match(functor,
                @"SpawnExtraProjectiles\s*\(\s*([^)]+)\s*\)",
                RegexOptions.IgnoreCase);
            if (spawnExtraProjectilesMatch.Success)
            {
                string projectileArg = spawnExtraProjectilesMatch.Groups[1].Value.Trim();
                int.TryParse(projectileArg, out var count);
                return new EffectDefinition
                {
                    Type = "spawn_extra_projectiles",
                    Value = count > 0 ? count : 1,
                    Parameters = new Dictionary<string, object>
                    {
                        { "count", count > 0 ? count : 1 },
                        { "projectile_template", projectileArg }
                    }
                };
            }

            // ApplyEquipmentStatus([slot,] statusId, [chance,] [duration])
            // Accepts forms:
            //   ApplyEquipmentStatus(STATUS_ID, duration)
            //   ApplyEquipmentStatus(MainHand, STATUS_ID, chance, duration)
            //   ApplyEquipmentStatus(MainHand, STATUS_ID, 100, -1)
            if (TryGetFunctorArguments(functor, "ApplyEquipmentStatus", out var applyEquipStatusArgs) && applyEquipStatusArgs.Count >= 1)
            {
                // Slot qualifiers that may appear as first arg
                var slotQualifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "MainHand", "OffHand", "OffhandWeapon", "Armor", "Helmet",
                      "Gloves", "Boots", "Amulet", "Ring", "Ring1", "Ring2", "Melee", "Ranged" };

                int statusArgIdx = 0;
                if (applyEquipStatusArgs.Count > 1 && slotQualifiers.Contains(NormalizeFunctorToken(applyEquipStatusArgs[0])))
                    statusArgIdx = 1;

                string statusId = NormalizeFunctorToken(applyEquipStatusArgs[statusArgIdx]);
                int duration = -1;
                // Count numeric args after statusId to distinguish chance from duration
                int numericArgCount = 0;
                for (int i = statusArgIdx + 1; i < applyEquipStatusArgs.Count; i++)
                    if (TryParseIntArgument(applyEquipStatusArgs[i], out _)) numericArgCount++;

                // If only one numeric: it's the duration (no chance present)
                // If two or more numerics: first is chance, second is duration
                bool hasChanceArg = numericArgCount > 1;
                bool skippedChance = false;
                for (int i = statusArgIdx + 1; i < applyEquipStatusArgs.Count; i++)
                {
                    if (TryParseIntArgument(applyEquipStatusArgs[i], out var val))
                    {
                        if (hasChanceArg && !skippedChance) { skippedChance = true; continue; }
                        duration = val;
                        break;
                    }
                }

                return new EffectDefinition
                {
                    Type = "apply_status",
                    StatusId = statusId.ToLowerInvariant(),
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

            // SetStatusDuration([target,] statusId, duration)
            // Accepts:
            //   SetStatusDuration(STATUS_ID, duration)
            //   SetStatusDuration(SELF, STATUS_ID, duration)
            if (TryGetFunctorArguments(functor, "SetStatusDuration", out var setStatusDurArgs) && setStatusDurArgs.Count >= 2)
            {
                int statusIdx = 0;
                if (setStatusDurArgs.Count >= 3 && IsTargetQualifier(setStatusDurArgs[0]))
                    statusIdx = 1;

                string statusId = NormalizeFunctorToken(setStatusDurArgs[statusIdx]);
                int duration = 1;
                TryParseIntArgument(setStatusDurArgs[statusIdx + 1], out duration);

                return new EffectDefinition
                {
                    Type = "set_status_duration",
                    StatusId = statusId.ToLowerInvariant(),
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

            // SurfaceChange(surfaceType[, radius[, ...[, lifetime]]])
            // Handles both common 3-arg form and BG3 variants such as:
            // SurfaceChange(Daylight,100,0,100,15)
            if (TryGetFunctorArguments(functor, "SurfaceChange", out var surfaceChangeArgs) &&
                surfaceChangeArgs.Count >= 1)
            {
                string surfaceType = NormalizeFunctorToken(surfaceChangeArgs[0]).ToLowerInvariant();
                float radius = 0f;
                int lifetime = 0;
                bool foundRadius = false;
                int? lastInt = null;

                for (int i = 1; i < surfaceChangeArgs.Count; i++)
                {
                    if (!foundRadius &&
                        TryParseFloatArgument(surfaceChangeArgs[i], out var parsedRadius, out _))
                    {
                        radius = parsedRadius;
                        foundRadius = true;
                    }

                    if (TryParseIntArgument(surfaceChangeArgs[i], out var parsedInt))
                    {
                        lastInt = parsedInt;
                    }
                }

                if (lastInt.HasValue)
                {
                    lifetime = lastInt.Value;
                }

                return new EffectDefinition
                {
                    Type = "surface_change",
                    Value = radius,
                    StatusDuration = lifetime,
                    Parameters = new Dictionary<string, object>
                    {
                        { "surface_type", surfaceType }
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

            // Unsummon() — removes an active summon from the battlefield
            if (Regex.IsMatch(functor, @"^Unsummon\s*\(\s*\)$", RegexOptions.IgnoreCase))
            {
                return new EffectDefinition
                {
                    Type = "unsummon",
                    Parameters = new Dictionary<string, object>()
                };
            }

            // Spawn(templateId[, args...]) — spawns a creature or object (treated as summon)
            if (TryGetFunctorArguments(functor, "Spawn", out var spawnArgs) && spawnArgs.Count >= 1)
            {
                string templateId = NormalizeFunctorToken(spawnArgs[0]);
                if (!string.IsNullOrWhiteSpace(templateId))
                {
                    return new EffectDefinition
                    {
                        Type = "summon",
                        StatusDuration = -1,
                        Parameters = new Dictionary<string, object>
                        {
                            { "templateId", templateId }
                        }
                    };
                }
            }

            // If we couldn't parse it, log a warning but don't fail
            if (!IsIgnorableFunc(functor))
            {
                RuntimeSafety.LogWarning($"[SpellEffectConverter] Unsupported functor skipped: {functor}");
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
                // Remove cast-level wrappers, e.g. Cast3[IF(...):ApplyStatus(...)].
                var castWrapperMatch = Regex.Match(current,
                    @"^Cast\d+\[\s*(.+)$",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (castWrapperMatch.Success)
                {
                    current = castWrapperMatch.Groups[1].Value.Trim();
                    continue;
                }

                // Remove trailing wrapper brackets left after splitting.
                string unbracketed = current.TrimEnd(']');
                if (unbracketed.Length != current.Length)
                {
                    current = unbracketed.Trim();
                    continue;
                }

                // Remove BG3 qualifier prefixes: TARGET:, GROUND:, SELF:, SOURCE:, AOE:,
                // and AI-hint prefixes: AI_ONLY:, AI_IGNORE:, CAST:
                var prefixMatch = Regex.Match(current,
                    @"^(TARGET|GROUND|SELF|SOURCE|AOE|AI_ONLY|AI_IGNORE|CAST):\s*(.+)$",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
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
                var prefixMatch = Regex.Match(current,
                    @"^(TARGET|GROUND|SELF|SOURCE|AOE|AI_ONLY|AI_IGNORE|CAST):\s*(.+)$",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
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

            // HasStatus('statusId') — simple single-arg form
            var notHasStatusMatch = Regex.Match(normalized,
                @"^nothasstatus\('([^']+)'\)$", RegexOptions.IgnoreCase);
            if (notHasStatusMatch.Success)
                return $"requires_no_status:{notHasStatusMatch.Groups[1].Value}";

            var hasStatusMatch = Regex.Match(normalized,
                @"^hasstatus\('([^']+)'\)$", RegexOptions.IgnoreCase);
            if (hasStatusMatch.Success)
                return $"requires_status:{hasStatusMatch.Groups[1].Value}";

            // Detect HasStatus forms we didn't match — log so they're auditable
            if (Regex.IsMatch(normalized, @"(not)?hasstatus\("))
            {
                RuntimeSafety.LogError($"[SpellEffectConverter] Unhandled HasStatus form: {conditionExpression}");
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

            int nextIndex = closeIndex + 1;
            while (nextIndex < functor.Length && char.IsWhiteSpace(functor[nextIndex]))
                nextIndex++;

            if (nextIndex >= functor.Length)
                return false;

            // Supports both IF(cond):Functor(...) and IF(cond)Functor(...)
            if (functor[nextIndex] == ':')
            {
                innerFunctor = functor.Substring(nextIndex + 1).Trim();
            }
            else
            {
                innerFunctor = functor.Substring(nextIndex).Trim();
            }

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
            
            // Handle division in formulas like "3d6/2" or "MainMeleeWeapon/2"
            var divMatch = System.Text.RegularExpressions.Regex.Match(formula, @"^(.+)/(\d+)$");
            if (divMatch.Success)
            {
                formula = divMatch.Groups[1].Value;
                // Store the divisor as metadata if needed, but for now strip it
                // The damage multiplier should be handled at the effect level
                RuntimeSafety.Log($"[SpellEffectConverter] Stripped divisor /{divMatch.Groups[2].Value} from formula: {formula}");
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
                "CameraWait",
                "ShortRest",
                "UseActionResource",
                "Unlock",
                "SurfaceClearLayer",
                "DisarmWeapon",
                "DisarmAndStealWeapon",
                "ResetCombatTurn"
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
                "unsummon" => true,
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
        public static string ResolveDynamicFormula(string formula, QDND.Combat.Entities.Combatant caster, QDND.Data.CharacterModel.CharacterDataRegistry registry = null)
        {
            if (string.IsNullOrWhiteSpace(formula) || caster == null)
                return formula;

            string resolved = formula;

            // Replace SpellcastingAbilityModifier with actual value
            if (resolved.Contains("SpellcastingAbilityModifier", StringComparison.OrdinalIgnoreCase))
            {
                int spellMod = GetSpellcastingModifier(caster, registry);
                resolved = System.Text.RegularExpressions.Regex.Replace(
                    resolved,
                    @"SpellcastingAbilityModifier",
                    spellMod.ToString(),
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Replace SpellCastingAbility (alternative spelling)
            if (resolved.Contains("SpellCastingAbility", StringComparison.OrdinalIgnoreCase))
            {
                int spellMod = GetSpellcastingModifier(caster, registry);
                resolved = System.Text.RegularExpressions.Regex.Replace(
                    resolved,
                    @"SpellCastingAbility",
                    spellMod.ToString(),
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Replace MainMeleeWeaponDamageType with actual weapon damage type
            // NOTE: must be checked BEFORE MainMeleeWeapon to avoid partial prefix match
            if (resolved.Contains("MainMeleeWeaponDamageType", StringComparison.OrdinalIgnoreCase))
            {
                string weaponDmgType = GetWeaponDamageType(caster, isMelee: true);
                resolved = System.Text.RegularExpressions.Regex.Replace(
                    resolved,
                    @"MainMeleeWeaponDamageType",
                    weaponDmgType,
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

            // Replace MainRangedWeaponDamageType with actual weapon damage type
            // NOTE: must be checked BEFORE MainRangedWeapon to avoid partial prefix match
            if (resolved.Contains("MainRangedWeaponDamageType", StringComparison.OrdinalIgnoreCase))
            {
                string weaponDmgType = GetWeaponDamageType(caster, isMelee: false);
                resolved = System.Text.RegularExpressions.Regex.Replace(
                    resolved,
                    @"MainRangedWeaponDamageType",
                    weaponDmgType,
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

            // Replace OffhandMeleeWeaponDamageType with actual weapon damage type
            // NOTE: must be checked BEFORE OffhandMeleeWeapon to avoid partial prefix match
            if (resolved.Contains("OffhandMeleeWeaponDamageType", StringComparison.OrdinalIgnoreCase))
            {
                string offhandDmgType = GetOffhandWeaponDamageType(caster);
                resolved = System.Text.RegularExpressions.Regex.Replace(
                    resolved,
                    @"OffhandMeleeWeaponDamageType",
                    offhandDmgType,
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

            // Replace OffhandRangedWeaponDamageType with actual weapon damage type
            // NOTE: must be checked BEFORE OffhandRangedWeapon to avoid partial prefix match
            if (resolved.Contains("OffhandRangedWeaponDamageType", StringComparison.OrdinalIgnoreCase))
            {
                string offhandRangedDmgType = GetOffhandWeaponDamageType(caster);
                resolved = System.Text.RegularExpressions.Regex.Replace(
                    resolved,
                    @"OffhandRangedWeaponDamageType",
                    offhandRangedDmgType,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Replace OffhandRangedWeapon with offhand weapon dice
            if (resolved.Contains("OffhandRangedWeapon", StringComparison.OrdinalIgnoreCase))
            {
                string offhandRangedDice = GetOffhandWeaponDamageDice(caster);
                resolved = System.Text.RegularExpressions.Regex.Replace(
                    resolved,
                    @"OffhandRangedWeapon",
                    offhandRangedDice,
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
        private static int GetSpellcastingModifier(QDND.Combat.Entities.Combatant caster, QDND.Data.CharacterModel.CharacterDataRegistry registry = null)
        {
            if (caster == null)
                return 0;

            var resolved = caster.ResolvedCharacter;
            if (resolved?.Sheet?.ClassLevels != null && resolved.Sheet.ClassLevels.Count > 0)
            {
                if (registry != null)
                {
                    foreach (var cl in resolved.Sheet.ClassLevels)
                    {
                        var classDef = registry.GetClass(cl.ClassId);
                        if (!string.IsNullOrEmpty(classDef?.SpellcastingAbility) &&
                            Enum.TryParse<QDND.Data.CharacterModel.AbilityType>(classDef.SpellcastingAbility, true, out var ability))
                            return caster.GetAbilityModifier(ability);
                    }
                    return 0;
                }
                // Fallback if registry unavailable
                string primaryClass = resolved.Sheet.ClassLevels[0].ClassId.ToLowerInvariant();
                return primaryClass switch
                {
                    "wizard" => caster.GetAbilityModifier(AbilityType.Intelligence),
                    "cleric" or "druid" or "ranger" or "monk" => caster.GetAbilityModifier(AbilityType.Wisdom),
                    "bard" or "sorcerer" or "warlock" or "paladin" => caster.GetAbilityModifier(AbilityType.Charisma),
                    _ => Math.Max(caster.GetAbilityModifier(AbilityType.Intelligence),
                                  Math.Max(caster.GetAbilityModifier(AbilityType.Wisdom), caster.GetAbilityModifier(AbilityType.Charisma)))
                };
            }

            // Fallback: use highest mental stat
            return Math.Max(caster.GetAbilityModifier(AbilityType.Intelligence),
                           Math.Max(caster.GetAbilityModifier(AbilityType.Wisdom), caster.GetAbilityModifier(AbilityType.Charisma)));
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
        /// Get the damage type of the caster's equipped weapon.
        /// </summary>
        private static string GetWeaponDamageType(QDND.Combat.Entities.Combatant caster, bool isMelee)
        {
            var weapon = caster.MainHandWeapon;
            if (weapon == null)
                return "bludgeoning"; // Unarmed strike default

            if (isMelee && weapon.IsRanged && caster.OffHandWeapon?.IsRanged == false)
                weapon = caster.OffHandWeapon;
            else if (!isMelee && !weapon.IsRanged && caster.OffHandWeapon?.IsRanged == true)
                weapon = caster.OffHandWeapon;

            return weapon.DamageType.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Get the damage type of the caster's offhand weapon.
        /// Falls back to bludgeoning (BG3 unarmed default) if no offhand weapon is equipped.
        /// </summary>
        private static string GetOffhandWeaponDamageType(QDND.Combat.Entities.Combatant caster)
        {
            var offhand = caster.OffHandWeapon;
            if (offhand == null)
                return "bludgeoning"; // Unarmed strike default

            return offhand.DamageType.ToString().ToLowerInvariant();
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

            // Standard breakpoints: 1-4, 5-9, 10-16, 17+
            int index = level switch
            {
                <= 4 => 0,
                <= 9 => 1,
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
