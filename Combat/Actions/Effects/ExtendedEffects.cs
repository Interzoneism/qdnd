using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Combat.Environment;
using QDND.Combat.Movement;
using QDND.Combat.Services;

namespace QDND.Combat.Actions.Effects
{
    /// <summary>
    /// Removes all statuses from targets that belong to a BG3 status group tag.
    /// </summary>
    public class RemoveStatusByGroupEffect : Effect
    {
        public override string Type => "remove_status_by_group";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";

            if (context?.Statuses == null)
            {
                results.Add(EffectResult.Failed(Type, sourceId, "none", "No status manager available"));
                return results;
            }

            if (!definition.Parameters.TryGetValue("group_id", out var groupObj) || groupObj is not string groupId)
            {
                results.Add(EffectResult.Failed(Type, sourceId, "none", "Missing group_id parameter"));
                return results;
            }

            var groupTag = groupId.ToLowerInvariant();

            foreach (var target in context.Targets ?? Enumerable.Empty<Combatant>())
            {
                int count = context.Statuses.RemoveStatuses(target.Id, s => s.Definition.Tags.Contains(groupTag));
                if (count > 0)
                    results.Add(EffectResult.Succeeded(Type, sourceId, target.Id, count, $"Removed {count} status(es) in group '{groupId}' from {target.Name}"));
                else
                    results.Add(EffectResult.Failed(Type, sourceId, target.Id, $"No statuses in group '{groupId}' found on {target.Name}"));
            }

            return results;
        }
    }

    /// <summary>
    /// Sets the remaining duration of a specific status on each target.
    /// </summary>
    public class SetStatusDurationEffect : Effect
    {
        public override string Type => "set_status_duration";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";

            if (context?.Statuses == null)
            {
                results.Add(EffectResult.Failed(Type, sourceId, "none", "No status manager available"));
                return results;
            }

            var statusId = definition.StatusId;
            int newDuration = definition.StatusDuration;

            foreach (var target in context.Targets ?? Enumerable.Empty<Combatant>())
            {
                var statuses = context.Statuses.GetStatuses(target.Id);
                var match = statuses?.FirstOrDefault(s => string.Equals(s.Definition.Id, statusId, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    match.RemainingDuration = newDuration;
                    results.Add(EffectResult.Succeeded(Type, sourceId, target.Id, newDuration, $"Set '{statusId}' duration to {newDuration} on {target.Name}"));
                }
                else
                {
                    results.Add(EffectResult.Failed(Type, sourceId, target.Id, $"Status '{statusId}' not found on {target.Name}"));
                }
            }

            return results;
        }
    }

    /// <summary>
    /// Grants advantage to the source by applying a transient "advantaged" status.
    /// </summary>
    public class SetAdvantageEffect : Effect
    {
        public override string Type => "set_advantage";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";

            if (context?.Statuses != null && context.Source != null)
            {
                context.Statuses.ApplyStatus("advantaged", sourceId, sourceId, duration: 1, stacks: 1);
            }

            results.Add(EffectResult.Succeeded(Type, sourceId, sourceId, 0, "Advantage granted"));
            return results;
        }
    }

    /// <summary>
    /// Imposes disadvantage on the source by applying a transient "disadvantaged" status.
    /// </summary>
    public class SetDisadvantageEffect : Effect
    {
        public override string Type => "set_disadvantage";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";

            if (context?.Statuses != null && context.Source != null)
            {
                context.Statuses.ApplyStatus("disadvantaged", sourceId, sourceId, duration: 1, stacks: 1);
            }

            results.Add(EffectResult.Succeeded(Type, sourceId, sourceId, 0, "Disadvantage imposed"));
            return results;
        }
    }

    /// <summary>
    /// Swaps positions between the caster and the first target.
    /// </summary>
    public class SwapPlacesEffect : Effect
    {
        public override string Type => "swap_places";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";

            if (context?.Source == null || context.Targets == null || context.Targets.Count == 0)
            {
                results.Add(EffectResult.Failed(Type, sourceId, "none", "No target available for swap"));
                return results;
            }

            var target = context.Targets[0];
            var sourcePos = context.Source.Position;
            var targetPos = target.Position;

            if (context.ForcedMovement != null)
            {
                context.ForcedMovement.Teleport(context.Source, targetPos);
                context.ForcedMovement.Teleport(target, sourcePos);
            }
            else
            {
                context.Source.Position = targetPos;
                target.Position = sourcePos;
            }

            results.Add(EffectResult.Succeeded(Type, sourceId, target.Id, 0, $"{context.Source.Name} swapped places with {target.Name}"));
            return results;
        }
    }

    /// <summary>
    /// Extinguishes fire surfaces at the target location and removes burning status from targets.
    /// </summary>
    public class DouseEffect : Effect
    {
        public override string Type => "douse";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";
            int statusesRemoved = 0;
            int surfacesDoused = 0;

            // Remove burning status from each target
            foreach (var target in context?.Targets ?? Enumerable.Empty<Combatant>())
            {
                if (context?.Statuses != null && context.Statuses.RemoveStatus(target.Id, "burning"))
                {
                    statusesRemoved++;
                }
            }

            // Remove fire surfaces at position
            if (context?.Surfaces != null)
            {
                var position = context.TargetPosition ?? context.Targets?.FirstOrDefault()?.Position;
                if (position.HasValue)
                {
                    var surfaces = context.Surfaces.GetSurfacesAt(position.Value);
                    foreach (var surface in surfaces)
                    {
                        if (surface.Definition.Tags.Contains("fire"))
                        {
                            context.Surfaces.RemoveSurface(surface);
                            surfacesDoused++;
                        }
                    }
                }
            }

            results.Add(EffectResult.Succeeded(Type, sourceId, context?.Targets?.FirstOrDefault()?.Id ?? "area",
                statusesRemoved + surfacesDoused,
                $"Doused: {statusesRemoved} burning status(es), {surfacesDoused} fire surface(s)"));
            return results;
        }
    }

    /// <summary>
    /// Sets the death animation/visual type on each target (cosmetic only).
    /// </summary>
    public class SwitchDeathTypeEffect : Effect
    {
        public override string Type => "switch_death_type";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";

            definition.Parameters.TryGetValue("death_type", out var deathTypeObj);
            var deathType = deathTypeObj?.ToString() ?? "default";

            foreach (var target in context?.Targets ?? Enumerable.Empty<Combatant>())
            {
                var result = EffectResult.Succeeded(Type, sourceId, target.Id, 0, $"Death type set to '{deathType}' for {target.Name}");
                result.Data["death_type"] = deathType;
                results.Add(result);
            }

            return results;
        }
    }

    /// <summary>
    /// Equalizes a resource (typically HP) between the caster and the first target.
    /// </summary>
    public class EqualizeEffect : Effect
    {
        public override string Type => "equalize";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";

            if (context?.Source == null || context.Targets == null || context.Targets.Count == 0)
            {
                results.Add(EffectResult.Failed(Type, sourceId, "none", "No target available for equalize"));
                return results;
            }

            var target = context.Targets[0];
            string resource = definition.Parameters.TryGetValue("resource", out var r) ? r?.ToString() : "hp";

            if (string.Equals(resource, "hp", StringComparison.OrdinalIgnoreCase))
            {
                int totalHp = context.Source.Resources.CurrentHP + target.Resources.CurrentHP;
                int average = totalHp / 2;
                context.Source.Resources.CurrentHP = Math.Min(average, context.Source.Resources.MaxHP);
                target.Resources.CurrentHP = Math.Min(average, target.Resources.MaxHP);

                results.Add(EffectResult.Succeeded(Type, sourceId, target.Id, average,
                    $"HP equalized: {context.Source.Name} = {context.Source.Resources.CurrentHP}, {target.Name} = {target.Resources.CurrentHP}"));
            }
            else
            {
                results.Add(EffectResult.Failed(Type, sourceId, target.Id, $"Unknown resource type: {resource}"));
            }

            return results;
        }
    }

    /// <summary>
    /// Transforms existing surfaces at the target position (freeze, electrify, ignite, douse, melt).
    /// </summary>
    public class SurfaceChangeEffect : Effect
    {
        public override string Type => "surface_change";

        private static readonly Dictionary<string, Dictionary<string, string>> TransformMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["freeze"] = new(StringComparer.OrdinalIgnoreCase) { ["water"] = "ice", ["electrified_water"] = "ice" },
            ["electrify"] = new(StringComparer.OrdinalIgnoreCase) { ["water"] = "electrified_water" },
            ["ignite"] = new(StringComparer.OrdinalIgnoreCase) { ["oil"] = "fire", ["grease"] = "fire", ["web"] = "fire" },
            ["melt"] = new(StringComparer.OrdinalIgnoreCase) { ["ice"] = "water" },
        };

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";

            if (context?.Surfaces == null)
            {
                results.Add(EffectResult.Failed(Type, sourceId, null, "No surface manager available"));
                return results;
            }

            definition.Parameters.TryGetValue("surface_type", out var surfaceTypeObj);
            var transformType = surfaceTypeObj?.ToString()?.ToLowerInvariant() ?? "unknown";

            var position = context.TargetPosition
                ?? context.Targets?.FirstOrDefault()?.Position
                ?? context.Source?.Position;

            if (!position.HasValue)
            {
                results.Add(EffectResult.Failed(Type, sourceId, null, "No position available for surface change"));
                return results;
            }

            var surfaces = context.Surfaces.GetSurfacesAt(position.Value);
            int transformed = 0;

            foreach (var surface in surfaces)
            {
                if (string.Equals(transformType, "douse", StringComparison.OrdinalIgnoreCase))
                {
                    if (surface.Definition.Tags.Contains("fire"))
                    {
                        context.Surfaces.RemoveSurface(surface);
                        transformed++;
                    }
                }
                else if (TransformMap.TryGetValue(transformType, out var mapping))
                {
                    if (mapping.TryGetValue(surface.Definition.Id, out var newSurfaceId))
                    {
                        context.Surfaces.TransformSurface(surface, newSurfaceId);
                        transformed++;
                    }
                }
            }

            results.Add(EffectResult.Succeeded(Type, sourceId, null, transformed,
                $"Surface change '{transformType}': {transformed} surface(s) affected"));
            return results;
        }
    }

    /// <summary>
    /// Fires the weapon's on-hit triggers (Divine Smite, Hex, etc.) on each target.
    /// </summary>
    public class ExecuteWeaponFunctorsEffect : Effect
    {
        public override string Type => "execute_weapon_functors";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";

            definition.Parameters.TryGetValue("damage_type", out var dmgTypeObj);
            var damageType = dmgTypeObj?.ToString()
                ?? context.Source?.MainHandWeapon?.DamageType.ToString().ToLowerInvariant()
                ?? "physical";

            foreach (var target in context?.Targets ?? Enumerable.Empty<Combatant>())
            {
                var hitContext = new OnHitContext
                {
                    Attacker = context.Source,
                    Target = target,
                    Action = context.Ability,
                    IsCritical = context.IsCritical,
                    DamageType = damageType,
                    AttackType = context.Ability?.AttackType ?? AttackType.MeleeWeapon
                };

                context.OnHitTriggerService?.ProcessOnHitConfirmed(hitContext);

                var result = EffectResult.Succeeded(Type, sourceId, target.Id,
                    hitContext.BonusDamage,
                    $"Weapon functors executed on {target.Name}" +
                    (hitContext.BonusDamage > 0 ? $" (+{hitContext.BonusDamage} {hitContext.BonusDamageType} bonus damage)" : ""));
                if (hitContext.BonusStatusesToApply?.Count > 0)
                    result.Data["bonus_statuses"] = string.Join(",", hitContext.BonusStatusesToApply);
                results.Add(result);
            }

            if (results.Count == 0)
                results.Add(EffectResult.Succeeded(Type, sourceId, null, 0, "No targets for weapon functors"));

            return results;
        }
    }

    /// <summary>
    /// Requests extra projectiles for multi-projectile spells.
    /// Stores the count for the projectile system to consume.
    /// </summary>
    public class SpawnExtraProjectilesEffect : Effect
    {
        public override string Type => "spawn_extra_projectiles";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";

            int extraCount = 0;
            if (definition.Parameters.TryGetValue("count", out var countObj))
                extraCount = Convert.ToInt32(countObj);
            if (extraCount == 0)
                extraCount = (int)definition.Value;

            var result = EffectResult.Succeeded(Type, sourceId, null, extraCount,
                $"Requested {extraCount} extra projectile(s)");
            result.Data["extra_projectile_count"] = extraCount;
            results.Add(result);

            return results;
        }
    }

    /// <summary>
    /// Executes another spell as a sub-action (used by reactions/interrupts).
    /// </summary>
    public class UseSpellEffect : Effect
    {
        public override string Type => "use_spell";

        [ThreadStatic] private static int _recursionDepth;
        private const int MaxRecursionDepth = 3;

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";

            if (_recursionDepth >= MaxRecursionDepth)
            {
                results.Add(EffectResult.Failed(Type, sourceId, null, "Max spell recursion depth reached"));
                return results;
            }

            if (!definition.Parameters.TryGetValue("spell_id", out var spellObj) || spellObj is not string spellId)
            {
                results.Add(EffectResult.Failed(Type, sourceId, null, "Missing spell_id parameter"));
                return results;
            }

            if (context.Pipeline == null)
            {
                results.Add(EffectResult.Failed(Type, sourceId, null, "No pipeline available for sub-spell execution"));
                return results;
            }

            _recursionDepth++;
            try
            {
                var options = new ActionExecutionOptions
                {
                    SkipCostValidation = true,
                    TargetPosition = context.TargetPosition
                };

                var subResult = context.Pipeline.ExecuteAction(spellId, context.Source, context.Targets, options);

                if (subResult.Success)
                    results.Add(EffectResult.Succeeded(Type, sourceId, context.Targets?.FirstOrDefault()?.Id, 0, $"Sub-spell '{spellId}' executed successfully"));
                else
                    results.Add(EffectResult.Failed(Type, sourceId, context.Targets?.FirstOrDefault()?.Id, $"Sub-spell '{spellId}' failed: {subResult.ErrorMessage}"));
            }
            finally
            {
                _recursionDepth--;
            }

            return results;
        }
    }

    /// <summary>
    /// Grants a feature, resource, or ability to targets.
    /// </summary>
    public class GrantEffect : Effect
    {
        public override string Type => "grant";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";

            string grantId = definition.Parameters.TryGetValue("grant_id", out var g) ? g?.ToString() : null;
            int amount = (int)definition.Value;
            if (amount == 0) amount = 1;

            foreach (var target in context?.Targets ?? Enumerable.Empty<Combatant>())
            {
                var result = EffectResult.Succeeded(Type, sourceId, target.Id, amount,
                    $"Granted '{grantId}' (x{amount}) to {target.Name}");
                result.Data["grant_id"] = grantId;
                result.Data["amount"] = amount;
                results.Add(result);
            }

            if (results.Count == 0)
                results.Add(EffectResult.Succeeded(Type, sourceId, null, amount, $"Granted '{grantId}' (x{amount})"));

            return results;
        }
    }

    /// <summary>
    /// Fires a secondary projectile spell at the current targets.
    /// </summary>
    public class FireProjectileEffect : Effect
    {
        public override string Type => "fire_projectile";

        [ThreadStatic] private static int _recursionDepth;
        private const int MaxRecursionDepth = 3;

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";

            if (_recursionDepth >= MaxRecursionDepth)
            {
                results.Add(EffectResult.Failed(Type, sourceId, null, "Max projectile recursion depth reached"));
                return results;
            }

            if (!definition.Parameters.TryGetValue("projectile_id", out var projObj) || projObj is not string projectileId)
            {
                results.Add(EffectResult.Failed(Type, sourceId, null, "Missing projectile_id parameter"));
                return results;
            }

            if (context.Pipeline == null)
            {
                results.Add(EffectResult.Failed(Type, sourceId, null, "No pipeline available for projectile execution"));
                return results;
            }

            _recursionDepth++;
            try
            {
                var options = new ActionExecutionOptions
                {
                    SkipCostValidation = true,
                    TargetPosition = context.TargetPosition
                };

                var subResult = context.Pipeline.ExecuteAction(projectileId, context.Source, context.Targets, options);

                if (subResult.Success)
                    results.Add(EffectResult.Succeeded(Type, sourceId, context.Targets?.FirstOrDefault()?.Id, 0, $"Projectile '{projectileId}' fired successfully"));
                else
                    results.Add(EffectResult.Failed(Type, sourceId, context.Targets?.FirstOrDefault()?.Id, $"Projectile '{projectileId}' failed: {subResult.ErrorMessage}"));
            }
            finally
            {
                _recursionDepth--;
            }

            return results;
        }
    }

    /// <summary>
    /// Creates an item in a target's inventory (e.g., Goodberry conjuration).
    /// </summary>
    public class SpawnInventoryItemEffect : Effect
    {
        public override string Type => "spawn_inventory_item";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";

            definition.Parameters.TryGetValue("item_id", out var itemIdObj);
            var itemId = itemIdObj?.ToString() ?? "unknown_item";

            int count = 0;
            if (definition.Parameters.TryGetValue("count", out var countObj))
                count = Convert.ToInt32(countObj);
            if (count == 0)
                count = Math.Max(1, (int)definition.Value);

            var recipients = (context?.Targets?.Count > 0) ? context.Targets : new List<Combatant>();
            if (recipients.Count == 0 && context?.Source != null)
                recipients = new List<Combatant> { context.Source };

            var inventoryService = context?.CombatContext?.GetService<InventoryService>();

            foreach (var target in recipients)
            {
                if (inventoryService != null)
                {
                    var item = new InventoryItem
                    {
                        DefinitionId = itemId,
                        Name = itemId,
                        Category = ItemCategory.Misc,
                        Quantity = count
                    };
                    inventoryService.AddItemToBag(target, item);
                }

                var result = EffectResult.Succeeded(Type, sourceId, target.Id, count,
                    $"Spawned {count}x '{itemId}' in {target.Name}'s inventory");
                result.Data["item_id"] = itemId;
                result.Data["count"] = count;
                results.Add(result);
            }

            return results;
        }
    }

    /// <summary>
    /// Picks up an entity (environmental object or item). Data-forwarding handler for future entity system.
    /// </summary>
    public class PickupEntityEffect : Effect
    {
        public override string Type => "pickup_entity";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();
            var sourceId = context?.Source?.Id ?? "unknown";

            string entityId = definition.Parameters.TryGetValue("entity_id", out var e) ? e?.ToString() : "unknown";

            foreach (var target in context?.Targets ?? Enumerable.Empty<Combatant>())
            {
                var result = EffectResult.Succeeded(Type, sourceId, target.Id, 0,
                    $"Picked up entity '{entityId}'");
                result.Data["entity_id"] = entityId;
                results.Add(result);
            }

            if (results.Count == 0)
            {
                var result = EffectResult.Succeeded(Type, sourceId, sourceId, 0,
                    $"Picked up entity '{entityId}'");
                result.Data["entity_id"] = entityId;
                results.Add(result);
            }

            return results;
        }
    }
}
