using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;

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
        /// <summary>
        /// Validate targets for an ability.
        /// </summary>
        public TargetValidation Validate(
            Abilities.AbilityDefinition ability,
            Combatant source,
            List<Combatant> selectedTargets,
            List<Combatant> allCombatants)
        {
            var result = new TargetValidation { IsValid = true };

            // Handle self-targeting
            if (ability.TargetType == Abilities.TargetType.Self)
            {
                if (!selectedTargets.Contains(source))
                    selectedTargets = new List<Combatant> { source };
                result.ValidTargets = selectedTargets;
                return result;
            }

            // Handle no-target abilities
            if (ability.TargetType == Abilities.TargetType.None)
            {
                result.ValidTargets = new List<Combatant>();
                return result;
            }

            // Validate each selected target
            foreach (var target in selectedTargets)
            {
                var validation = ValidateSingleTarget(ability, source, target);
                if (validation.IsValid)
                {
                    result.ValidTargets.Add(target);
                }
                else
                {
                    result.InvalidTargets.Add((target, validation.Reason));
                }
            }

            // Check target count
            if (result.ValidTargets.Count == 0)
            {
                return TargetValidation.Invalid("No valid targets selected");
            }

            if (result.ValidTargets.Count > ability.MaxTargets)
            {
                result.ValidTargets = result.ValidTargets.Take(ability.MaxTargets).ToList();
            }

            return result;
        }

        /// <summary>
        /// Validate a single target.
        /// </summary>
        public TargetValidation ValidateSingleTarget(
            Abilities.AbilityDefinition ability,
            Combatant source,
            Combatant target)
        {
            // Check target is alive
            if (!target.IsActive)
                return TargetValidation.Invalid("Target is incapacitated");

            // Check faction filter
            if (!IsValidFaction(ability.TargetFilter, source, target))
                return TargetValidation.Invalid("Invalid target faction");

            // TODO: Range check would require position data
            // if (GetDistance(source, target) > ability.Range)
            //     return TargetValidation.Invalid("Target out of range");

            return TargetValidation.Valid(new List<Combatant> { target });
        }

        /// <summary>
        /// Get all valid targets for an ability.
        /// </summary>
        public List<Combatant> GetValidTargets(
            Abilities.AbilityDefinition ability,
            Combatant source,
            List<Combatant> allCombatants)
        {
            if (ability.TargetType == Abilities.TargetType.Self)
                return new List<Combatant> { source };

            if (ability.TargetType == Abilities.TargetType.None)
                return new List<Combatant>();

            return allCombatants
                .Where(c => c.IsActive)
                .Where(c => IsValidFaction(ability.TargetFilter, source, c))
                .ToList();
        }

        /// <summary>
        /// Check if target faction matches filter.
        /// </summary>
        public bool IsValidFaction(Abilities.TargetFilter filter, Combatant source, Combatant target)
        {
            if (filter == Abilities.TargetFilter.All)
                return true;

            bool isSelf = source.Id == target.Id;
            bool isAlly = source.Faction == target.Faction;
            bool isEnemy = !isAlly && target.Faction != Faction.Neutral;
            bool isNeutral = target.Faction == Faction.Neutral;

            if (isSelf && filter.HasFlag(Abilities.TargetFilter.Self))
                return true;
            if (isAlly && !isSelf && filter.HasFlag(Abilities.TargetFilter.Allies))
                return true;
            if (isEnemy && filter.HasFlag(Abilities.TargetFilter.Enemies))
                return true;
            if (isNeutral && filter.HasFlag(Abilities.TargetFilter.Neutrals))
                return true;

            return false;
        }

        /// <summary>
        /// Resolve area targets (for AoE abilities).
        /// </summary>
        public List<Combatant> ResolveAreaTargets(
            Abilities.AbilityDefinition ability,
            Combatant source,
            Godot.Vector3 targetPoint,
            List<Combatant> allCombatants,
            Func<Combatant, Godot.Vector3> getPosition)
        {
            var targets = new List<Combatant>();

            foreach (var combatant in allCombatants.Where(c => c.IsActive))
            {
                if (!IsValidFaction(ability.TargetFilter, source, combatant))
                    continue;

                var pos = getPosition(combatant);
                float distance = pos.DistanceTo(targetPoint);

                bool inArea = ability.TargetType switch
                {
                    Abilities.TargetType.Circle => distance <= ability.AreaRadius,
                    Abilities.TargetType.Cone => IsInCone(getPosition(source), targetPoint, pos, ability.ConeAngle, ability.Range),
                    Abilities.TargetType.Line => IsOnLine(getPosition(source), targetPoint, pos, 1f), // 1 unit width
                    _ => false
                };

                if (inArea)
                {
                    targets.Add(combatant);
                }
            }

            return targets;
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

            return distance <= width;
        }
    }
}
