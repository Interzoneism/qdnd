using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Statuses
{
    /// <summary>
    /// Information about an active concentration effect.
    /// </summary>
    public class ConcentrationInfo
    {
        /// <summary>
        /// The ability being concentrated on.
        /// </summary>
        public string AbilityId { get; set; }

        /// <summary>
        /// The status effect applied by the concentration.
        /// </summary>
        public string StatusId { get; set; }

        /// <summary>
        /// The target affected by the concentration effect.
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// When concentration started (Unix timestamp milliseconds).
        /// </summary>
        public long StartedAt { get; set; }
    }

    /// <summary>
    /// Result of a concentration check.
    /// </summary>
    public class ConcentrationCheckResult
    {
        /// <summary>
        /// Whether concentration was maintained.
        /// </summary>
        public bool Maintained { get; set; }

        /// <summary>
        /// The DC that was required.
        /// </summary>
        public int DC { get; set; }

        /// <summary>
        /// The roll result (if a roll was made).
        /// </summary>
        public QueryResult RollResult { get; set; }
    }

    /// <summary>
    /// Manages concentration effects for BG3/5e-style mechanics.
    /// Only one concentration effect per combatant at a time.
    /// Concentration breaks on damage (with save) or when casting another concentration spell.
    /// </summary>
    public class ConcentrationSystem
    {
        private readonly Dictionary<string, ConcentrationInfo> _activeConcentrations = new();
        private readonly StatusManager _statusManager;
        private readonly RulesEngine _rulesEngine;
        private readonly List<string> _eventSubscriptionIds = new();

        /// <summary>
        /// Fired when concentration starts.
        /// </summary>
        public event Action<string, ConcentrationInfo> OnConcentrationStarted;

        /// <summary>
        /// Fired when concentration breaks (includes reason).
        /// </summary>
        public event Action<string, ConcentrationInfo, string> OnConcentrationBroken;

        /// <summary>
        /// Fired when a concentration check is made.
        /// </summary>
        public event Action<string, ConcentrationCheckResult> OnConcentrationChecked;

        /// <summary>
        /// Optional resolver to map combatant IDs to runtime combatants.
        /// Allows concentration saves to use the combatant's true CON save bonus/modifiers.
        /// </summary>
        public Func<string, Combatant> ResolveCombatant { get; set; }

        public ConcentrationSystem(StatusManager statusManager, RulesEngine rulesEngine)
        {
            _statusManager = statusManager ?? throw new ArgumentNullException(nameof(statusManager));
            _rulesEngine = rulesEngine ?? throw new ArgumentNullException(nameof(rulesEngine));

            SubscribeToDamageEvents();
        }

        /// <summary>
        /// Subscribe to damage events to trigger concentration checks.
        /// </summary>
        private void SubscribeToDamageEvents()
        {
            var sub = _rulesEngine.Events.Subscribe(
                RuleEventType.DamageTaken,
                OnDamageTaken,
                priority: 50, // Run before status removals
                ownerId: "ConcentrationSystem"
            );
            _eventSubscriptionIds.Add(sub.Id);
        }

        /// <summary>
        /// Handle damage taken events to check concentration.
        /// </summary>
        private void OnDamageTaken(RuleEvent evt)
        {
            if (string.IsNullOrEmpty(evt.TargetId))
                return;

            // Check if the damaged combatant is concentrating
            if (!IsConcentrating(evt.TargetId))
                return;

            int damageTaken = (int)evt.FinalValue;
            if (damageTaken <= 0)
                return;

            // Make concentration check
            var result = CheckConcentration(evt.TargetId, damageTaken);

            if (!result.Maintained)
            {
                BreakConcentration(evt.TargetId, "failed concentration save");
            }
        }

        /// <summary>
        /// Start concentrating on an ability.
        /// If already concentrating, breaks the previous concentration.
        /// </summary>
        /// <param name="combatantId">The combatant who is concentrating.</param>
        /// <param name="abilityId">The ability being concentrated on.</param>
        /// <param name="statusId">The status effect applied by the ability.</param>
        /// <param name="targetId">The target of the concentration effect.</param>
        public void StartConcentration(string combatantId, string abilityId, string statusId, string targetId)
        {
            if (string.IsNullOrEmpty(combatantId))
                throw new ArgumentNullException(nameof(combatantId));
            if (string.IsNullOrEmpty(abilityId))
                throw new ArgumentNullException(nameof(abilityId));

            // Break previous concentration if any
            if (_activeConcentrations.TryGetValue(combatantId, out var previous))
            {
                BreakConcentration(combatantId, "started new concentration");
            }

            // Record new concentration
            var info = new ConcentrationInfo
            {
                AbilityId = abilityId,
                StatusId = statusId,
                TargetId = targetId,
                StartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _activeConcentrations[combatantId] = info;
            OnConcentrationStarted?.Invoke(combatantId, info);

            // Dispatch event
            _rulesEngine.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "ConcentrationStarted",
                SourceId = combatantId,
                TargetId = targetId,
                AbilityId = abilityId,
                Data = new Dictionary<string, object>
                {
                    { "statusId", statusId }
                }
            });
        }

        /// <summary>
        /// Break concentration on the current effect.
        /// Removes the associated status effect.
        /// </summary>
        /// <param name="combatantId">The combatant whose concentration to break.</param>
        /// <param name="reason">Why concentration was broken.</param>
        /// <returns>True if concentration was broken, false if not concentrating.</returns>
        public bool BreakConcentration(string combatantId, string reason = "manually broken")
        {
            if (!_activeConcentrations.TryGetValue(combatantId, out var info))
                return false;

            // Remove the concentration tracking
            _activeConcentrations.Remove(combatantId);

            // Remove associated concentration statuses from all targets affected by this caster.
            // This supports multi-target concentration effects like Bless.
            if (!string.IsNullOrEmpty(info.StatusId))
            {
                var matchingStatuses = _statusManager
                    .GetAllStatuses()
                    .Where(s =>
                        string.Equals(s.Definition.Id, info.StatusId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(s.SourceId, combatantId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var status in matchingStatuses)
                {
                    _statusManager.RemoveStatusInstance(status);
                }

                // Fallback for older/snapshot states that may only track a target ID.
                if (matchingStatuses.Count == 0 && !string.IsNullOrEmpty(info.TargetId))
                {
                    _statusManager.RemoveStatus(info.TargetId, info.StatusId);
                }
            }

            OnConcentrationBroken?.Invoke(combatantId, info, reason);

            // Dispatch event
            _rulesEngine.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "ConcentrationBroken",
                SourceId = combatantId,
                TargetId = info.TargetId,
                AbilityId = info.AbilityId,
                Data = new Dictionary<string, object>
                {
                    { "statusId", info.StatusId },
                    { "reason", reason }
                }
            });

            return true;
        }

        /// <summary>
        /// Check if a combatant maintains concentration after taking damage.
        /// DC = max(10, damage / 2)
        /// </summary>
        /// <param name="combatantId">The combatant to check.</param>
        /// <param name="damageTaken">The amount of damage taken.</param>
        /// <returns>Result of the concentration check.</returns>
        public ConcentrationCheckResult CheckConcentration(string combatantId, int damageTaken)
        {
            // Calculate DC: max(10, damage / 2)
            int dc = Math.Max(10, damageTaken / 2);
            var combatant = ResolveCombatant?.Invoke(combatantId);
            int conSaveBonus = GetConstitutionSaveBonus(combatant);

            var result = new ConcentrationCheckResult
            {
                DC = dc,
                Maintained = true
            };

            // Roll Constitution save
            var saveQuery = new QueryInput
            {
                Type = QueryType.SavingThrow,
                DC = dc,
                BaseValue = conSaveBonus,
                Target = combatant
            };
            saveQuery.Tags.Add("save:constitution");
            saveQuery.Tags.Add("concentration");

            // War Caster: advantage on concentration saves
            if (combatant?.ResolvedCharacter?.Sheet?.FeatIds != null &&
                combatant.ResolvedCharacter.Sheet.FeatIds.Any(f =>
                    string.Equals(f, "war_caster", StringComparison.OrdinalIgnoreCase)))
            {
                var advantageSources = new List<string> { "War Caster" };
                saveQuery.Parameters["statusAdvantageSources"] = advantageSources;
            }

            var saveResult = _rulesEngine.RollSave(saveQuery);
            result.RollResult = saveResult;
            result.Maintained = saveResult.IsSuccess;

            OnConcentrationChecked?.Invoke(combatantId, result);

            return result;
        }

        private static int GetConstitutionSaveBonus(Combatant combatant)
        {
            if (combatant?.Stats == null)
                return 0;

            int bonus = combatant.Stats.ConstitutionModifier;
            if (combatant.ResolvedCharacter?.Proficiencies.IsProficientInSave(AbilityType.Constitution) == true)
            {
                bonus += Math.Max(0, combatant.ProficiencyBonus);
            }

            return bonus;
        }

        /// <summary>
        /// Check if a combatant is currently concentrating.
        /// </summary>
        public bool IsConcentrating(string combatantId)
        {
            return _activeConcentrations.ContainsKey(combatantId);
        }

        /// <summary>
        /// Get the current concentration effect for a combatant.
        /// </summary>
        public ConcentrationInfo GetConcentratedEffect(string combatantId)
        {
            return _activeConcentrations.TryGetValue(combatantId, out var info) ? info : null;
        }

        /// <summary>
        /// Get all active concentrations.
        /// </summary>
        public IReadOnlyDictionary<string, ConcentrationInfo> GetAllConcentrations()
        {
            return _activeConcentrations;
        }

        /// <summary>
        /// Clear concentration for a specific combatant without removing status.
        /// Used during cleanup when combatant is removed from combat.
        /// </summary>
        public void ClearCombatant(string combatantId)
        {
            _activeConcentrations.Remove(combatantId);
        }

        /// <summary>
        /// Reset all concentration state.
        /// </summary>
        public void Reset()
        {
            _activeConcentrations.Clear();
        }

        /// <summary>
        /// Unsubscribe from all events (cleanup).
        /// </summary>
        public void Dispose()
        {
            foreach (var subId in _eventSubscriptionIds)
            {
                _rulesEngine.Events.Unsubscribe(subId);
            }
            _eventSubscriptionIds.Clear();
        }

        /// <summary>
        /// Export concentration state for persistence.
        /// </summary>
        public List<Persistence.ConcentrationSnapshot> ExportState()
        {
            var snapshots = new List<Persistence.ConcentrationSnapshot>();

            foreach (var (combatantId, info) in _activeConcentrations)
            {
                snapshots.Add(new Persistence.ConcentrationSnapshot
                {
                    CombatantId = combatantId,
                    AbilityId = info.AbilityId,
                    StatusId = info.StatusId,
                    TargetId = info.TargetId,
                    StartedAt = info.StartedAt
                });
            }

            return snapshots;
        }

        /// <summary>
        /// Import concentration state from persistence.
        /// Does not trigger events.
        /// </summary>
        public void ImportState(List<Persistence.ConcentrationSnapshot> snapshots)
        {
            if (snapshots == null)
                return;

            _activeConcentrations.Clear();

            foreach (var snapshot in snapshots)
            {
                _activeConcentrations[snapshot.CombatantId] = new ConcentrationInfo
                {
                    AbilityId = snapshot.AbilityId,
                    StatusId = snapshot.StatusId,
                    TargetId = snapshot.TargetId,
                    StartedAt = snapshot.StartedAt
                };
            }
        }
    }
}
