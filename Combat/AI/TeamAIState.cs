using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Shared state for AI team coordination.
    /// Tracks focus targets, damage distribution, and role assignments across a faction.
    /// Reset at the start of each round.
    /// </summary>
    public class TeamAIState
    {
        /// <summary>
        /// Current round number.
        /// </summary>
        public int CurrentRound { get; private set; } = 0;

        /// <summary>
        /// Primary focus target for this round.
        /// </summary>
        public string FocusTargetId { get; set; }

        /// <summary>
        /// Damage dealt to each enemy this round by this team.
        /// </summary>
        public Dictionary<string, float> DamageDealtThisRound { get; } = new();

        /// <summary>
        /// CC (crowd control) applied to targets this round.
        /// </summary>
        public HashSet<string> CCAppliedThisRound { get; } = new();

        /// <summary>
        /// Heals cast this round (to avoid redundant healing).
        /// </summary>
        public Dictionary<string, float> HealsThisRound { get; } = new();

        /// <summary>
        /// Who has already acted this round.
        /// </summary>
        public HashSet<string> ActedThisRound { get; } = new();

        /// <summary>
        /// Role assignments for team members.
        /// </summary>
        public Dictionary<string, AIArchetype> RoleAssignments { get; } = new();

        /// <summary>
        /// Reset state for a new round.
        /// </summary>
        public void BeginNewRound()
        {
            CurrentRound++;
            DamageDealtThisRound.Clear();
            CCAppliedThisRound.Clear();
            HealsThisRound.Clear();
            ActedThisRound.Clear();
        }

        /// <summary>
        /// Record that damage was dealt to a target.
        /// </summary>
        public void RecordDamage(string targetId, float damage)
        {
            if (!DamageDealtThisRound.ContainsKey(targetId))
                DamageDealtThisRound[targetId] = 0;
            DamageDealtThisRound[targetId] += damage;
        }

        /// <summary>
        /// Record that CC was applied to a target.
        /// </summary>
        public void RecordCC(string targetId)
        {
            CCAppliedThisRound.Add(targetId);
        }

        /// <summary>
        /// Record that healing was applied to an ally.
        /// </summary>
        public void RecordHeal(string targetId, float amount)
        {
            if (!HealsThisRound.ContainsKey(targetId))
                HealsThisRound[targetId] = 0;
            HealsThisRound[targetId] += amount;
        }

        /// <summary>
        /// Record that a combatant has acted.
        /// </summary>
        public void RecordActed(string combatantId)
        {
            ActedThisRound.Add(combatantId);
        }

        /// <summary>
        /// Determine the best focus target based on damaged enemies.
        /// Prefers enemies that are already damaged (to finish them quickly).
        /// </summary>
        public string DetermineFocusTarget(IEnumerable<Combatant> enemies)
        {
            if (FocusTargetId != null)
            {
                // Verify focus target is still alive
                var focusTarget = enemies.FirstOrDefault(e => e.Id == FocusTargetId);
                if (focusTarget != null && focusTarget.Resources?.CurrentHP > 0)
                    return FocusTargetId;
                FocusTargetId = null;
            }

            // Pick the most damaged enemy that isn't dead
            Combatant bestTarget = null;
            float bestScore = float.MinValue;

            foreach (var enemy in enemies)
            {
                if (enemy.Resources?.CurrentHP <= 0) continue;

                float score = 0;
                
                // Prefer already-damaged enemies (focus fire completion)
                float hpPercent = (float)enemy.Resources.CurrentHP / Math.Max(1, enemy.Resources.MaxHP);
                if (hpPercent < 1f)
                {
                    score += (1f - hpPercent) * 10f; // More damaged = higher priority
                }
                
                // Bonus for enemies we've already hit this round
                if (DamageDealtThisRound.ContainsKey(enemy.Id))
                {
                    score += 5f; // Strong preference to continue attacking same target
                }

                // Penalty for already-CC'd enemies
                if (CCAppliedThisRound.Contains(enemy.Id))
                {
                    score -= 3f; // Slight discouragement
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = enemy;
                }
            }

            FocusTargetId = bestTarget?.Id;
            return FocusTargetId;
        }

        /// <summary>
        /// Get focus fire score bonus for attacking a specific target.
        /// </summary>
        public float GetFocusFireBonus(string targetId)
        {
            if (targetId == FocusTargetId)
                return 4f; // Strong bonus for attacking focus target
            
            // Smaller bonus for attacking enemies we've already damaged this round
            if (DamageDealtThisRound.ContainsKey(targetId))
                return 2f;
            
            return 0f;
        }

        /// <summary>
        /// Check if a target already has CC applied this round (to avoid redundancy).
        /// </summary>
        public bool IsAlreadyCCd(string targetId)
        {
            return CCAppliedThisRound.Contains(targetId);
        }
    }
}
