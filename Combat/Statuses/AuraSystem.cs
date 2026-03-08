using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
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
        private const string AuraOfProtectionBuffPrefix = "aura_of_protection_buff_";

        private readonly StatusManager _statusManager;
        private readonly Func<IEnumerable<Combatant>> _getCombatants;
        private readonly Func<string, Combatant> _resolveCombatant;

        // Bug 5: Track which targets were already damaged by each aura owner this round.
        // Key = aura owner ID, Value = set of target IDs already hit this round.
        // Cleared at the start of the aura owner's own turn via ProcessTurnStartAuras.
        private readonly Dictionary<string, HashSet<string>> _damagedThisRound
            = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

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
        /// Called at each combatant's turn end. Checks all active end-of-turn auras and applies/removes
        /// the aura status to/from this combatant based on proximity.
        /// Skips auras with AuraTurnStart = true (those are handled by ProcessTurnStartAuras).
        /// </summary>
        public void ProcessTurnEndAuras(string combatantId)
        {
            var target = _resolveCombatant(combatantId);
            if (target == null || target.LifeState == CombatantLifeState.Dead
                || target.LifeState == CombatantLifeState.Unconscious)
                return;

            var allCombatants = _getCombatants();

            foreach (var auraSource in allCombatants)
            {
                if (auraSource.LifeState == CombatantLifeState.Dead
                    || auraSource.LifeState == CombatantLifeState.Unconscious)
                    continue;

                // Snapshot to list so LINQ doesn't re-evaluate during iteration
                var auraStatuses = _statusManager.GetStatuses(auraSource.Id)
                    .Where(s => s.Definition.AuraRadius > 0f
                             && !string.IsNullOrEmpty(s.Definition.AuraStatusId)
                             && !s.Definition.AuraTurnStart)  // skip turn-start auras
                    .ToList();

                foreach (var auraStatus in auraStatuses)
                {
                    ProcessSingleAura(target, auraSource, auraStatus, roundTracked: false);
                }
            }
        }

        /// <summary>
        /// Called at each combatant's turn start. Checks all active turn-start auras
        /// (AuraTurnStart = true, e.g. Spirit Guardians) and applies/removes the aura child
        /// status to/from this combatant based on proximity.
        /// Also clears this combatant's own round-damage tracking (as aura owner).
        /// </summary>
        /// <summary>
        /// Clears the entire round-damage tracking dictionary. Called at round end to ensure
        /// aura owners whose turns were skipped (stunned, killed) don't retain stale entries.
        /// </summary>
        public void ClearRoundTracking()
        {
            _damagedThisRound.Clear();
        }

        public void ProcessTurnStartAuras(string combatantId)
        {
            // Clear round tracking for this combatant as an aura owner so enemies
            // can be hit again in the new round.
            _damagedThisRound.Remove(combatantId);

            var target = _resolveCombatant(combatantId);
            if (target == null || target.LifeState == CombatantLifeState.Dead
                || target.LifeState == CombatantLifeState.Unconscious)
                return;

            var allCombatants = _getCombatants();

            foreach (var auraSource in allCombatants)
            {
                if (auraSource.LifeState == CombatantLifeState.Dead
                    || auraSource.LifeState == CombatantLifeState.Unconscious)
                    continue;

                var auraStatuses = _statusManager.GetStatuses(auraSource.Id)
                    .Where(s => s.Definition.AuraRadius > 0f
                             && !string.IsNullOrEmpty(s.Definition.AuraStatusId)
                             && s.Definition.AuraTurnStart)  // only turn-start auras
                    .ToList();

                foreach (var auraStatus in auraStatuses)
                {
                    ProcessSingleAura(target, auraSource, auraStatus, roundTracked: true);
                }
            }
        }

        private void ProcessSingleAura(Combatant target, Combatant auraSource, StatusInstance auraStatus, bool roundTracked)
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

            // Preserve historical behavior (no self-aura) for non-ally-only effects.
            // Ally-only auras (e.g. Paladin Aura of Protection) include self.
            bool isSelf = string.Equals(target.Id, auraSource.Id, StringComparison.OrdinalIgnoreCase);
            if (isSelf && !auraStatus.Definition.AuraAffectsAlliesOnly)
                return;

            if (auraStatus.Definition.AuraAffectsEnemiesOnly && !isEnemy)
                return; // Skip allies and self

            if (auraStatus.Definition.AuraAffectsAlliesOnly && isEnemy)
                return; // Skip enemies for ally-only auras

            bool hasChildStatus = _statusManager.HasStatus(target.Id, childStatusId);

            if (inRange && hasChildStatus)
            {
                // Keep Aura of Protection active while in range by refreshing duration.
                if (childStatusId.StartsWith(AuraOfProtectionBuffPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    _statusManager.ApplyStatus(childStatusId, auraSource.Id, target.Id, duration: childDuration);
                }
                return;
            }

            if (inRange && !hasChildStatus)
            {
                // Aura of Protection does not stack from multiple paladins; keep only the strongest bonus.
                if (!CanApplyAuraOfProtection(target, childStatusId, childDef))
                    return;

                // Bug 5: For turn-start auras, skip targets already damaged this round
                // to prevent double-damage (e.g. entered aura + turn-start both firing).
                if (roundTracked)
                {
                    if (_damagedThisRound.TryGetValue(auraSource.Id, out var alreadyHit)
                        && alreadyHit.Contains(target.Id))
                    {
                        return;
                    }
                }

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

                // Bug 5: Record this target as damaged this round for the aura owner.
                if (roundTracked)
                {
                    if (!_damagedThisRound.TryGetValue(auraSource.Id, out var hitSet))
                    {
                        hitSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        _damagedThisRound[auraSource.Id] = hitSet;
                    }
                    hitSet.Add(target.Id);
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

        private bool CanApplyAuraOfProtection(Combatant target, string childStatusId, StatusDefinition childDefinition)
        {
            if (childDefinition == null ||
                !childStatusId.StartsWith(AuraOfProtectionBuffPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            int incomingBonus = GetSavingThrowFlatBonus(childDefinition);
            var existingAuraStatuses = _statusManager.GetStatuses(target.Id)
                .Where(s => s.Definition.Id.StartsWith(AuraOfProtectionBuffPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            int strongestExistingBonus = existingAuraStatuses
                .Select(s => GetSavingThrowFlatBonus(s.Definition))
                .DefaultIfEmpty(int.MinValue)
                .Max();

            if (strongestExistingBonus > incomingBonus)
                return false;

            foreach (var existing in existingAuraStatuses)
            {
                if (GetSavingThrowFlatBonus(existing.Definition) <= incomingBonus)
                    _statusManager.RemoveStatusInstance(existing);
            }

            return true;
        }

        private static int GetSavingThrowFlatBonus(StatusDefinition definition)
        {
            if (definition == null)
                return 0;

            var modifier = definition.Modifiers.FirstOrDefault(m =>
                m.Target == ModifierTarget.SavingThrow && m.Type == ModifierType.Flat);
            if (modifier == null)
                return 0;

            return (int)MathF.Floor(modifier.Value);
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
