using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Rules;

namespace QDND.Tools.AutoBattler
{
    /// <summary>
    /// Snapshot of a target's state after an action for logging.
    /// </summary>
    public class TargetSnapshot
    {
        public string Id { get; set; }
        public float[] Position { get; set; }
        public int CurrentHP { get; set; }
        public int MaxHP { get; set; }
    }

    /// <summary>
    /// Extracts rich, action-category-specific detail from an ActionExecutionResult
    /// for forensic logging via the BlackBoxLogger.
    /// </summary>
    public static class ActionDetailCollector
    {
        /// <summary>
        /// Collect detailed action data from an execution result.
        /// Returns a dictionary suitable for the BlackBoxLogger ACTION_DETAIL event.
        /// </summary>
        public static Dictionary<string, object> Collect(
            ActionExecutionResult result,
            ActionDefinition action,
            float[] sourcePosition,
            int sourceHp,
            int sourceMaxHp,
            List<TargetSnapshot> targetSnapshots)
        {
            // Guard against null inputs
            if (result == null || action == null)
                return new Dictionary<string, object>();

            var details = new Dictionary<string, object>();

            // Always include basic fields
            details["success"] = result.Success;
            details["action_id"] = result.ActionId;

            // Include spell-specific fields only when applicable
            if (action.SpellLevel > 0)
                details["spell_level"] = action.SpellLevel;

            if (action.School != SpellSchool.None)
                details["school"] = action.School.ToString();

            if (action.Tags != null && action.Tags.Count > 0)
                details["tags"] = action.Tags.ToList();

            if (action.RequiresConcentration)
                details["requires_concentration"] = true;

            if (action.AttackType.HasValue)
                details["attack_type"] = action.AttackType.ToString();

            if (!string.IsNullOrEmpty(action.SaveType))
                details["save_type"] = action.SaveType;

            details["range"] = action.Range;
            details["source_hp"] = sourceHp;
            details["source_max_hp"] = sourceMaxHp;

            // Include source position if available
            if (sourcePosition != null)
            {
                details["source_position"] = sourcePosition;
            }

            // Collect attack roll details
            if (result.AttackResult != null)
            {
                details["attack_roll"] = CollectAttackRoll(result.AttackResult);
            }

            // Collect saving throw details
            if (result.SaveResult != null)
            {
                details["saving_throw"] = CollectSavingThrow(result.SaveResult, action.SaveType);
            }

            // Collect effect-specific details
            if (result.EffectResults != null && result.EffectResults.Count > 0)
            {
                CollectDamageEffects(result.EffectResults, details);
                CollectHealEffects(result.EffectResults, details);
                CollectStatusEffects(result.EffectResults, details);
                CollectTeleportEffects(result.EffectResults, details);
                CollectForcedMovementEffects(result.EffectResults, details);
                CollectSurfaceEffects(result.EffectResults, details);
                CollectSummonEffects(result.EffectResults, details);
            }

            // Include error for failed actions
            if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
            {
                details["error"] = result.ErrorMessage;
            }

            // Include target HP snapshots
            if (targetSnapshots != null && targetSnapshots.Count > 0)
            {
                var targetStates = new List<Dictionary<string, object>>();
                foreach (var snapshot in targetSnapshots)
                {
                    var state = new Dictionary<string, object>
                    {
                        { "id", snapshot.Id },
                        { "hp", snapshot.CurrentHP },
                        { "max_hp", snapshot.MaxHP }
                    };
                    if (snapshot.Position != null)
                    {
                        state["position"] = snapshot.Position;
                    }
                    targetStates.Add(state);
                }
                details["target_states"] = targetStates;
            }

            return details;
        }

        private static Dictionary<string, object> CollectAttackRoll(QueryResult attackResult)
        {
            var attackRoll = new Dictionary<string, object>
            {
                { "natural_roll", attackResult.NaturalRoll },
                { "total", (int)attackResult.FinalValue },
                { "hit", attackResult.IsSuccess },
                { "critical", attackResult.IsCritical }
            };

            // Add target AC if available from input DC (used for attack rolls)
            if (attackResult.Input?.DC > 0)
            {
                attackRoll["target_ac"] = attackResult.Input.DC;
            }

            // Add advantage state
            string advantageState = attackResult.AdvantageState switch
            {
                1 => "advantage",
                -1 => "disadvantage",
                _ => "normal"
            };
            attackRoll["advantage"] = advantageState;

            return attackRoll;
        }

        private static Dictionary<string, object> CollectSavingThrow(QueryResult saveResult, string saveType)
        {
            return new Dictionary<string, object>
            {
                { "type", saveType ?? "unknown" },
                { "dc", saveResult.Input?.DC ?? 0 },
                { "natural_roll", saveResult.NaturalRoll },
                { "total", (int)saveResult.FinalValue },
                { "passed", saveResult.IsSuccess }
            };
        }

        private static void CollectDamageEffects(List<EffectResult> effectResults, Dictionary<string, object> details)
        {
            var damageEffects = effectResults.Where(e => e.EffectType == "damage").ToList();
            if (damageEffects.Count == 0)
                return;

            var damageDealt = new List<Dictionary<string, object>>();
            int totalDamage = 0;

            foreach (var effect in damageEffects)
            {
                int amount = SafeGet(effect.Data, "actualDamageDealt", (int)effect.Value);
                totalDamage += amount;

                var damageEntry = new Dictionary<string, object>
                {
                    { "target", effect.TargetId },
                    { "amount", amount },
                    { "type", SafeGet(effect.Data, "damageType", "Unknown") },
                    { "was_critical", SafeGet(effect.Data, "wasCritical", false) }
                };

                // Add resistance info if present
                var resistanceApplied = SafeGetRaw(effect.Data, "resistanceApplied");
                if (resistanceApplied != null)
                {
                    damageEntry["resistances"] = resistanceApplied.ToString();
                }

                damageDealt.Add(damageEntry);
            }

            details["damage_dealt"] = damageDealt;
            details["total_damage"] = totalDamage;
        }

        private static void CollectHealEffects(List<EffectResult> effectResults, Dictionary<string, object> details)
        {
            var healEffects = effectResults.Where(e => e.EffectType == "heal").ToList();
            if (healEffects.Count == 0)
                return;

            var healingDone = new List<Dictionary<string, object>>();
            int totalHealing = 0;

            foreach (var effect in healEffects)
            {
                int amount = (int)effect.Value;
                totalHealing += amount;

                healingDone.Add(new Dictionary<string, object>
                {
                    { "target", effect.TargetId },
                    { "amount", amount }
                });
            }

            details["healing_done"] = healingDone;
            details["total_healing"] = totalHealing;
        }

        private static void CollectStatusEffects(List<EffectResult> effectResults, Dictionary<string, object> details)
        {
            // Collect apply_status effects
            var applyStatusEffects = effectResults.Where(e => e.EffectType == "apply_status").ToList();
            if (applyStatusEffects.Count > 0)
            {
                var statusesApplied = new List<Dictionary<string, object>>();
                foreach (var effect in applyStatusEffects)
                {
                    string statusId = SafeGet(effect.Data, "statusId", effect.Message);

                    var statusEntry = new Dictionary<string, object>
                    {
                        { "target", effect.TargetId },
                        { "status_id", statusId }
                    };

                    var duration = SafeGetRaw(effect.Data, "duration");
                    if (duration != null)
                    {
                        statusEntry["duration"] = duration;
                    }

                    statusesApplied.Add(statusEntry);
                }
                details["statuses_applied"] = statusesApplied;
            }

            // Collect remove_status effects
            var removeStatusEffects = effectResults.Where(e => e.EffectType == "remove_status").ToList();
            if (removeStatusEffects.Count > 0)
            {
                var statusesRemoved = new List<Dictionary<string, object>>();
                foreach (var effect in removeStatusEffects)
                {
                    string statusId = SafeGet(effect.Data, "statusId", effect.Message);

                    statusesRemoved.Add(new Dictionary<string, object>
                    {
                        { "target", effect.TargetId },
                        { "status_id", statusId }
                    });
                }
                details["statuses_removed"] = statusesRemoved;
            }
        }

        private static void CollectTeleportEffects(List<EffectResult> effectResults, Dictionary<string, object> details)
        {
            var teleportEffects = effectResults.Where(e => e.EffectType == "teleport").ToList();
            if (teleportEffects.Count == 0)
                return;

            var teleports = new List<Dictionary<string, object>>();
            foreach (var effect in teleportEffects)
            {
                var teleport = new Dictionary<string, object>
                {
                    { "target", effect.TargetId }
                };

                var from = SafeGetRaw(effect.Data, "from");
                if (from != null)
                {
                    teleport["from"] = ConvertToFloatArray(from);
                }

                var to = SafeGetRaw(effect.Data, "to");
                if (to != null)
                {
                    teleport["to"] = ConvertToFloatArray(to);
                }

                teleports.Add(teleport);
            }

            details["teleports"] = teleports;
        }

        private static void CollectForcedMovementEffects(List<EffectResult> effectResults, Dictionary<string, object> details)
        {
            var movementEffects = effectResults.Where(e =>
                e.EffectType == "forced_move" || e.EffectType == "push" || e.EffectType == "pull").ToList();

            if (movementEffects.Count == 0)
                return;

            var movements = new List<Dictionary<string, object>>();
            foreach (var effect in movementEffects)
            {
                var movement = new Dictionary<string, object>
                {
                    { "target", effect.TargetId },
                    { "type", effect.EffectType }
                };

                var distance = SafeGetRaw(effect.Data, "distance");
                if (distance != null)
                {
                    movement["distance"] = distance;
                }

                var from = SafeGetRaw(effect.Data, "from");
                if (from != null)
                {
                    movement["from"] = ConvertToFloatArray(from);
                }

                var to = SafeGetRaw(effect.Data, "to");
                if (to != null)
                {
                    movement["to"] = ConvertToFloatArray(to);
                }

                var collisionDamage = SafeGetRaw(effect.Data, "collisionDamage");
                if (collisionDamage != null)
                {
                    movement["collision_damage"] = collisionDamage;
                }

                var fallDamage = SafeGetRaw(effect.Data, "fallDamage");
                if (fallDamage != null)
                {
                    movement["fall_damage"] = fallDamage;
                }

                movements.Add(movement);
            }

            details["forced_movements"] = movements;
        }

        private static void CollectSurfaceEffects(List<EffectResult> effectResults, Dictionary<string, object> details)
        {
            var surfaceEffects = effectResults.Where(e => e.EffectType == "spawn_surface").ToList();
            if (surfaceEffects.Count == 0)
                return;

            var surfaces = new List<Dictionary<string, object>>();
            foreach (var effect in surfaceEffects)
            {
                var surface = new Dictionary<string, object>();

                var surfaceType = SafeGetRaw(effect.Data, "surfaceType");
                if (surfaceType != null)
                {
                    surface["type"] = surfaceType;
                }

                var radius = SafeGetRaw(effect.Data, "radius");
                if (radius != null)
                {
                    surface["radius"] = radius;
                }

                var position = SafeGetRaw(effect.Data, "position");
                if (position != null)
                {
                    surface["position"] = ConvertToFloatArray(position);
                }

                if (surface.Count > 0)
                {
                    surfaces.Add(surface);
                }
            }

            if (surfaces.Count > 0)
            {
                details["surfaces_created"] = surfaces;
            }
        }

        private static void CollectSummonEffects(List<EffectResult> effectResults, Dictionary<string, object> details)
        {
            var summonEffects = effectResults.Where(e =>
                e.EffectType == "summon" || e.EffectType == "summon_combatant").ToList();

            if (summonEffects.Count == 0)
                return;

            var summons = new List<Dictionary<string, object>>();
            foreach (var effect in summonEffects)
            {
                var summon = new Dictionary<string, object>();

                var templateId = SafeGetRaw(effect.Data, "templateId");
                if (templateId != null)
                {
                    summon["unit_id"] = templateId;
                }

                // Try to get unit name from message or data
                if (!string.IsNullOrEmpty(effect.Message))
                {
                    summon["unit_name"] = effect.Message;
                }

                if (summon.Count > 0)
                {
                    summons.Add(summon);
                }
            }

            if (summons.Count > 0)
            {
                details["summons"] = summons;
            }
        }

        // Helper methods

        private static T SafeGet<T>(Dictionary<string, object> data, string key, T defaultValue = default)
        {
            if (data == null || !data.TryGetValue(key, out var value) || value == null)
                return defaultValue;
            try { return (T)Convert.ChangeType(value, typeof(T)); }
            catch { return defaultValue; }
        }

        private static object SafeGetRaw(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.TryGetValue(key, out var value))
                return null;
            return value;
        }

        private static float[] ConvertToFloatArray(object value)
        {
            // Handle various possible formats
            if (value is float[] floatArray)
                return floatArray;

            // Handle Godot.Vector3 (extract X, Y, Z via reflection to avoid direct Godot dependency)
            if (value != null)
            {
                var type = value.GetType();
                if (type.Name == "Vector3")
                {
                    try
                    {
                        var xProp = type.GetProperty("X");
                        var xField = type.GetField("x");
                        var yProp = type.GetProperty("Y");
                        var yField = type.GetField("y");
                        var zProp = type.GetProperty("Z");
                        var zField = type.GetField("z");

                        if ((xProp != null || xField != null) && 
                            (yProp != null || yField != null) && 
                            (zProp != null || zField != null))
                        {
                            float x = Convert.ToSingle(xProp != null ? xProp.GetValue(value) : xField.GetValue(value));
                            float y = Convert.ToSingle(yProp != null ? yProp.GetValue(value) : yField.GetValue(value));
                            float z = Convert.ToSingle(zProp != null ? zProp.GetValue(value) : zField.GetValue(value));
                            return new float[] { x, y, z };
                        }
                    }
                    catch
                    {
                        // Fall through to default
                    }
                }
            }

            return new float[] { 0, 0, 0 };
        }
    }
}
