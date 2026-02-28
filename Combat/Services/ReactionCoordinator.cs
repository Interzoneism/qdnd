using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Arena;
using QDND.Combat.Entities;
using QDND.Combat.Reactions;
using QDND.Combat.States;
using QDND.Combat.Targeting;
using QDND.Combat.UI;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Owns all reaction-handling logic extracted from CombatArena.
    /// Handles reaction prompts, AI reaction decisions, reaction execution,
    /// target resolution for reactions, and baseline reaction grants during init.
    ///
    /// Circular-dependency note: ReactionCoordinator depends on EffectPipeline
    /// directly for OnReactionUsed execution. It does NOT hold a reference to
    /// ActionExecutionService, keeping the dependency graph acyclic.
    /// </summary>
    public class ReactionCoordinator
    {
        private readonly ReactionSystem _reactionSystem;
        private IReactionResolver _reactionResolver;
        private readonly Action<ReactionPrompt, Action<bool>> _showReactionPrompt;
        private readonly CombatStateMachine _stateMachine;
        private readonly EffectPipeline _effectPipeline;
        private readonly CombatContext _combatContext;
        private TargetValidator _targetValidator;
        private readonly TurnQueueService _turnQueue;
        private readonly List<Combatant> _combatants;
        private readonly Func<bool> _isAutoBattleMode;
        private readonly Func<Random> _getRng;
        private readonly Action<string> _log;

        public ReactionCoordinator(
            ReactionSystem reactionSystem,
            Action<ReactionPrompt, Action<bool>> showReactionPrompt,
            CombatStateMachine stateMachine,
            EffectPipeline effectPipeline,
            CombatContext combatContext,
            TargetValidator targetValidator,
            TurnQueueService turnQueue,
            List<Combatant> combatants,
            Func<bool> isAutoBattleMode,
            Func<Random> getRng,
            Action<string> log)
        {
            _reactionSystem = reactionSystem;
            _showReactionPrompt = showReactionPrompt;
            _stateMachine = stateMachine;
            _effectPipeline = effectPipeline;
            _combatContext = combatContext;
            _targetValidator = targetValidator;
            _turnQueue = turnQueue;
            _combatants = combatants;
            _isAutoBattleMode = isAutoBattleMode;
            _getRng = getRng;
            _log = log;
        }

        /// <summary>
        /// Inject the reaction resolver after construction. Required because the
        /// resolver's delegate properties (PromptDecisionProvider, AIDecisionProvider)
        /// reference coordinator methods, so the coordinator must be created first.
        /// </summary>
        public void SetReactionResolver(IReactionResolver resolver) =>
            _reactionResolver = resolver;

        /// <summary>
        /// Inject the target validator after construction. TargetValidator is created
        /// after the coordinator in the RegisterServices ordering.
        /// </summary>
        public void SetTargetValidator(TargetValidator targetValidator) =>
            _targetValidator = targetValidator;

        // ── Synchronous prompt decision (called by ReactionResolver) ─────────

        public bool? ResolveSynchronousReactionPromptDecision(ReactionPrompt prompt)
        {
            if (prompt == null)
                return false;

            var reactor = _combatContext.GetCombatant(prompt.ReactorId);
            if (reactor == null)
                return false;

            if (!reactor.IsPlayerControlled || _isAutoBattleMode())
                return DecideAIReaction(prompt);

            var policy = _reactionResolver?.GetPlayerReactionPolicy(prompt.ReactorId, prompt.Reaction?.Id)
                         ?? PlayerReactionPolicy.AlwaysAsk;

            return policy switch
            {
                PlayerReactionPolicy.AlwaysUse => true,
                PlayerReactionPolicy.NeverUse => false,
                _ => null
            };
        }

        // ── Reaction-used event handler (subscribed to ReactionSystem.OnReactionUsed) ─

        public void OnReactionUsed(string reactorId, ReactionDefinition reaction, ReactionTriggerContext triggerContext)
        {
            if (_effectPipeline == null || reaction == null)
                return;

            var reactor = _combatContext.GetCombatant(reactorId);
            if (reactor == null)
                return;

            if (triggerContext == null)
            {
                _log($"Reaction {reaction.Id} has no trigger context; skipping execution.");
                return;
            }

            var triggerSource = _combatContext.GetCombatant(triggerContext.TriggerSourceId);
            GetCombatLog()?.LogReactionUsed(
                reactor.Id,
                reactor.Name,
                reaction.Id,
                reaction.Name,
                triggerContext.TriggerType.ToString(),
                triggerSource?.Id ?? triggerContext.TriggerSourceId,
                triggerSource?.Name ?? triggerContext.TriggerSourceId);

            // Explicit, deterministic reaction execution path for OA-like effects.
            if (TryExecuteExplicitReaction(reaction, reactor, triggerContext))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(reaction.ActionId))
            {
                return;
            }

            ExecuteReactionAction(reaction.ActionId, reaction.Id, reactor, triggerContext);
        }

        private bool TryExecuteExplicitReaction(
            ReactionDefinition reaction,
            Combatant reactor,
            ReactionTriggerContext triggerContext)
        {
            if (triggerContext.Data == null)
            {
                return false;
            }

            if (triggerContext.Data.TryGetValue("executeAttack", out var attackObj)
                && attackObj is bool shouldExecuteAttack
                && shouldExecuteAttack)
            {
                string attackActionId = reaction.ActionId;
                if (string.IsNullOrWhiteSpace(attackActionId))
                {
                    attackActionId = "main_hand_attack";
                }

                return ExecuteReactionAction(attackActionId, reaction.Id, reactor, triggerContext);
            }

            if (triggerContext.Data.TryGetValue("executeSpell", out var spellObj)
                && spellObj is bool shouldExecuteSpell
                && shouldExecuteSpell)
            {
                string spellId = triggerContext.Data.TryGetValue("spellId", out var spellIdObj)
                    ? spellIdObj?.ToString()
                    : null;
                if (string.IsNullOrWhiteSpace(spellId))
                {
                    _log($"{reactor.Name}'s reaction {reaction.Id} requested executeSpell without spellId.");
                    return false;
                }

                return ExecuteReactionAction(spellId, reaction.Id, reactor, triggerContext);
            }

            return false;
        }

        private bool ExecuteReactionAction(
            string actionId,
            string reactionId,
            Combatant reactor,
            ReactionTriggerContext triggerContext)
        {
            var action = _effectPipeline.GetAction(actionId);
            if (action == null)
            {
                _log($"Reaction ability not found: {actionId} (reaction {reactionId})");
                return false;
            }

            var targets = ResolveReactionTargets(reactor, action, triggerContext);
            if (action.TargetType == TargetType.SingleUnit && targets.Count == 0)
            {
                _log($"No valid target for reaction ability {action.Id} from {reactor.Name}");
                return false;
            }

            var reactionOptions = new ActionExecutionOptions
            {
                SkipRangeValidation = true,
                SkipCostValidation = true,
                TriggerContext = triggerContext
            };

            var result = _effectPipeline.ExecuteAction(action.Id, reactor, targets, reactionOptions);
            if (!result.Success)
            {
                _log($"{reactor.Name}'s reaction ability {action.Id} failed: {result.ErrorMessage}");
                return false;
            }

            _log($"{reactor.Name} resolved reaction ability {action.Id}");
            return true;
        }

        // ── Internal target resolution ────────────────────────────────────────

        private List<Combatant> ResolveReactionTargets(Combatant reactor, ActionDefinition action, ReactionTriggerContext triggerContext)
        {
            if (action == null || reactor == null)
                return new List<Combatant>();

            switch (action.TargetType)
            {
                case TargetType.Self:
                    return new List<Combatant> { reactor };
                case TargetType.None:
                    return new List<Combatant>();
                case TargetType.All:
                    return _targetValidator != null
                        ? _targetValidator.GetValidTargets(action, reactor, _combatants)
                        : _combatants.Where(c => c.IsActive).ToList();
                case TargetType.Circle:
                case TargetType.Cone:
                case TargetType.Line:
                case TargetType.Point:
                case TargetType.Charge:
                case TargetType.WallSegment:
                {
                    if (_targetValidator == null || triggerContext == null)
                        return new List<Combatant>();
                    Vector3 GetPosition(Combatant c) => c.Position;
                    return _targetValidator.ResolveAreaTargets(
                        action,
                        reactor,
                        triggerContext.Position,
                        _combatants,
                        GetPosition);
                }
                case TargetType.SingleUnit:
                default:
                {
                    var target = _combatContext.GetCombatant(triggerContext?.TriggerSourceId ?? string.Empty)
                                 ?? _combatContext.GetCombatant(triggerContext?.AffectedId ?? string.Empty);
                    return target != null
                        ? new List<Combatant> { target }
                        : new List<Combatant>();
                }
            }
        }

        // ── Prompt event handler (subscribed to ReactionSystem.OnPromptCreated) ─

        public void OnReactionPrompt(ReactionPrompt prompt)
        {
            var reactor = _combatContext.GetCombatant(prompt.ReactorId);
            if (reactor == null)
            {
                _log($"Reactor not found: {prompt.ReactorId}");
                return;
            }

            var triggerSource = _combatContext.GetCombatant(prompt.TriggerContext?.TriggerSourceId ?? string.Empty);
            GetCombatLog()?.LogReactionTriggered(
                reactor.Id,
                reactor.Name,
                prompt.Reaction?.Id ?? string.Empty,
                prompt.Reaction?.Name ?? "Reaction",
                prompt.TriggerContext?.TriggerType.ToString() ?? "Unknown",
                triggerSource?.Id ?? prompt.TriggerContext?.TriggerSourceId,
                triggerSource?.Name ?? prompt.TriggerContext?.TriggerSourceId);

            if (reactor.IsPlayerControlled && (!_isAutoBattleMode() || QDND.Tools.DebugFlags.IsFullFidelity))
            {
                // Player-controlled in normal play: show UI and pause combat
                _stateMachine.TryTransition(CombatState.ReactionPrompt, $"Awaiting {reactor.Name}'s reaction decision");
                _showReactionPrompt(prompt, (useReaction) => HandleReactionDecision(prompt, useReaction));
                _log($"Reaction prompt shown to player: {prompt.Reaction.Name}");
            }
            else
            {
                // AI-controlled OR autobattle mode: auto-decide based on policy
                bool shouldUse = DecideAIReaction(prompt);
                HandleReactionDecision(prompt, shouldUse);
                string mode = _isAutoBattleMode() && reactor.IsPlayerControlled ? "AutoBattle" : "AI";
                _log($"{mode} auto-decided reaction: {(shouldUse ? "Use" : "Skip")} {prompt.Reaction.Name}");
            }
        }

        // ── AI policy decision ────────────────────────────────────────────────

        public bool DecideAIReaction(ReactionPrompt prompt)
        {
            switch (prompt.Reaction.AIPolicy)
            {
                case ReactionAIPolicy.Always:
                    return true;
                case ReactionAIPolicy.Never:
                    return false;
                case ReactionAIPolicy.DamageThreshold:
                    // Use if damage is significant (>25% of actor HP)
                    var reactor = _combatContext.GetCombatant(prompt.ReactorId);
                    if (reactor != null && prompt.TriggerContext.Value > reactor.Resources.MaxHP * 0.25f)
                        return true;
                    return false;
                case ReactionAIPolicy.Random:
                    return _getRng()?.Next(0, 2) == 1;
                case ReactionAIPolicy.PriorityTargets:
                    return true;
                default:
                    return false;
            }
        }

        // ── Decision resolution ───────────────────────────────────────────────

        public void HandleReactionDecision(ReactionPrompt prompt, bool useReaction)
        {
            prompt.Resolve(useReaction);

            var reactor = _combatContext.GetCombatant(prompt.ReactorId);
            if (reactor == null)
                return;

            if (useReaction)
            {
                bool used = _reactionSystem.UseReaction(reactor, prompt.Reaction, prompt.TriggerContext);
                if (used)
                {
                    _log($"{reactor.Name} used {prompt.Reaction.Name}");
                }
                else
                {
                    var triggerSource = _combatContext.GetCombatant(prompt.TriggerContext?.TriggerSourceId ?? string.Empty);
                    GetCombatLog()?.LogReactionDeclined(
                        reactor.Id,
                        reactor.Name,
                        prompt.Reaction?.Id ?? string.Empty,
                        prompt.Reaction?.Name ?? "Reaction",
                        prompt.TriggerContext?.TriggerType.ToString() ?? "Unknown",
                        triggerSource?.Id ?? prompt.TriggerContext?.TriggerSourceId,
                        triggerSource?.Name ?? prompt.TriggerContext?.TriggerSourceId,
                        "Reaction or required resources unavailable");
                    _log($"{reactor.Name} could not use {prompt.Reaction.Name}: missing reaction/resource budget");
                }
            }
            else
            {
                var triggerSource = _combatContext.GetCombatant(prompt.TriggerContext?.TriggerSourceId ?? string.Empty);
                GetCombatLog()?.LogReactionDeclined(
                    reactor.Id,
                    reactor.Name,
                    prompt.Reaction?.Id ?? string.Empty,
                    prompt.Reaction?.Name ?? "Reaction",
                    prompt.TriggerContext?.TriggerType.ToString() ?? "Unknown",
                    triggerSource?.Id ?? prompt.TriggerContext?.TriggerSourceId,
                    triggerSource?.Name ?? prompt.TriggerContext?.TriggerSourceId,
                    "Declined");
                _log($"{reactor.Name} skipped {prompt.Reaction.Name}");
            }

            // Resume combat flow - return to appropriate state
            var currentTurn = _turnQueue.CurrentCombatant;
            if (currentTurn != null)
            {
                var targetState = currentTurn.IsPlayerControlled
                    ? CombatState.PlayerDecision
                    : CombatState.AIDecision;
                _stateMachine.TryTransition(targetState, "Resuming after reaction decision");
            }
        }

        // ── Initialization: grant baseline reactions to all combatants ────────

        public void GrantBaselineReactions(IEnumerable<Combatant> combatants)
        {
            if (_reactionSystem == null || combatants == null)
                return;

            // Retrieve BG3 reaction integration if available
            var bg3Reactions = _combatContext?.GetService<BG3ReactionIntegration>();

            foreach (var combatant in combatants)
            {
                if (combatant == null)
                    continue;

                // Everyone in combat has baseline opportunity attack reaction.
                // Uses alias resolution, so legacy IDs still normalize to canonical.
                _reactionSystem.GrantReaction(combatant.Id, ReactionIds.OpportunityAttack);

                if (combatant.IsPlayerControlled)
                {
                    _reactionResolver?.SetPlayerDefaultPolicy(combatant.Id, PlayerReactionPolicy.AlwaysAsk);
                }

                // Grant BG3 reactions based on known BG3 spell IDs
                if (bg3Reactions != null && combatant.KnownActions != null)
                {
                    bool hasShield = combatant.KnownActions.Any(a =>
                        string.Equals(a, "shield", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a, "Projectile_Shield", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a, "Target_Shield", StringComparison.OrdinalIgnoreCase));
                    bool hasCounterspell = combatant.KnownActions.Any(a =>
                        string.Equals(a, "counterspell", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a, "Projectile_Counterspell", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a, "Target_Counterspell", StringComparison.OrdinalIgnoreCase));
                    bool hasUncannyDodge = combatant.PassiveIds?.Any(p =>
                        p.IndexOf("UncannyDodge", StringComparison.OrdinalIgnoreCase) >= 0) == true;
                    bool hasDeflectMissiles = combatant.ResolvedCharacter?.Features?.Any(f =>
                        string.Equals(f.Id, "deflect_missiles", StringComparison.OrdinalIgnoreCase)) == true;

                    // Hellish Rebuke: Tieflings get it racially, or any class that knows the spell
                    bool hasHellishRebuke = combatant.KnownActions?.Any(a =>
                        a.IndexOf("hellish_rebuke", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        a.IndexOf("HellishRebuke", StringComparison.OrdinalIgnoreCase) >= 0) == true;

                    // Cutting Words: Bard feature (College of Lore subclass)
                    bool hasCuttingWords = combatant.ResolvedCharacter?.Features?.Any(f =>
                        string.Equals(f.Id, "cutting_words", StringComparison.OrdinalIgnoreCase)) == true ||
                        combatant.PassiveIds?.Any(p =>
                        p.IndexOf("CuttingWords", StringComparison.OrdinalIgnoreCase) >= 0) == true;

                    // Sentinel: Feat — check features and passive IDs
                    bool hasSentinel = combatant.ResolvedCharacter?.Features?.Any(f =>
                        string.Equals(f.Id, "sentinel", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(f.Id, "Sentinel", StringComparison.OrdinalIgnoreCase)) == true ||
                        combatant.PassiveIds?.Any(p =>
                        p.IndexOf("Sentinel", StringComparison.OrdinalIgnoreCase) >= 0) == true;

                    // Mage Slayer: Feat — check features and passives
                    bool hasMageSlayer = combatant.ResolvedCharacter?.Features?.Any(f =>
                        string.Equals(f.Id, "mage_slayer", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(f.Id, "MageSlayer", StringComparison.OrdinalIgnoreCase)) == true ||
                        combatant.PassiveIds?.Any(p =>
                        p.IndexOf("MageSlayer", StringComparison.OrdinalIgnoreCase) >= 0) == true;

                    // War Caster: Feat — check features and passives
                    bool hasWarCaster = combatant.ResolvedCharacter?.Features?.Any(f =>
                        string.Equals(f.Id, "war_caster", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(f.Id, "WarCaster", StringComparison.OrdinalIgnoreCase)) == true ||
                        combatant.PassiveIds?.Any(p =>
                        p.IndexOf("WarCaster", StringComparison.OrdinalIgnoreCase) >= 0) == true;

                    // Warding Flare: Light Cleric domain feature
                    bool hasWardingFlare = combatant.ResolvedCharacter?.Features?.Any(f =>
                        string.Equals(f.Id, "warding_flare", StringComparison.OrdinalIgnoreCase)) == true ||
                        combatant.PassiveIds?.Any(p =>
                        p.IndexOf("WardingFlare", StringComparison.OrdinalIgnoreCase) >= 0) == true;

                    // Defensive Duelist: Feat — check features and passives
                    bool hasDefensiveDuelist = combatant.ResolvedCharacter?.Features?.Any(f =>
                        string.Equals(f.Id, "defensive_duelist", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(f.Id, "DefensiveDuelist", StringComparison.OrdinalIgnoreCase)) == true ||
                        combatant.PassiveIds?.Any(p =>
                        p.IndexOf("DefensiveDuelist", StringComparison.OrdinalIgnoreCase) >= 0) == true;

                    bg3Reactions.GrantCoreReactions(combatant, hasShield, hasCounterspell, hasUncannyDodge, hasDeflectMissiles,
                        hasHellishRebuke, hasCuttingWords, hasSentinel, hasMageSlayer, hasWarCaster, hasWardingFlare, hasDefensiveDuelist);
                }
                else
                {
                    if (combatant.KnownActions?.Contains("shield") == true)
                    {
                        _reactionSystem.GrantReaction(combatant.Id, ReactionIds.Shield);
                    }

                    if (combatant.KnownActions?.Contains("counterspell") == true)
                    {
                        _reactionSystem.GrantReaction(combatant.Id, ReactionIds.Counterspell);
                    }
                }
            }
        }

        private CombatLog GetCombatLog() => _combatContext?.GetService<CombatLog>();
    }
}
