using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Data.Descriptions;
using QDND.Data.Parsers;

namespace QDND.Data.Statuses
{
    /// <summary>
    /// Converts BG3StatusData (parsed from raw TXT files) into runtime StatusDefinition objects 
    /// that can be used by the StatusManager. Handles:
    /// - Functor parsing (OnApply/OnTick/OnRemove → TriggerEffects/TickEffects)
    /// - Boost parsing (BG3 Boosts string → StatusModifiers)
    /// - StatusGroup mapping (BG3 StatusGroups → Tags)
    /// - RemoveEvents mapping (BG3 RemoveEvents → DurationType.UntilEvent)
    /// - Duration mapping
    /// - StackType mapping
    /// </summary>
    public static class BG3StatusIntegration
    {
        /// <summary>
        /// Convert a BG3StatusData into a runtime StatusDefinition.
        /// </summary>
        public static StatusDefinition ConvertToStatusDefinition(BG3StatusData bg3Status)
        {
            if (bg3Status == null)
                throw new ArgumentNullException(nameof(bg3Status));

            var resolvedDisplayName = BG3DisplayNameResolver.Resolve(bg3Status.DisplayName, bg3Status.StatusId);
            var description = BG3DisplayNameResolver.IsLocalizationHandle(bg3Status.Description)
                ? ""
                : (bg3Status.Description ?? "");
            if (!string.IsNullOrEmpty(bg3Status.DescriptionParams) && !string.IsNullOrEmpty(description))
                description = DescriptionParamResolver.Resolve(description, bg3Status.DescriptionParams);

            var statusDef = new StatusDefinition
            {
                Id = bg3Status.StatusId?.ToLowerInvariant() ?? "unknown",
                Name = StatusPresentationPolicy.ResolveDisplayName(resolvedDisplayName, bg3Status.StatusId),
                Description = description,
                Icon = bg3Status.Icon ?? ""
            };

            StatusPresentationPolicy.ApplyStatusPropertyFlags(statusDef, bg3Status.StatusPropertyFlags);

            // Determine if this is a buff (BOOST type with beneficial effects)
            statusDef.IsBuff = bg3Status.StatusType == BG3StatusType.BOOST;

            // Map duration
            MapDuration(bg3Status, statusDef);

            // Map stacking behavior
            MapStackingBehavior(bg3Status, statusDef);

            // Map status groups to tags
            MapStatusGroups(bg3Status, statusDef);

            // Map RemoveEvents
            MapRemoveEvents(bg3Status, statusDef);

            // Handle INCAPACITATED status type
            if (bg3Status.StatusType == BG3StatusType.INCAPACITATED ||
                statusDef.Tags.Any(t => t.Contains("incapacitated")))
            {
                statusDef.BlockedActions.Add("*");
            }

            // Parse Boosts into modifiers
            ParseBoosts(bg3Status.Boosts, statusDef);

            // Parse functors into effects
            ParseFunctors(bg3Status, statusDef);

            // Parse RepeatSave from BG3 RemoveConditions
            if (bg3Status.RawProperties != null
                && bg3Status.RawProperties.TryGetValue("RemoveConditions", out var removeConditions)
                && !string.IsNullOrWhiteSpace(removeConditions))
            {
                var saveMatch = Regex.Match(removeConditions,
                    @"SavingThrow\(Ability\.(\w+)");
                if (saveMatch.Success)
                {
                    var abilityName = saveMatch.Groups[1].Value.ToUpperInvariant();
                    var abbrev = abilityName switch
                    {
                        "STRENGTH" => "STR",
                        "DEXTERITY" => "DEX",
                        "CONSTITUTION" => "CON",
                        "INTELLIGENCE" => "INT",
                        "WISDOM" => "WIS",
                        "CHARISMA" => "CHA",
                        _ => abilityName.Length >= 3 ? abilityName.Substring(0, 3) : abilityName
                    };
                    statusDef.RepeatSave = new SaveRepeatInfo
                    {
                        Save = abbrev,
                        DC = 13  // Default DC; will be overridden by caster's spell DC when available
                    };
                }
            }

            return statusDef;
        }

        /// <summary>
        /// Batch convert and register multiple BG3 statuses into a StatusManager.
        /// </summary>
        public static int RegisterBG3Statuses(StatusManager statusManager, IEnumerable<BG3StatusData> bg3Statuses)
        {
            if (statusManager == null)
                throw new ArgumentNullException(nameof(statusManager));

            if (bg3Statuses == null)
                return 0;

            int count = 0;
            foreach (var bg3Status in bg3Statuses)
            {
                try
                {
                    var statusDef = ConvertToStatusDefinition(bg3Status);
                    statusManager.RegisterStatus(statusDef);
                    count++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BG3StatusIntegration] Failed to convert status {bg3Status.StatusId}: {ex.Message}");
                }
            }

            return count;
        }

        #region Private Mapping Methods

        /// <summary>
        /// Map BG3 duration to StatusDefinition duration.
        /// </summary>
        private static void MapDuration(BG3StatusData bg3Status, StatusDefinition statusDef)
        {
            if (bg3Status.Duration == null)
            {
                statusDef.DefaultDuration = 3; // Default to 3 turns
                statusDef.DurationType = DurationType.Turns;
            }
            else if (bg3Status.Duration == -1 || bg3Status.Duration == 0)
            {
                statusDef.DurationType = DurationType.Permanent;
                statusDef.DefaultDuration = 0;
            }
            else
            {
                statusDef.DefaultDuration = bg3Status.Duration.Value;
                statusDef.DurationType = DurationType.Turns;
            }
        }

        /// <summary>
        /// Map BG3 StackType to StatusDefinition StackingBehavior.
        /// </summary>
        private static void MapStackingBehavior(BG3StatusData bg3Status, StatusDefinition statusDef)
        {
            if (string.IsNullOrWhiteSpace(bg3Status.StackType))
            {
                statusDef.Stacking = StackingBehavior.Refresh; // Default
                return;
            }

            statusDef.Stacking = bg3Status.StackType.ToLowerInvariant() switch
            {
                "stack" => StackingBehavior.Stack,
                "overwrite" => StackingBehavior.Replace,
                "additive" => StackingBehavior.Extend,
                _ => StackingBehavior.Refresh
            };
        }

        /// <summary>
        /// Map BG3 StatusGroups to tags.
        /// </summary>
        private static void MapStatusGroups(BG3StatusData bg3Status, StatusDefinition statusDef)
        {
            if (string.IsNullOrWhiteSpace(bg3Status.StatusGroups))
                return;

            var groups = bg3Status.StatusGroups.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var group in groups)
            {
                var tag = group.Trim().ToLowerInvariant();
                statusDef.Tags.Add(tag);
            }
        }

        /// <summary>
        /// Map BG3 RemoveEvents to DurationType.UntilEvent and RemoveOnEvents.
        /// </summary>
        private static void MapRemoveEvents(BG3StatusData bg3Status, StatusDefinition statusDef)
        {
            if (string.IsNullOrWhiteSpace(bg3Status.RemoveEvents))
                return;

            var events = bg3Status.RemoveEvents.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var ev in events)
            {
                var token = ev.Trim().ToLowerInvariant();
                var mappedEvent = token switch
                {
                    "onturn" => RuleEventType.TurnEnded,
                    "onmove" => RuleEventType.MovementCompleted,
                    "ondamage" => RuleEventType.DamageTaken,
                    "onattack" => RuleEventType.AttackDeclared,
                    "oncast" => RuleEventType.AbilityDeclared,
                    "onheal" => RuleEventType.HealingReceived,
                    _ => (RuleEventType?)null
                };

                if (!mappedEvent.HasValue)
                    continue;

                if (mappedEvent.Value == RuleEventType.HealingReceived)
                {
                    // OnHeal: use dedicated RemoveOnHeal flag — keep DurationType.Turns so tick effects still fire
                    statusDef.RemoveOnHeal = true;
                    continue;
                }

                statusDef.DurationType = DurationType.UntilEvent;
                if (!statusDef.RemoveOnEvents.Contains(mappedEvent.Value))
                    statusDef.RemoveOnEvents.Add(mappedEvent.Value);
            }
        }

        /// <summary>
        /// Parse BG3 Boosts string into StatusModifiers.
        /// Handles basic boost patterns: AC(N), Advantage(X), Disadvantage(X), Resistance(X,Y).
        /// </summary>
        private static void ParseBoosts(string boostsString, StatusDefinition statusDef)
        {
            if (string.IsNullOrWhiteSpace(boostsString))
                return;

            // Split by semicolon
            var boosts = boostsString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var boost in boosts)
            {
                var trimmed = boost.Trim();

                // AC(N) is handled by the boost pipeline at runtime.
                // Do not emit legacy status modifiers here to avoid double-applying AC bonuses.
                var acMatch = Regex.Match(trimmed, @"AC\s*\(\s*(-?\d+)\s*\)", RegexOptions.IgnoreCase);
                if (acMatch.Success)
                {
                    continue;
                }

                // Advantage(AttackRoll) or Advantage(AttackRoll, Ability)
                var advantageMatch = Regex.Match(trimmed,
                    @"Advantage\s*\(\s*(\w+)(?:\s*,\s*(\w+))?\s*\)", RegexOptions.IgnoreCase);
                if (advantageMatch.Success)
                {
                    var rawTarget = advantageMatch.Groups[1].Value;
                    if (IsAttackTargetVariant(rawTarget))
                    {
                        // AttackTarget means "attacks against me" in BG3; preserve as a status tag.
                        statusDef.Tags.Add("advantage:attacktarget");
                    }
                    else
                    {
                        var target = ParseModifierTarget(rawTarget);
                        if (target.HasValue)
                        {
                            var mod = new StatusModifier
                            {
                                Target = target.Value,
                                Type = ModifierType.Advantage
                            };
                            if (advantageMatch.Groups[2].Success)
                                mod.Condition = $"ability:{advantageMatch.Groups[2].Value.ToLowerInvariant()}";
                            statusDef.Modifiers.Add(mod);
                        }
                        else
                        {
                            Console.WriteLine($"[BG3StatusIntegration] Advantage: unresolved target '{rawTarget}' in: {trimmed}");
                        }
                    }
                    continue;
                }

                // Disadvantage(AttackRoll) or Disadvantage(AttackRoll, Ability)
                var disadvantageMatch = Regex.Match(trimmed,
                    @"Disadvantage\s*\(\s*(\w+)(?:\s*,\s*(\w+))?\s*\)", RegexOptions.IgnoreCase);
                if (disadvantageMatch.Success)
                {
                    var rawTarget = disadvantageMatch.Groups[1].Value;
                    if (IsAttackTargetVariant(rawTarget))
                    {
                        // AttackTarget means "attacks against me" in BG3; preserve as a status tag.
                        statusDef.Tags.Add("disadvantage:attacktarget");
                    }
                    else
                    {
                        var target = ParseModifierTarget(rawTarget);
                        if (target.HasValue)
                        {
                            var mod = new StatusModifier
                            {
                                Target = target.Value,
                                Type = ModifierType.Disadvantage
                            };
                            if (disadvantageMatch.Groups[2].Success)
                                mod.Condition = $"ability:{disadvantageMatch.Groups[2].Value.ToLowerInvariant()}";
                            statusDef.Modifiers.Add(mod);
                        }
                        else
                        {
                            Console.WriteLine($"[BG3StatusIntegration] Disadvantage: unresolved target '{rawTarget}' in: {trimmed}");
                        }
                    }
                    continue;
                }

                // Resistance(DamageType,Resistant) - add as tag
                var resistanceMatch = Regex.Match(trimmed, @"Resistance\s*\(\s*(\w+)\s*,\s*(\w+)\s*\)", RegexOptions.IgnoreCase);
                if (resistanceMatch.Success)
                {
                    var damageType = resistanceMatch.Groups[1].Value.ToLowerInvariant();
                    statusDef.Tags.Add($"resistance:{damageType}");
                    continue;
                }

                // CriticalHit(target, Success|Never, ...)
                var criticalHitMatch = Regex.Match(trimmed,
                    @"CriticalHit\s*\(\s*([^,\)]+)\s*,\s*(Success|Never)(?:\s*,\s*[^,\)]*(?:\s*,\s*([^,\)]+))?)?\s*\)",
                    RegexOptions.IgnoreCase);
                if (criticalHitMatch.Success)
                {
                    var target = NormalizeBoostTargetToken(criticalHitMatch.Groups[1].Value);
                    var mode = criticalHitMatch.Groups[2].Value.ToLowerInvariant();
                    var range = criticalHitMatch.Groups[3].Success
                        ? criticalHitMatch.Groups[3].Value.Trim()
                        : "";
                    var tag = string.IsNullOrEmpty(range)
                        ? $"critical_hit:{target}:{mode}"
                        : $"critical_hit:{target}:{mode}:{range}";
                    statusDef.Tags.Add(tag);
                    continue;
                }

                // Attribute(Name)
                var attributeMatch = Regex.Match(trimmed, @"Attribute\s*\(\s*([^\)]+)\s*\)", RegexOptions.IgnoreCase);
                if (attributeMatch.Success)
                {
                    var attributeName = attributeMatch.Groups[1].Value.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(attributeName))
                    {
                        statusDef.Tags.Add($"attribute:{attributeName}");
                    }
                    continue;
                }

                // StatusImmunity(StatusId)
                var statusImmunityMatch = Regex.Match(trimmed, @"StatusImmunity\s*\(\s*([^\)]+)\s*\)", RegexOptions.IgnoreCase);
                if (statusImmunityMatch.Success)
                {
                    var statusId = statusImmunityMatch.Groups[1].Value.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(statusId))
                    {
                        statusDef.Tags.Add($"status_immunity:{statusId}");
                    }
                    continue;
                }

                // MovementSpeedLimit(Mode)
                var movementSpeedLimitMatch = Regex.Match(trimmed, @"MovementSpeedLimit\s*\(\s*([^\)]+)\s*\)", RegexOptions.IgnoreCase);
                if (movementSpeedLimitMatch.Success)
                {
                    var movementMode = movementSpeedLimitMatch.Groups[1].Value.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(movementMode))
                    {
                        statusDef.Tags.Add($"movement_speed_limit:{movementMode}");
                    }
                    continue;
                }

                // DarkvisionRangeMin(N)
                var darkvisionRangeMinMatch = Regex.Match(trimmed, @"DarkvisionRangeMin\s*\(\s*(-?\d+)\s*\)", RegexOptions.IgnoreCase);
                if (darkvisionRangeMinMatch.Success)
                {
                    int.TryParse(darkvisionRangeMinMatch.Groups[1].Value, out var range);
                    statusDef.Tags.Add($"darkvision_range_min:{range}");
                    continue;
                }

                // Invisibility()
                var invisibilityMatch = Regex.Match(trimmed, @"Invisibility\s*\(\s*\)", RegexOptions.IgnoreCase);
                if (invisibilityMatch.Success)
                {
                    statusDef.Tags.Add("attribute:invisibility");
                    continue;
                }

                // For unsupported boost types, just log and continue
                // Don't use Godot.GD.Print to avoid testhost crash
                // BlockAbilityModifierFromAC(Dexterity) - mark status to exclude DEX from AC
                var blockAcMatch = Regex.Match(trimmed, @"BlockAbilityModifierFromAC\s*\(\s*Dexterity\s*\)", RegexOptions.IgnoreCase);
                if (blockAcMatch.Success)
                {
                    statusDef.BlockDexFromAC = true;
                    continue;
                }

                var actionResourceBlockMatch = Regex.Match(trimmed, @"ActionResourceBlock\s*\(\s*(ActionPoint|BonusActionPoint|ReactionActionPoint|Movement)\s*\)", RegexOptions.IgnoreCase);
                if (actionResourceBlockMatch.Success)
                {
                    string resource = actionResourceBlockMatch.Groups[1].Value;
                    string blockedToken = resource.ToLowerInvariant() switch
                    {
                        "actionpoint" => "action",
                        "bonusactionpoint" => "bonus_action",
                        "reactionactionpoint" => "reaction",
                        "movement" => "movement",
                        _ => null
                    };

                    if (!string.IsNullOrEmpty(blockedToken))
                    {
                        statusDef.BlockedActions.Add(blockedToken);
                    }

                    continue;
                }

                var ignoreLeaveAttackRangeMatch = Regex.Match(trimmed, @"IgnoreLeaveAttackRange\s*\(?.*\)?", RegexOptions.IgnoreCase);
                if (ignoreLeaveAttackRangeMatch.Success)
                {
                    statusDef.Tags.Add("ignore_leave_attack_range");
                    continue;
                }

                var failedSavingThrowMatch = Regex.Match(trimmed, @"AbilityFailedSavingThrow\s*\(\s*(Strength|Dexterity)\s*\)", RegexOptions.IgnoreCase);
                if (failedSavingThrowMatch.Success)
                {
                    string ability = failedSavingThrowMatch.Groups[1].Value;
                    if (ability.Equals("Strength", StringComparison.OrdinalIgnoreCase))
                    {
                        statusDef.Tags.Add("auto_fail_save_strength");
                    }
                    else if (ability.Equals("Dexterity", StringComparison.OrdinalIgnoreCase))
                    {
                        statusDef.Tags.Add("auto_fail_save_dexterity");
                    }

                    continue;
                }

                // ActionResource(ActionPoint,N,0) — extra action charges (e.g. Haste)
                // ActionResource(Movement,N,0) — flat movement bonus
                var actionResourceMatch = Regex.Match(trimmed,
                    @"ActionResource\s*\(\s*(ActionPoint|Movement)\s*,\s*(-?[\d.]+)\s*,\s*\d+\s*\)",
                    RegexOptions.IgnoreCase);
                if (actionResourceMatch.Success)
                {
                    string resourceType = actionResourceMatch.Groups[1].Value;
                    float.TryParse(actionResourceMatch.Groups[2].Value,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float resourceAmount);

                    if (resourceType.Equals("ActionPoint", StringComparison.OrdinalIgnoreCase))
                    {
                        statusDef.ExtraActionCharges += (int)resourceAmount;
                    }
                    else // Movement
                    {
                        statusDef.Modifiers.Add(new StatusModifier
                        {
                            Target = ModifierTarget.MovementSpeed,
                            Type = ModifierType.Flat,
                            Value = resourceAmount
                        });
                    }
                    continue;
                }

                Console.WriteLine($"[BG3StatusIntegration] Unsupported boost: {trimmed}");
            }
        }

        /// <summary>
        /// Parse BG3 modifier target string to ModifierTarget enum.
        /// </summary>
        private static ModifierTarget? ParseModifierTarget(string targetStr)
        {
            if (string.IsNullOrWhiteSpace(targetStr))
                return null;

            return targetStr.ToLowerInvariant() switch
            {
                "attack" => ModifierTarget.AttackRoll,
                "attackroll" => ModifierTarget.AttackRoll,
                "savingthrow" => ModifierTarget.SavingThrow,
                "ability" => ModifierTarget.SkillCheck,
                "skillcheck" => ModifierTarget.SkillCheck,
                "initiative" => ModifierTarget.Initiative,
                "damage" => ModifierTarget.DamageDealt,
                // Extended BG3 target aliases
                "allsavingthrows" => ModifierTarget.SavingThrow,
                "allabilities" => ModifierTarget.SkillCheck,
                "allabilitycheck" => ModifierTarget.SkillCheck,
                _ => null
            };
        }

        private static bool IsAttackTargetVariant(string targetStr)
        {
            return string.Equals(targetStr?.Trim(), "AttackTarget", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeBoostTargetToken(string targetToken)
        {
            if (string.IsNullOrWhiteSpace(targetToken))
                return "attack";

            return targetToken.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Parse OnApply/OnTick/OnRemove functors into StatusDefinition effects.
        /// </summary>
        private static void ParseFunctors(BG3StatusData bg3Status, StatusDefinition statusDef)
        {
            // Parse OnApply functors
            if (!string.IsNullOrWhiteSpace(bg3Status.OnApplyFunctors))
            {
                var onApplyEffects = StatusFunctorEngine.ParseOnApplyFunctors(bg3Status.OnApplyFunctors);
                statusDef.TriggerEffects.AddRange(onApplyEffects);
            }

            // Parse OnTick functors
            if (!string.IsNullOrWhiteSpace(bg3Status.OnTickFunctors))
            {
                var (onTickEffects, onTickTriggerEffects) = StatusFunctorEngine.ParseOnTickFunctors(bg3Status.OnTickFunctors);
                statusDef.TickEffects.AddRange(onTickEffects);
                statusDef.TriggerEffects.AddRange(onTickTriggerEffects);
            }

            // Parse OnRemove functors
            if (!string.IsNullOrWhiteSpace(bg3Status.OnRemoveFunctors))
            {
                var onRemoveEffects = StatusFunctorEngine.ParseOnRemoveFunctors(bg3Status.OnRemoveFunctors);
                statusDef.TriggerEffects.AddRange(onRemoveEffects);
            }
        }

        #endregion
    }
}
