using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;

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

            var statusDef = new StatusDefinition
            {
                Id = bg3Status.StatusId?.ToLowerInvariant() ?? "unknown",
                Name = bg3Status.DisplayName ?? bg3Status.StatusId ?? "Unknown",
                Description = bg3Status.Description ?? "",
                Icon = bg3Status.Icon ?? ""
            };

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
        /// Map BG3 RemoveEvents to DurationType.UntilEvent and RemoveOnEvent.
        /// </summary>
        private static void MapRemoveEvents(BG3StatusData bg3Status, StatusDefinition statusDef)
        {
            if (string.IsNullOrWhiteSpace(bg3Status.RemoveEvents))
                return;

            var removeEvent = bg3Status.RemoveEvents.Trim().ToLowerInvariant();

            var mappedEvent = removeEvent switch
            {
                "onturn" => RuleEventType.TurnEnded,
                "onmove" => RuleEventType.MovementCompleted,
                "ondamage" => RuleEventType.DamageTaken,
                "onattack" => RuleEventType.AttackDeclared,
                "oncast" => RuleEventType.AbilityDeclared,
                _ => (RuleEventType?)null
            };

            if (mappedEvent.HasValue)
            {
                statusDef.DurationType = DurationType.UntilEvent;
                statusDef.RemoveOnEvent = mappedEvent.Value;
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

                // AC(N) - flat AC bonus
                var acMatch = Regex.Match(trimmed, @"AC\s*\(\s*(-?\d+)\s*\)", RegexOptions.IgnoreCase);
                if (acMatch.Success)
                {
                    int.TryParse(acMatch.Groups[1].Value, out var value);
                    statusDef.Modifiers.Add(new StatusModifier
                    {
                        Target = ModifierTarget.ArmorClass,
                        Type = ModifierType.Flat,
                        Value = value
                    });
                    continue;
                }

                // Advantage(AttackRoll)
                var advantageMatch = Regex.Match(trimmed, @"Advantage\s*\(\s*(\w+)\s*\)", RegexOptions.IgnoreCase);
                if (advantageMatch.Success)
                {
                    var target = ParseModifierTarget(advantageMatch.Groups[1].Value);
                    if (target.HasValue)
                    {
                        statusDef.Modifiers.Add(new StatusModifier
                        {
                            Target = target.Value,
                            Type = ModifierType.Advantage
                        });
                    }
                    continue;
                }

                // Disadvantage(AttackRoll)
                var disadvantageMatch = Regex.Match(trimmed, @"Disadvantage\s*\(\s*(\w+)\s*\)", RegexOptions.IgnoreCase);
                if (disadvantageMatch.Success)
                {
                    var target = ParseModifierTarget(disadvantageMatch.Groups[1].Value);
                    if (target.HasValue)
                    {
                        statusDef.Modifiers.Add(new StatusModifier
                        {
                            Target = target.Value,
                            Type = ModifierType.Disadvantage
                        });
                    }
                    continue;
                }

                // Resistance(DamageType,Resistant) - add as tag
                var resistanceMatch = Regex.Match(trimmed, @"Resistance\s*\(\s*(\w+)\s*,\s*(\w+)\s*\)", RegexOptions.IgnoreCase);
                if (resistanceMatch.Success)
                {
                    var damageType = resistanceMatch.Groups[1].Value.ToLowerInvariant();
                    var resistType = resistanceMatch.Groups[2].Value.ToLowerInvariant();
                    statusDef.Tags.Add($"resistance:{damageType}");
                    continue;
                }

                // For unsupported boost types, just log and continue
                // Don't use Godot.GD.Print to avoid testhost crash
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
                "attackroll" => ModifierTarget.AttackRoll,
                "savingthrow" => ModifierTarget.SavingThrow,
                "ability" => ModifierTarget.SkillCheck,
                "skillcheck" => ModifierTarget.SkillCheck,
                "initiative" => ModifierTarget.Initiative,
                "damage" => ModifierTarget.DamageDealt,
                _ => null
            };
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
