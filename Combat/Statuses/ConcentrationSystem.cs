using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Boosts;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Statuses
{
    /// <summary>
    /// Trigger source for concentration checks.
    /// </summary>
    public enum ConcentrationCheckTrigger
    {
        Damage,
        Prone,
        IncapacitatingEffect
    }

    /// <summary>
    /// Link to a sustained effect owned by concentration.
    /// </summary>
    public class ConcentrationEffectLink
    {
        /// <summary>
        /// The status effect applied by this concentration effect.
        /// </summary>
        public string StatusId { get; set; }

        /// <summary>
        /// The target affected by this status.
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// Optional precise status instance ID for exact cleanup.
        /// </summary>
        public string StatusInstanceId { get; set; }

        /// <summary>
        /// Optional surface ID linked to this concentration effect.
        /// When concentration breaks, the linked surface is also removed.
        /// </summary>
        public string SurfaceInstanceId { get; set; }
    }

    /// <summary>
    /// Unified contract describing an active concentration effect.
    /// </summary>
    public class ConcentrationInfo
    {
        /// <summary>
        /// The combatant who is concentrating.
        /// </summary>
        public string CombatantId { get; set; }

        /// <summary>
        /// The ability being concentrated on.
        /// </summary>
        public string ActionId { get; set; }

        /// <summary>
        /// The primary status effect applied by the concentration action.
        /// </summary>
        public string StatusId { get; set; }

        /// <summary>
        /// Primary target affected by the concentration effect.
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// When concentration started (Unix timestamp milliseconds).
        /// </summary>
        public long StartedAt { get; set; }

        /// <summary>
        /// Concrete sustained effects tied to this concentration.
        /// </summary>
        public List<ConcentrationEffectLink> LinkedEffects { get; set; } = new();
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
        /// Why the check was triggered.
        /// </summary>
        public ConcentrationCheckTrigger Trigger { get; set; }

        /// <summary>
        /// Damage taken, if this check came from damage.
        /// </summary>
        public int? DamageTaken { get; set; }

        /// <summary>
        /// Triggering status ID, if this check came from a status transition.
        /// </summary>
        public string StatusId { get; set; }

        /// <summary>
        /// The roll result (if a roll was made).
        /// </summary>
        public QueryResult RollResult { get; set; }
    }

    /// <summary>
    /// Manages concentration effects for BG3/5e-style mechanics.
    /// Only one concentration effect per combatant at a time.
    /// Concentration breaks on failed checks, incapacitation, death, or when starting a new concentration.
    /// </summary>
    public class ConcentrationSystem
    {
        private const int MinimumConcentrationDc = 10;

        private static readonly HashSet<string> ExplicitIncapacitatingStatusIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "paralyzed",
            "stunned",
            "asleep",
            "hypnotised",
            "hypnotic_pattern",
            "incapacitated",
            "unconscious"
        };

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

        /// <summary>
        /// Optional callback to remove surfaces when concentration breaks.
        /// Called with the surface instance ID string. Set by the arena/environment system.
        /// </summary>
        public Action<string> RemoveSurfaceById { get; set; }

        /// <summary>
        /// Optional callback to remove all surfaces created by a specific combatant.
        /// Called with the creator combatant ID. Called as a fallback when no explicit surface link exists.
        /// </summary>
        public Action<string> RemoveSurfacesByCreator { get; set; }

        public ConcentrationSystem(StatusManager statusManager, RulesEngine rulesEngine)
        {
            _statusManager = statusManager ?? throw new ArgumentNullException(nameof(statusManager));
            _rulesEngine = rulesEngine ?? throw new ArgumentNullException(nameof(rulesEngine));

            SubscribeToConcentrationEvents();
        }

        /// <summary>
        /// Subscribe to damage/death/status events that can affect concentration.
        /// </summary>
        private void SubscribeToConcentrationEvents()
        {
            var damageSub = _rulesEngine.Events.Subscribe(
                RuleEventType.DamageTaken,
                OnDamageTaken,
                priority: 50,
                ownerId: "ConcentrationSystem"
            );
            _eventSubscriptionIds.Add(damageSub.Id);

            var diedSub = _rulesEngine.Events.Subscribe(
                RuleEventType.CombatantDied,
                OnCombatantDied,
                priority: 50,
                ownerId: "ConcentrationSystem"
            );
            _eventSubscriptionIds.Add(diedSub.Id);

            var statusAppliedSub = _rulesEngine.Events.Subscribe(
                RuleEventType.StatusApplied,
                OnStatusApplied,
                priority: 50,
                ownerId: "ConcentrationSystem"
            );
            _eventSubscriptionIds.Add(statusAppliedSub.Id);
        }

        /// <summary>
        /// Handle damage taken events to check concentration.
        /// In BG3/5e, concentration breaks immediately when a combatant drops to 0 HP.
        /// </summary>
        private void OnDamageTaken(RuleEvent evt)
        {
            if (string.IsNullOrEmpty(evt.TargetId) || !IsConcentrating(evt.TargetId))
                return;

            int damageTaken = (int)evt.FinalValue;
            if (damageTaken <= 0)
                return;

            // DamageTaken is dispatched after life-state updates in damage effects.
            var combatant = ResolveCombatant?.Invoke(evt.TargetId);
            if (combatant != null && (combatant.LifeState == CombatantLifeState.Downed ||
                                      combatant.LifeState == CombatantLifeState.Dead))
            {
                BreakConcentration(evt.TargetId, "reduced to 0 HP");
                return;
            }

            var attacker = !string.IsNullOrEmpty(evt.SourceId) ? ResolveCombatant?.Invoke(evt.SourceId) : null;
            var result = CheckConcentration(evt.TargetId, damageTaken, attacker);
            if (!result.Maintained)
            {
                BreakConcentration(evt.TargetId, "failed concentration save");
            }
        }

        /// <summary>
        /// Handle status application events that affect concentration (e.g., prone/incapacitated).
        /// </summary>
        private void OnStatusApplied(RuleEvent evt)
        {
            if (string.IsNullOrWhiteSpace(evt.TargetId) || !IsConcentrating(evt.TargetId))
                return;

            if (!TryReadStatusId(evt, out var statusId) || string.IsNullOrWhiteSpace(statusId))
                return;

            var statusDefinition = _statusManager.GetDefinition(statusId);
            if (statusDefinition == null)
                return;

            if (IsProneStatus(statusDefinition))
            {
                var result = CheckConcentrationAgainstDc(
                    evt.TargetId,
                    MinimumConcentrationDc,
                    ConcentrationCheckTrigger.Prone,
                    statusId: statusDefinition.Id);

                if (!result.Maintained)
                {
                    BreakConcentration(evt.TargetId, "failed concentration save (prone)");
                }

                return;
            }

            if (IsIncapacitatingStatus(statusDefinition))
            {
                DispatchConcentrationCheckWindow(
                    evt.TargetId,
                    ConcentrationCheckTrigger.IncapacitatingEffect,
                    dc: MinimumConcentrationDc,
                    statusId: statusDefinition.Id,
                    autoBreak: true);

                string reason = string.IsNullOrWhiteSpace(statusDefinition.Name)
                    ? "became incapacitated"
                    : $"became incapacitated ({statusDefinition.Name})";

                BreakConcentration(evt.TargetId, reason);
            }
        }

        /// <summary>
        /// Handle combatant death events (e.g., from failed death saves).
        /// </summary>
        private void OnCombatantDied(RuleEvent evt)
        {
            if (string.IsNullOrEmpty(evt.TargetId))
                return;

            if (IsConcentrating(evt.TargetId))
            {
                BreakConcentration(evt.TargetId, "died");
            }
        }

        /// <summary>
        /// Start concentrating with explicit concentration contract data.
        /// If already concentrating, breaks the previous concentration.
        /// </summary>
        public void StartConcentration(ConcentrationInfo contract)
        {
            if (contract == null)
                throw new ArgumentNullException(nameof(contract));
            if (string.IsNullOrWhiteSpace(contract.CombatantId))
                throw new ArgumentNullException(nameof(contract.CombatantId));
            if (string.IsNullOrWhiteSpace(contract.ActionId))
                throw new ArgumentNullException(nameof(contract.ActionId));

            string combatantId = contract.CombatantId;

            if (_activeConcentrations.ContainsKey(combatantId))
            {
                BreakConcentration(combatantId, "started new concentration");
            }

            var info = NormalizeContract(contract);
            if (info.LinkedEffects.Count == 0)
            {
                CaptureLinkedEffects(info);
            }

            _activeConcentrations[combatantId] = info;
            OnConcentrationStarted?.Invoke(combatantId, info);

            _rulesEngine.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "ConcentrationStarted",
                SourceId = combatantId,
                TargetId = info.TargetId,
                ActionId = info.ActionId,
                Data = new Dictionary<string, object>
                {
                    { "statusId", info.StatusId }
                }
            });
        }

        /// <summary>
        /// Start concentrating on an action.
        /// Backward-compatible overload that builds a concentration contract.
        /// </summary>
        public void StartConcentration(string combatantId, string actionId, string statusId, string targetId)
        {
            StartConcentration(new ConcentrationInfo
            {
                CombatantId = combatantId,
                ActionId = actionId,
                StatusId = statusId,
                TargetId = targetId
            });
        }

        /// <summary>
        /// End concentration intentionally (free action in BG3/5e terms).
        /// </summary>
        public bool EndConcentration(string combatantId)
        {
            return BreakConcentration(combatantId, "manually ended");
        }

        /// <summary>
        /// Break concentration on the current effect and remove linked sustained effects.
        /// </summary>
        /// <param name="combatantId">The combatant whose concentration to break.</param>
        /// <param name="reason">Why concentration was broken.</param>
        /// <returns>True if concentration was broken, false if not concentrating.</returns>
        public bool BreakConcentration(string combatantId, string reason = "manually broken")
        {
            if (!_activeConcentrations.TryGetValue(combatantId, out var info))
                return false;

            _activeConcentrations.Remove(combatantId);

            bool removedTrackedEffects = RemoveTrackedEffects(info);
            if (!removedTrackedEffects && !string.IsNullOrWhiteSpace(info.StatusId))
            {
                RemoveStatusesBySourceAndStatus(combatantId, info.StatusId, info.TargetId);
            }

            // Remove linked surfaces when concentration breaks
            RemoveLinkedSurfaces(info);

            OnConcentrationBroken?.Invoke(combatantId, info, reason);

            _rulesEngine.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "ConcentrationBroken",
                SourceId = combatantId,
                TargetId = info.TargetId,
                ActionId = info.ActionId,
                Data = new Dictionary<string, object>
                {
                    { "statusId", info.StatusId },
                    { "reason", reason }
                }
            });

            _rulesEngine.RuleWindows.Dispatch(RuleWindow.OnConcentrationBroken, new RuleEventContext
            {
                Source = ResolveCombatant?.Invoke(combatantId),
                Target = !string.IsNullOrEmpty(info.TargetId) ? ResolveCombatant?.Invoke(info.TargetId) : null,
                Ability = null,
                Data =
                {
                    ["actionId"] = info.ActionId,
                    ["statusId"] = info.StatusId,
                    ["reason"] = reason
                }
            });

            return true;
        }

        /// <summary>
        /// Check if a combatant maintains concentration after taking damage.
        /// DC = max(10, damage / 2).
        /// </summary>
        public ConcentrationCheckResult CheckConcentration(string combatantId, int damageTaken, Combatant attacker = null)
        {
            int dc = Math.Max(MinimumConcentrationDc, damageTaken / 2);
            return CheckConcentrationAgainstDc(
                combatantId,
                dc,
                ConcentrationCheckTrigger.Damage,
                damageTaken: damageTaken,
                attacker: attacker);
        }

        private ConcentrationCheckResult CheckConcentrationAgainstDc(
            string combatantId,
            int dc,
            ConcentrationCheckTrigger trigger,
            int? damageTaken = null,
            string statusId = null,
            Combatant attacker = null)
        {
            var combatant = ResolveCombatant?.Invoke(combatantId);
            int conSaveBonus = GetConstitutionSaveBonus(combatant);

            var result = new ConcentrationCheckResult
            {
                DC = dc,
                Trigger = trigger,
                DamageTaken = damageTaken,
                StatusId = statusId,
                Maintained = true
            };

            var saveQuery = new QueryInput
            {
                Type = QueryType.SavingThrow,
                DC = dc,
                BaseValue = conSaveBonus,
                Target = combatant
            };
            saveQuery.Tags.Add("save:constitution");
            saveQuery.Tags.Add("concentration");

            var concentrationContext = BuildConcentrationCheckContext(
                combatant,
                saveQuery,
                trigger,
                dc,
                damageTaken,
                statusId,
                autoBreak: false,
                includeSaveTags: true);

            _rulesEngine.RuleWindows.Dispatch(RuleWindow.OnConcentrationCheck, concentrationContext);
            _rulesEngine.RuleWindows.Dispatch(RuleWindow.BeforeSavingThrow, concentrationContext);

            // Check for static Advantage(Concentration) boosts (e.g., War Caster feat)
            if (combatant != null && BoostEvaluator.HasAdvantage(combatant, RollType.Concentration))
                concentrationContext.AdvantageSources.Add("WarCaster");

            // Mage Slayer: attacker's hits impose concentration disadvantage
            if (attacker?.ResolvedCharacter?.Sheet?.FeatIds?.Any(f =>
                    string.Equals(f, "mage_slayer", StringComparison.OrdinalIgnoreCase)) == true)
                concentrationContext.DisadvantageSources.Add("MageSlayer");

            saveQuery.BaseValue += concentrationContext.TotalSaveBonus;
            if (concentrationContext.AdvantageSources.Count > 0)
            {
                saveQuery.Parameters["statusAdvantageSources"] = concentrationContext.AdvantageSources.ToList();
            }

            if (concentrationContext.DisadvantageSources.Count > 0)
            {
                saveQuery.Parameters["statusDisadvantageSources"] = concentrationContext.DisadvantageSources.ToList();
            }

            var saveResult = _rulesEngine.RollSave(saveQuery);
            result.RollResult = saveResult;
            result.Maintained = saveResult.IsSuccess;

            var afterSaveContext = BuildConcentrationCheckContext(
                combatant,
                saveQuery,
                trigger,
                dc,
                damageTaken,
                statusId,
                autoBreak: false,
                includeSaveTags: true);
            afterSaveContext.QueryResult = saveResult;

            _rulesEngine.RuleWindows.Dispatch(RuleWindow.AfterSavingThrow, afterSaveContext);

            OnConcentrationChecked?.Invoke(combatantId, result);
            return result;
        }

        private void DispatchConcentrationCheckWindow(
            string combatantId,
            ConcentrationCheckTrigger trigger,
            int dc,
            string statusId,
            bool autoBreak)
        {
            var combatant = ResolveCombatant?.Invoke(combatantId);
            var context = BuildConcentrationCheckContext(
                combatant,
                saveQuery: null,
                trigger,
                dc,
                damageTaken: null,
                statusId,
                autoBreak,
                includeSaveTags: false);

            _rulesEngine.RuleWindows.Dispatch(RuleWindow.OnConcentrationCheck, context);
        }

        private static ConcentrationInfo NormalizeContract(ConcentrationInfo contract)
        {
            return new ConcentrationInfo
            {
                CombatantId = contract.CombatantId,
                ActionId = contract.ActionId,
                StatusId = contract.StatusId,
                TargetId = string.IsNullOrWhiteSpace(contract.TargetId) ? contract.CombatantId : contract.TargetId,
                StartedAt = contract.StartedAt > 0
                    ? contract.StartedAt
                    : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                LinkedEffects = contract.LinkedEffects?
                    .Where(e => e != null)
                    .Select(CloneEffectLink)
                    .ToList() ?? new List<ConcentrationEffectLink>()
            };
        }

        private static ConcentrationEffectLink CloneEffectLink(ConcentrationEffectLink link)
        {
            return new ConcentrationEffectLink
            {
                StatusId = link.StatusId,
                TargetId = link.TargetId,
                StatusInstanceId = link.StatusInstanceId,
                SurfaceInstanceId = link.SurfaceInstanceId
            };
        }

        private void CaptureLinkedEffects(ConcentrationInfo info)
        {
            if (string.IsNullOrWhiteSpace(info.StatusId) || string.IsNullOrWhiteSpace(info.CombatantId))
                return;

            var matchingStatuses = _statusManager
                .GetAllStatuses()
                .Where(s =>
                    string.Equals(s.Definition.Id, info.StatusId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(s.SourceId, info.CombatantId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var status in matchingStatuses)
            {
                info.LinkedEffects.Add(new ConcentrationEffectLink
                {
                    StatusId = status.Definition.Id,
                    TargetId = status.TargetId,
                    StatusInstanceId = status.InstanceId
                });
            }

            // Backward-compatible fallback for effects that aren't represented as status instances.
            if (matchingStatuses.Count == 0 && !string.IsNullOrWhiteSpace(info.TargetId))
            {
                info.LinkedEffects.Add(new ConcentrationEffectLink
                {
                    StatusId = info.StatusId,
                    TargetId = info.TargetId
                });
            }
        }

        private bool RemoveTrackedEffects(ConcentrationInfo info)
        {
            if (info?.LinkedEffects == null || info.LinkedEffects.Count == 0)
                return false;

            bool removedAny = false;
            var statusByInstance = _statusManager
                .GetAllStatuses()
                .GroupBy(s => s.InstanceId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var link in info.LinkedEffects)
            {
                if (link == null || string.IsNullOrWhiteSpace(link.StatusId))
                    continue;

                if (!string.IsNullOrWhiteSpace(link.StatusInstanceId) &&
                    statusByInstance.TryGetValue(link.StatusInstanceId, out var instance))
                {
                    if (_statusManager.RemoveStatusInstance(instance))
                    {
                        removedAny = true;
                        continue;
                    }
                }

                if (!string.IsNullOrWhiteSpace(link.TargetId))
                {
                    if (_statusManager.RemoveStatus(link.TargetId, link.StatusId))
                    {
                        removedAny = true;
                    }
                }
            }

            return removedAny;
        }

        private bool RemoveStatusesBySourceAndStatus(string combatantId, string statusId, string fallbackTargetId)
        {
            var matchingStatuses = _statusManager
                .GetAllStatuses()
                .Where(s =>
                    string.Equals(s.Definition.Id, statusId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(s.SourceId, combatantId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var status in matchingStatuses)
            {
                _statusManager.RemoveStatusInstance(status);
            }

            if (matchingStatuses.Count > 0)
                return true;

            if (!string.IsNullOrWhiteSpace(fallbackTargetId))
                return _statusManager.RemoveStatus(fallbackTargetId, statusId);

            return false;
        }

        /// <summary>
        /// Remove surfaces linked to a concentration effect.
        /// First tries explicit surface links, then falls back to removing surfaces by creator ID.
        /// </summary>
        private void RemoveLinkedSurfaces(ConcentrationInfo info)
        {
            if (info == null)
                return;

            bool removedAnySurface = false;

            // Try explicit surface instance ID links
            if (info.LinkedEffects != null)
            {
                foreach (var link in info.LinkedEffects)
                {
                    if (!string.IsNullOrWhiteSpace(link.SurfaceInstanceId) && RemoveSurfaceById != null)
                    {
                        RemoveSurfaceById(link.SurfaceInstanceId);
                        removedAnySurface = true;
                    }
                }
            }

            // Fallback: remove surfaces created by this combatant if no explicit links were used
            if (!removedAnySurface && RemoveSurfacesByCreator != null && !string.IsNullOrWhiteSpace(info.CombatantId))
            {
                // Only remove if the action is known to create surfaces (concentration spells that produce area effects)
                var surfaceActionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "spirit_guardians", "darkness", "cloud_of_daggers", "moonbeam",
                    "flaming_sphere", "wall_of_fire", "hunger_of_hadar", "sleet_storm",
                    "spike_growth", "plant_growth", "stinking_cloud", "web",
                    "grease", "entangle", "fog_cloud", "silence"
                };

                if (!string.IsNullOrWhiteSpace(info.ActionId) && surfaceActionIds.Contains(info.ActionId))
                {
                    RemoveSurfacesByCreator(info.CombatantId);
                }
            }
        }

        private RuleEventContext BuildConcentrationCheckContext(
            Combatant combatant,
            QueryInput saveQuery,
            ConcentrationCheckTrigger trigger,
            int dc,
            int? damageTaken,
            string statusId,
            bool autoBreak,
            bool includeSaveTags)
        {
            var context = new RuleEventContext
            {
                Source = combatant,
                Target = combatant,
                QueryInput = saveQuery
            };

            context.Tags.Add("concentration");
            context.Tags.Add(GetTriggerTag(trigger));

            if (includeSaveTags)
            {
                context.Tags.Add("save:constitution");
            }

            context.Data["trigger"] = trigger.ToString();
            context.Data["dc"] = dc;
            context.Data["autoBreak"] = autoBreak;

            if (damageTaken.HasValue)
                context.Data["damageTaken"] = damageTaken.Value;

            if (!string.IsNullOrWhiteSpace(statusId))
                context.Data["statusId"] = statusId;

            return context;
        }

        private static bool TryReadStatusId(RuleEvent evt, out string statusId)
        {
            statusId = null;
            if (evt?.Data == null)
                return false;

            if (!evt.Data.TryGetValue("statusId", out var rawStatusId) || rawStatusId == null)
                return false;

            statusId = rawStatusId.ToString();
            return !string.IsNullOrWhiteSpace(statusId);
        }

        private static bool IsProneStatus(StatusDefinition definition)
        {
            return definition != null && string.Equals(definition.Id, "prone", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsIncapacitatingStatus(StatusDefinition definition)
        {
            if (definition == null)
                return false;

            if (ExplicitIncapacitatingStatusIds.Contains(definition.Id))
                return true;

            bool hasHardControlTag = definition.Tags != null &&
                                     definition.Tags.Any(tag => string.Equals(tag, "hard_control", StringComparison.OrdinalIgnoreCase));
            if (hasHardControlTag)
                return true;

            return !string.IsNullOrWhiteSpace(definition.Description) &&
                   definition.Description.Contains("incapacitated", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetTriggerTag(ConcentrationCheckTrigger trigger)
        {
            return trigger switch
            {
                ConcentrationCheckTrigger.Damage => "concentration:damage",
                ConcentrationCheckTrigger.Prone => "concentration:prone",
                ConcentrationCheckTrigger.IncapacitatingEffect => "concentration:incapacitating",
                _ => "concentration:unknown"
            };
        }

        private static int GetConstitutionSaveBonus(Combatant combatant)
        {
            if (combatant == null)
                return 0;

            int bonus = combatant.GetAbilityModifier(AbilityType.Constitution);
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
                    ActionId = info.ActionId,
                    StatusId = info.StatusId,
                    TargetId = info.TargetId,
                    StartedAt = info.StartedAt,
                    LinkedEffects = info.LinkedEffects
                        .Select(link => new Persistence.ConcentrationEffectSnapshot
                        {
                            StatusId = link.StatusId,
                            TargetId = link.TargetId,
                            StatusInstanceId = link.StatusInstanceId,
                            SurfaceInstanceId = link.SurfaceInstanceId ?? string.Empty
                        })
                        .ToList()
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
                    CombatantId = snapshot.CombatantId,
                    ActionId = snapshot.ActionId,
                    StatusId = snapshot.StatusId,
                    TargetId = snapshot.TargetId,
                    StartedAt = snapshot.StartedAt,
                    LinkedEffects = snapshot.LinkedEffects?
                        .Select(link => new ConcentrationEffectLink
                        {
                            StatusId = link.StatusId,
                            TargetId = link.TargetId,
                            StatusInstanceId = link.StatusInstanceId,
                            SurfaceInstanceId = link.SurfaceInstanceId
                        })
                        .ToList() ?? new List<ConcentrationEffectLink>()
                };
            }
        }
    }
}
