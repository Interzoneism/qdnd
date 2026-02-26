using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Data.CharacterModel;
using QDND.Combat.Environment;
using QDND.Combat.Statuses;

namespace QDND.Combat.Targeting
{
    /// <summary>
    /// Result of target validation.
    /// </summary>
    public class TargetValidation
    {
        public bool IsValid { get; set; }
        public string Reason { get; set; }
        public List<Combatant> ValidTargets { get; set; } = new();
        public List<(Combatant Target, string Reason)> InvalidTargets { get; set; } = new();

        public static TargetValidation Valid(List<Combatant> targets)
        {
            return new TargetValidation
            {
                IsValid = true,
                ValidTargets = targets
            };
        }

        public static TargetValidation Invalid(string reason)
        {
            return new TargetValidation
            {
                IsValid = false,
                Reason = reason
            };
        }
    }

    /// <summary>
    /// Validates and resolves ability targets.
    /// </summary>
    public class TargetValidator
    {
        private readonly LOSService _losService;
        private readonly Func<Combatant, Vector3> _getPosition;
        private readonly Random _rng = new Random();

        /// <summary>
        /// Optional status manager for sanctuary and other status-based targeting checks.
        /// </summary>
        public StatusManager Statuses { get; set; }

        /// <summary>
        /// Create a validator without LOS checking.
        /// </summary>
        public TargetValidator()
        {
            _losService = null;
            _getPosition = null;
        }

        /// <summary>
        /// Create a validator with LOS checking.
        /// </summary>
        /// <param name="losService">Service for line of sight calculations.</param>
        /// <param name="getPosition">Function to get combatant position.</param>
        public TargetValidator(LOSService losService, Func<Combatant, Vector3> getPosition)
        {
            _losService = losService;
            _getPosition = getPosition;
        }

        /// <summary>
        /// Validate targets for an action.
        /// </summary>
        public TargetValidation Validate(
            Actions.ActionDefinition action,
            Combatant source,
            List<Combatant> selectedTargets,
            List<Combatant> allCombatants)
        {
            var result = new TargetValidation { IsValid = true };

            // Handle self-targeting
            if (action.TargetType == Actions.TargetType.Self)
            {
                if (!selectedTargets.Contains(source))
                    selectedTargets = new List<Combatant> { source };
                result.ValidTargets = selectedTargets;
                return result;
            }

            // Handle no-target abilities
            if (action.TargetType == Actions.TargetType.None)
            {
                result.ValidTargets = new List<Combatant>();
                return result;
            }

            // Validate each selected target
            foreach (var target in selectedTargets)
            {
                var validation = ValidateSingleTarget(action, source, target);
                if (validation.IsValid)
                {
                    // Check LOS if service is available
                    if (!HasLineOfSight(source, target))
                    {
                        result.InvalidTargets.Add((target, "No line of sight to target"));
                    }
                    else
                    {
                        result.ValidTargets.Add(target);
                    }
                }
                else
                {
                    result.InvalidTargets.Add((target, validation.Reason));
                }
            }

            // Check target count
            if (result.ValidTargets.Count == 0)
            {
                result.IsValid = false;
                result.Reason = "No valid targets selected";
                return result;
            }

            if (result.ValidTargets.Count > action.MaxTargets)
            {
                result.ValidTargets = result.ValidTargets.Take(action.MaxTargets).ToList();
            }

            return result;
        }

        /// <summary>
        /// Validate a single target.
        /// </summary>
        public TargetValidation ValidateSingleTarget(
            Actions.ActionDefinition action,
            Combatant source,
            Combatant target)
        {
            // Check target life state compatibility for this action
            if (!IsTargetStateAllowed(action, target))
                return TargetValidation.Invalid("Target is incapacitated");

            // Check faction filter
            if (!IsValidFaction(action.TargetFilter, source, target))
                return TargetValidation.Invalid("Invalid target faction");

            // Check required tags
            if (!HasRequiredTags(action, target))
                return TargetValidation.Invalid($"Target missing required tags: {string.Join(", ", action.RequiredTags)}");

            // Shove size restriction: cannot shove a target more than one size larger.
            if (IsShoveAction(action) && !IsValidShoveSize(source, target))
                return TargetValidation.Invalid("Target is too large to shove");

            // Range check using position data
            // Melee attacks get a tolerance for character body radius positioning
            if (action.Range > 0)
            {
                float distance;
                if (IsShoveAction(action))
                {
                    distance = new Vector3(
                        source.Position.X - target.Position.X,
                        0,
                        source.Position.Z - target.Position.Z
                    ).Length();
                }
                else
                {
                    distance = source.Position.DistanceTo(target.Position);
                }

                bool isMelee = IsShoveAction(action) ||
                               action.AttackType == Actions.AttackType.MeleeWeapon ||
                               action.AttackType == Actions.AttackType.MeleeSpell;
                float tolerance = isMelee ? 0.75f : 0.5f;  // Body radius tolerance
                if (distance > action.Range + tolerance)
                    return TargetValidation.Invalid($"Target out of range ({distance:F1}/{action.Range + tolerance:F1})");
            }

            // Sanctuary check: BG3 Sanctuary is a hard targeting block via CannotHarmCauseEntity.
            // The protected creature cannot be targeted by hostile actions at all.
            // Sanctuary is removed when the protected creature attacks (handled by passive system).
            if (Statuses != null
                && source.Id != target.Id
                && source.Faction != target.Faction
                && Statuses.HasStatus(target.Id, "sanctuary"))
            {
                return TargetValidation.Invalid("Target is protected by Sanctuary");
            }

            return TargetValidation.Valid(new List<Combatant> { target });
        }

        /// <summary>
        /// Get all valid targets for an action.
        /// </summary>
        public List<Combatant> GetValidTargets(
            Actions.ActionDefinition action,
            Combatant source,
            List<Combatant> allCombatants)
        {
            if (action.TargetType == Actions.TargetType.Self)
                return new List<Combatant> { source };

            if (action.TargetType == Actions.TargetType.None)
                return new List<Combatant>();

            return allCombatants
                .Where(c => IsTargetStateAllowed(action, c))
                .Where(c => IsValidFaction(action.TargetFilter, source, c))
                .Where(c => HasRequiredTags(action, c))
                .Where(c => !IsShoveAction(action) || IsValidShoveSize(source, c))
                .Where(c => HasLineOfSight(source, c))
                .Where(c => IsInAbilityRange(source, c, action.Range))
                .ToList();
        }

        /// <summary>
        /// Shove can only target creatures up to one size category larger than the source.
        /// </summary>
        public static bool IsValidShoveSize(Combatant source, Combatant target)
        {
            if (source == null || target == null)
                return false;

            return (int)target.CreatureSize <= (int)source.CreatureSize + 1;
        }

        /// <summary>
        /// Check if target is within ability range.
        /// </summary>
        private bool IsInAbilityRange(Combatant source, Combatant target, float range)
        {
            // Range 0 means unlimited/melee touch
            if (range <= 0)
                return true;

            float distance = source.Position.DistanceTo(target.Position);
            return distance <= range;
        }

        private static bool IsShoveAction(Actions.ActionDefinition action)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.Id))
                return false;

            return action.Id.IndexOf("shove", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Check if target faction matches filter.
        /// </summary>
        public bool IsValidFaction(Actions.TargetFilter filter, Combatant source, Combatant target)
        {
            if (filter == Actions.TargetFilter.All)
                return true;

            bool isSelf = source.Id == target.Id;
            bool isAlly = source.Faction == target.Faction;
            bool isEnemy = !isAlly && target.Faction != Faction.Neutral;
            bool isNeutral = target.Faction == Faction.Neutral;

            if (isSelf && filter.HasFlag(Actions.TargetFilter.Self))
                return true;
            if (isAlly && !isSelf && filter.HasFlag(Actions.TargetFilter.Allies))
                return true;
            if (isEnemy && filter.HasFlag(Actions.TargetFilter.Enemies))
                return true;
            if (isNeutral && filter.HasFlag(Actions.TargetFilter.Neutrals))
                return true;

            return false;
        }

        /// <summary>
        /// Check if target is within ability range of source.
        /// </summary>
        public bool IsInRange(Combatant source, Combatant target, float range)
        {
            float distance = source.Position.DistanceTo(target.Position);
            return distance <= range;
        }

        /// <summary>
        /// Check if target has all required tags for an action.
        /// Returns true if ability has no required tags or target has all required tags.
        /// </summary>
        private bool HasRequiredTags(Actions.ActionDefinition action, Combatant target)
        {
            // If no required tags specified, any target is valid
            if (action.RequiredTags == null || action.RequiredTags.Count == 0)
                return true;

            // Check if target has all required tags
            if (target.Tags == null)
                return false;

            return action.RequiredTags.All(requiredTag => 
                target.Tags.Any(targetTag => targetTag.Equals(requiredTag, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Check whether a target's life state can be targeted by the given action.
        /// Keeps dead/downed invalid by default unless explicitly allowed by effect semantics.
        /// </summary>
        private bool IsTargetStateAllowed(Actions.ActionDefinition action, Combatant target)
        {
            if (target == null)
                return false;

            if (target.ParticipationState != CombatantParticipationState.InFight)
                return false;

            if (target.LifeState == CombatantLifeState.Alive)
                return true;

            bool allowsStabilize = ActionHasEffectType(action, "stabilize");
            bool allowsResurrectOrRevive = ActionHasEffectType(action, "resurrect") || ActionHasEffectType(action, "revive");
            bool allowsHeal = ActionHasEffectType(action, "heal");
            bool allowsRemoveDowned = ActionRemovesStatus(action, "downed");

            return target.LifeState switch
            {
                CombatantLifeState.Dead => allowsResurrectOrRevive,
                CombatantLifeState.Downed => allowsStabilize || allowsResurrectOrRevive || allowsHeal || allowsRemoveDowned,
                CombatantLifeState.Unconscious => allowsResurrectOrRevive,
                _ => false
            };
        }

        private static bool ActionHasEffectType(Actions.ActionDefinition action, string effectType)
        {
            return action?.Effects?.Any(effect => effect != null &&
                string.Equals(effect.Type, effectType, StringComparison.OrdinalIgnoreCase)) == true;
        }

        private static bool ActionRemovesStatus(Actions.ActionDefinition action, string statusId)
        {
            return action?.Effects?.Any(effect =>
                effect != null &&
                string.Equals(effect.Type, "remove_status", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(effect.StatusId, statusId, StringComparison.OrdinalIgnoreCase)) == true;
        }

        /// <summary>
        /// Resolve area targets (for AoE abilities).
        /// </summary>
        public List<Combatant> ResolveAreaTargets(
            Actions.ActionDefinition action,
            Combatant source,
            Godot.Vector3 targetPoint,
            List<Combatant> allCombatants,
            Func<Combatant, Godot.Vector3> getPosition,
            Godot.Vector3? wallStart = null)
        {
            var targets = new List<Combatant>();
            var sourcePosition = getPosition(source);
            var lineOfEffectOrigin = ResolveLineOfEffectOrigin(action, sourcePosition, targetPoint, wallStart);

            foreach (var combatant in allCombatants.Where(c => IsTargetStateAllowed(action, c)))
            {
                bool validFaction = IsValidFaction(action.TargetFilter, source, combatant);
                if (!validFaction)
                    continue;

                bool hasRequiredTags = HasRequiredTags(action, combatant);
                if (!hasRequiredTags)
                    continue;

                var pos = getPosition(combatant);
                float distance = pos.DistanceTo(targetPoint);

                bool inArea = action.TargetType switch
                {
                    Actions.TargetType.Circle => distance <= action.AreaRadius,
                    Actions.TargetType.Cone => IsInCone(sourcePosition, targetPoint, pos, action.ConeAngle, action.Range),
                    Actions.TargetType.Line => IsOnLine(sourcePosition, targetPoint, pos, action.LineWidth),
                    Actions.TargetType.WallSegment => wallStart.HasValue && IsOnLine(wallStart.Value, targetPoint, pos, action.LineWidth),
                    Actions.TargetType.Point => distance <= Mathf.Max(0.25f, action.AreaRadius),
                    _ => false
                };

                if (!inArea)
                    continue;

                // Circle/Point effects use cast point as center; Cone/Line should use source origin.
                bool hasEffect = HasLineOfEffectFromPoint(lineOfEffectOrigin, combatant, getPosition);
                if (hasEffect)
                {
                    targets.Add(combatant);
                }
            }

            return targets;
        }

        /// <summary>
        /// Check if source has line of sight to target.
        /// Always returns true for self-targeting or if LOS service is not configured.
        /// </summary>
        private bool HasLineOfSight(Combatant source, Combatant target)
        {
            // Self-targeting always has LOS
            if (source.Id == target.Id)
                return true;

            // No LOS service configured - skip LOS check
            if (_losService == null)
                return true;

            return _losService.HasLineOfSight(source, target);
        }

        /// <summary>
        /// Check if there's line of effect from a point to a combatant.
        /// Used for AoE effects centered on a point.
        /// </summary>
        private bool HasLineOfEffectFromPoint(Vector3 point, Combatant target, Func<Combatant, Vector3> getPosition)
        {
            // No LOS service configured - skip LOS check
            if (_losService == null)
                return true;

            // Use the provided position getter (from method parameter) if available, otherwise fall back to field
            var positionGetter = getPosition ?? _getPosition;
            if (positionGetter == null)
                return true;

            var targetPos = positionGetter(target);
            if (point.DistanceSquaredTo(targetPos) <= 0.0001f)
                return true;

            var result = _losService.CheckLOS(point, targetPos);
            return result.HasLineOfSight;
        }

        private static Vector3 ResolveLineOfEffectOrigin(
            Actions.ActionDefinition action,
            Vector3 sourcePosition,
            Vector3 targetPoint,
            Vector3? wallStart)
        {
            return action.TargetType switch
            {
                Actions.TargetType.Cone => sourcePosition,
                Actions.TargetType.Line => sourcePosition,
                Actions.TargetType.Charge => sourcePosition,
                Actions.TargetType.WallSegment => wallStart ?? sourcePosition,
                _ => targetPoint
            };
        }

        /// <summary>
        /// Check if point is within a cone.
        /// </summary>
        private bool IsInCone(Godot.Vector3 origin, Godot.Vector3 direction, Godot.Vector3 point, float angle, float range)
        {
            var toPoint = point - origin;
            float distance = toPoint.Length();
            if (distance > range)
                return false;

            var dir = (direction - origin).Normalized();
            var pointDir = toPoint.Normalized();
            float dot = dir.Dot(pointDir);
            float halfAngleRad = Godot.Mathf.DegToRad(angle / 2);

            return dot >= Godot.Mathf.Cos(halfAngleRad);
        }

        /// <summary>
        /// Check if point is on a line (within width).
        /// </summary>
        private bool IsOnLine(Godot.Vector3 start, Godot.Vector3 end, Godot.Vector3 point, float width)
        {
            var line = end - start;
            float lineLength = line.Length();
            var lineDir = line.Normalized();

            var toPoint = point - start;
            float projection = toPoint.Dot(lineDir);

            if (projection < 0 || projection > lineLength)
                return false;

            var closestPoint = start + lineDir * projection;
            float distance = closestPoint.DistanceTo(point);

            return distance <= width / 2;
        }
    }
}
