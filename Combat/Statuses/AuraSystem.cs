using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Data;

namespace QDND.Combat.Statuses
{
    /// <summary>
    /// Lightweight data struct for AI aura queries. No Godot node dependencies.
    /// </summary>
    public readonly struct AuraInfo
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly string StatusId;
        public readonly string SourceCombatantId;
        public readonly string SourceFaction;
        public readonly string SourceTeam;
        public readonly bool AffectsEnemiesOnly;

        public AuraInfo(Vector3 position, float radius, string statusId,
            string sourceCombatantId, string sourceFaction, string sourceTeam,
            bool affectsEnemiesOnly)
        {
            Position = position;
            Radius = radius;
            StatusId = statusId;
            SourceCombatantId = sourceCombatantId;
            SourceFaction = sourceFaction;
            SourceTeam = sourceTeam;
            AffectsEnemiesOnly = affectsEnemiesOnly;
        }
    }

    /// <summary>
    /// Processes entity-attached auras: when a combatant has a status with AuraRadius > 0,
    /// nearby combatants within range receive (or lose) the AuraStatusId status.
    /// Checked at each combatant's turn end (matching BG3's "end of turn" aura behavior).
    /// 
    /// Concentration interaction: aura statuses (e.g., flaming_sphere_aura) are removed when
    /// the aura-bearing entity dies (e.g., from concentration break). The concentration system
    /// handles this via RemoveLinkedSummons() → entity death → all statuses on the entity are
    /// cleaned up, which stops the aura from applying further child statuses.
    /// </summary>
    public class AuraSystem
    {
        private readonly StatusManager _statusManager;
        private readonly Func<IEnumerable<Combatant>> _getCombatants;
        private readonly Func<string, Combatant> _resolveCombatant;

        public AuraSystem(
            StatusManager statusManager,
            Func<IEnumerable<Combatant>> getCombatants,
            Func<string, Combatant> resolveCombatant)
        {
            _statusManager = statusManager ?? throw new ArgumentNullException(nameof(statusManager));
            _getCombatants = getCombatants ?? throw new ArgumentNullException(nameof(getCombatants));
            _resolveCombatant = resolveCombatant ?? throw new ArgumentNullException(nameof(resolveCombatant));
        }

        /// <summary>
        /// Called at each combatant's turn end. Checks all active auras and applies/removes
        /// the aura status to/from this combatant based on proximity.
        /// </summary>
        public void ProcessTurnEndAuras(string combatantId)
        {
            var target = _resolveCombatant(combatantId);
            if (target == null || target.LifeState == CombatantLifeState.Dead)
                return;

            var allCombatants = _getCombatants();

            foreach (var auraSource in allCombatants)
            {
                if (auraSource.Id == combatantId) continue; // Don't aura yourself
                if (auraSource.LifeState == CombatantLifeState.Dead) continue;

                // Snapshot to list so LINQ doesn't re-evaluate during iteration
                var auraStatuses = _statusManager.GetStatuses(auraSource.Id)
                    .Where(s => s.Definition.AuraRadius > 0f && !string.IsNullOrEmpty(s.Definition.AuraStatusId))
                    .ToList();

                foreach (var auraStatus in auraStatuses)
                {
                    ProcessSingleAura(target, auraSource, auraStatus);
                }
            }
        }

        private void ProcessSingleAura(Combatant target, Combatant auraSource, StatusInstance auraStatus)
        {
            float distance = target.Position.DistanceTo(auraSource.Position);
            bool inRange = distance <= auraStatus.Definition.AuraRadius;
            string childStatusId = auraStatus.Definition.AuraStatusId;

            // Resolve child status definition to use its DefaultDuration instead of hardcoding
            var childDef = _statusManager.GetDefinition(childStatusId);
            int childDuration = childDef?.DefaultDuration ?? 1;

            // Check faction filter — Faction is an enum, so we compare directly
            bool isEnemy = target.Faction != auraSource.Faction;

            // Also respect Team string if both are set (allies can share a team)
            if (!string.IsNullOrEmpty(target.Team) && !string.IsNullOrEmpty(auraSource.Team))
            {
                if (string.Equals(target.Team, auraSource.Team, StringComparison.OrdinalIgnoreCase))
                    isEnemy = false;
            }

            if (auraStatus.Definition.AuraAffectsEnemiesOnly && !isEnemy)
                return; // Skip allies and self

            bool hasChildStatus = _statusManager.HasStatus(target.Id, childStatusId);

            if (inRange && !hasChildStatus)
            {
                // Apply the aura's child status.
                // Determine the effective source for DC purposes (summon's owner if applicable).
                int? saveDC = auraSource.OwnerSpellSaveDC;
                if (!saveDC.HasValue)
                {
                    // Fallback: try to get DC from the owner combatant
                    var owner = !string.IsNullOrEmpty(auraSource.OwnerId)
                        ? _resolveCombatant(auraSource.OwnerId)
                        : null;
                    if (owner?.OwnerSpellSaveDC.HasValue == true)
                        saveDC = owner.OwnerSpellSaveDC;
                    else
                        RuntimeSafety.Log($"[AuraSystem] Warning: No spell save DC for aura source {auraSource.Id}, using status default");
                }

                var instance = _statusManager.ApplyStatus(childStatusId, auraSource.Id, target.Id, duration: childDuration);
                if (instance != null && saveDC.HasValue)
                {
                    instance.SaveDCOverride = saveDC.Value;
                }

                RuntimeSafety.Log($"[AuraSystem] Applied '{childStatusId}' to {target.Id} " +
                    $"(within {auraStatus.Definition.AuraRadius}m aura of {auraSource.Id})");
            }
            else if (!inRange && hasChildStatus)
            {
                // NOTE: For transient aura children (duration=1), this removal branch rarely triggers
                // because the child status expires during ProcessTurnEnd. The re-apply next turn is
                // handled by the inRange && !hasChildStatus branch. This branch is still needed for
                // persistent aura children (duration > 1 or permanent).

                // Remove the child status if out of range.
                // Only remove instances sourced from this aura emitter.
                RemoveStatusFromSource(target.Id, childStatusId, auraSource.Id);

                RuntimeSafety.Log($"[AuraSystem] Removed '{childStatusId}' from {target.Id} " +
                    $"(left aura range of {auraSource.Id})");
            }
        }

        // ──────────────────────────────────────────────
        //  Query API (read-only, used by AI)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Returns all active auras in combat (statuses with AuraRadius > 0 on living combatants).
        /// </summary>
        public List<AuraInfo> GetActiveAuras()
        {
            var result = new List<AuraInfo>();
            var allCombatants = _getCombatants();

            foreach (var combatant in allCombatants)
            {
                if (combatant.LifeState == CombatantLifeState.Dead) continue;

                var auraStatuses = _statusManager.GetStatuses(combatant.Id)
                    .Where(s => s.Definition.AuraRadius > 0f && !string.IsNullOrEmpty(s.Definition.AuraStatusId))
                    .ToList();

                foreach (var auraStatus in auraStatuses)
                {
                    result.Add(new AuraInfo(
                        combatant.Position,
                        auraStatus.Definition.AuraRadius,
                        auraStatus.Definition.AuraStatusId,
                        combatant.Id,
                        combatant.Faction.ToString(),
                        combatant.Team ?? "",
                        auraStatus.Definition.AuraAffectsEnemiesOnly));
                }
            }

            return result;
        }

        /// <summary>
        /// Checks whether a position falls inside any friendly, hostile, or own aura
        /// relative to the querying combatant.  Convenience overload that recomputes
        /// the active-aura list on every call.
        /// </summary>
        public (bool InFriendly, bool InHostile, bool InOwnAura) IsPositionInAura(
            Vector3 position, string combatantId, string combatantFaction, string combatantTeam = null)
        {
            return IsPositionInAura(position, combatantId, combatantFaction, combatantTeam, GetActiveAuras());
        }

        /// <summary>
        /// Checks whether a position falls inside any friendly, hostile, or own aura
        /// relative to the querying combatant.  Uses a pre-computed aura list to avoid
        /// O(candidates × combatants × statuses) overhead when called in a tight loop.
        /// </summary>
        public (bool InFriendly, bool InHostile, bool InOwnAura) IsPositionInAura(
            Vector3 position, string combatantId, string combatantFaction,
            string combatantTeam, List<AuraInfo> cachedAuras)
        {
            bool inFriendly = false;
            bool inHostile = false;
            bool inOwnAura = false;

            foreach (var aura in cachedAuras)
            {
                float dist = position.DistanceTo(aura.Position);
                if (dist > aura.Radius) continue;

                bool sameSource = string.Equals(aura.SourceCombatantId, combatantId, StringComparison.OrdinalIgnoreCase);
                bool sameFaction = string.Equals(aura.SourceFaction, combatantFaction, StringComparison.OrdinalIgnoreCase);

                // Team override: if both sides share a Team string, treat as friendly
                // even when factions differ (mirrors ProcessSingleAura logic).
                bool sameTeam = !string.IsNullOrEmpty(aura.SourceTeam)
                    && !string.IsNullOrEmpty(combatantTeam)
                    && string.Equals(aura.SourceTeam, combatantTeam, StringComparison.OrdinalIgnoreCase);

                if (sameSource)
                {
                    inOwnAura = true;
                }
                else if (sameFaction || sameTeam)
                {
                    // Friendly aura — only counts if the aura doesn't exclusively affect enemies
                    if (!aura.AffectsEnemiesOnly)
                        inFriendly = true;
                }
                else
                {
                    // Different faction and different/no team — hostile aura
                    inHostile = true;
                }
            }

            return (inFriendly, inHostile, inOwnAura);
        }

        /// <summary>
        /// Remove a status from a combatant, but only if the SourceId matches.
        /// This prevents accidentally removing a same-named status applied by a different caster.
        /// </summary>
        private void RemoveStatusFromSource(string combatantId, string statusId, string sourceId)
        {
            var statuses = _statusManager.GetStatuses(combatantId);
            var instance = statuses.FirstOrDefault(s =>
                string.Equals(s.Definition.Id, statusId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.SourceId, sourceId, StringComparison.OrdinalIgnoreCase));

            if (instance != null)
            {
                _statusManager.RemoveStatusInstance(instance);
            }
        }
    }
}
