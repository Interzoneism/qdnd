using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Boosts;
using QDND.Data.Interrupts;

namespace QDND.Combat.Reactions
{
    /// <summary>
    /// Bridges parsed BG3 interrupt data into the existing <see cref="ReactionSystem"/>.
    /// Converts <see cref="BG3InterruptData"/> entries into <see cref="ReactionDefinition"/>
    /// objects and provides runtime hooks that execute interrupt effects (boost application,
    /// roll modification, counter-spelling, etc.) when reactions fire.
    ///
    /// <para><b>Core interrupts wired end-to-end:</b></para>
    /// <list type="bullet">
    ///   <item><b>Opportunity Attack</b> (<c>OnLeaveAttackRange</c>) — uses melee attack via existing action system.</item>
    ///   <item><b>Shield</b> (<c>OnPostRoll</c>) — grants AC+5 boost via <see cref="BoostApplicator"/>.</item>
    ///   <item><b>Counterspell</b> (<c>OnSpellCast</c>) — cancels the triggering spell cast.</item>
    ///   <item><b>Uncanny Dodge</b> (<c>OnPreDamage</c>) — halves incoming damage via modifier tag.</item>
    /// </list>
    ///
    /// Usage:
    /// <code>
    /// var integration = new BG3ReactionIntegration(reactionSystem, interruptRegistry);
    /// integration.RegisterCoreInterrupts();
    /// integration.GrantCoreReactions(combatant);
    /// </code>
    /// </summary>
    public class BG3ReactionIntegration
    {
        /// <summary>Well-known reaction ID for Opportunity Attack.</summary>
        public const string OpportunityAttackId = "BG3_OpportunityAttack";

        /// <summary>Well-known reaction ID for Shield spell reaction.</summary>
        public const string ShieldId = "BG3_Shield";

        /// <summary>Well-known reaction ID for Counterspell.</summary>
        public const string CounterspellId = "BG3_Counterspell";

        /// <summary>Well-known reaction ID for Uncanny Dodge.</summary>
        public const string UncannyDodgeId = "BG3_UncannyDodge";

        private readonly ReactionSystem _reactions;
        private readonly InterruptRegistry _registry;

        /// <summary>
        /// Mapping from registered BG3 reaction IDs to their corresponding
        /// <see cref="BG3InterruptData"/> source entry, for effect resolution.
        /// </summary>
        private readonly Dictionary<string, BG3InterruptData> _reactionToInterrupt = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Effect handlers keyed by reaction ID. When a reaction fires, the handler
        /// executes the interrupt's mechanical effects (boosts, damage mod, cancellation).
        /// </summary>
        private readonly Dictionary<string, Action<Combatant, ReactionTriggerContext>> _effectHandlers = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a new integration bridge.
        /// </summary>
        /// <param name="reactions">The core reaction system to register definitions into.</param>
        /// <param name="registry">The interrupt registry containing parsed BG3 data.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="reactions"/> is null.</exception>
        public BG3ReactionIntegration(ReactionSystem reactions, InterruptRegistry registry)
        {
            _reactions = reactions ?? throw new ArgumentNullException(nameof(reactions));
            _registry = registry; // May be null if only using manual registration
        }

        /// <summary>
        /// Registers the four core BG3 interrupts as <see cref="ReactionDefinition"/>s
        /// in the reaction system: Opportunity Attack, Shield, Counterspell, Uncanny Dodge.
        /// Also wires up effect handlers that fire when each reaction is used.
        /// </summary>
        public void RegisterCoreInterrupts()
        {
            RegisterOpportunityAttack();
            RegisterShield();
            RegisterCounterspell();
            RegisterUncannyDodge();

            // Subscribe to fire effects when reactions are used
            _reactions.OnReactionUsed -= HandleReactionUsed;
            _reactions.OnReactionUsed += HandleReactionUsed;
        }

        /// <summary>
        /// Converts all interrupts in the <see cref="InterruptRegistry"/> that match a given
        /// <see cref="BG3InterruptContext"/> to <see cref="ReactionDefinition"/>s and registers them.
        /// Useful for batch-loading all interrupts of a specific trigger type.
        /// </summary>
        /// <param name="context">The BG3 interrupt context to import.</param>
        /// <returns>Number of interrupts registered for this context.</returns>
        public int RegisterInterruptsByContext(BG3InterruptContext context)
        {
            if (_registry == null)
                return 0;

            var interrupts = _registry.GetInterruptsByContext(context);
            int count = 0;

            foreach (var interrupt in interrupts)
            {
                var definition = ConvertToReactionDefinition(interrupt);
                if (definition != null)
                {
                    _reactions.RegisterReaction(definition);
                    _reactionToInterrupt[definition.Id] = interrupt;
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Grants the four core reactions (opportunity attack, shield, counterspell, uncanny dodge)
        /// to a combatant. Only grants reactions the combatant is eligible for based on their
        /// capabilities. Called during combat initialization for each combatant.
        /// </summary>
        /// <param name="combatant">The combatant to grant reactions to.</param>
        /// <param name="hasShield">Whether the combatant knows the Shield spell.</param>
        /// <param name="hasCounterspell">Whether the combatant knows Counterspell.</param>
        /// <param name="hasUncannyDodge">Whether the combatant has the Uncanny Dodge feature.</param>
        public void GrantCoreReactions(Combatant combatant, bool hasShield = false, bool hasCounterspell = false, bool hasUncannyDodge = false)
        {
            if (combatant == null)
                return;

            // Everyone gets opportunity attacks
            _reactions.GrantReaction(combatant.Id, OpportunityAttackId);

            if (hasShield)
                _reactions.GrantReaction(combatant.Id, ShieldId);

            if (hasCounterspell)
                _reactions.GrantReaction(combatant.Id, CounterspellId);

            if (hasUncannyDodge)
                _reactions.GrantReaction(combatant.Id, UncannyDodgeId);
        }

        /// <summary>
        /// Gets the <see cref="BG3InterruptData"/> that backs a given reaction ID,
        /// if it was registered through this integration layer.
        /// </summary>
        /// <param name="reactionId">The reaction definition ID.</param>
        /// <returns>The source interrupt data, or null.</returns>
        public BG3InterruptData GetSourceInterrupt(string reactionId)
        {
            if (string.IsNullOrEmpty(reactionId))
                return null;
            _reactionToInterrupt.TryGetValue(reactionId, out var data);
            return data;
        }

        /// <summary>
        /// Manually execute the effect handler for a reaction, applying boosts/modifications
        /// to the reactor and trigger context. Used by the resolver after a reaction is confirmed.
        /// </summary>
        /// <param name="reactionId">The reaction definition ID.</param>
        /// <param name="reactor">The combatant using the reaction.</param>
        /// <param name="context">The trigger context.</param>
        /// <returns>True if a handler was found and executed.</returns>
        public bool ExecuteEffect(string reactionId, Combatant reactor, ReactionTriggerContext context)
        {
            if (_effectHandlers.TryGetValue(reactionId, out var handler))
            {
                handler(reactor, context);
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------
        //  Core Interrupt Registration
        // ---------------------------------------------------------------

        private void RegisterOpportunityAttack()
        {
            var bg3Data = _registry?.GetInterrupt("Interrupt_AttackOfOpportunity");

            var definition = new ReactionDefinition
            {
                Id = OpportunityAttackId,
                Name = bg3Data?.DisplayName ?? "Opportunity Attack",
                Description = bg3Data?.Description ?? "Attack an enemy moving out of your reach.",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.EnemyLeavesReach },
                Priority = 10,
                Range = 1.5f, // Melee reach
                CanCancel = false,
                CanModify = false,
                Tags = new HashSet<string> { "opportunity_attack", "melee", "bg3" },
                AIPolicy = ReactionAIPolicy.Always
            };

            _reactions.RegisterReaction(definition);
            if (bg3Data != null)
                _reactionToInterrupt[definition.Id] = bg3Data;

            // Effect: use melee attack (actual attack resolution is handled by the combat pipeline;
            // the handler here marks the context so the combat system knows to queue the attack).
            _effectHandlers[OpportunityAttackId] = (reactor, context) =>
            {
                context.Data["executeAttack"] = true;
                context.Data["attackType"] = "melee";
                context.Data["interruptId"] = "Interrupt_AttackOfOpportunity";
            };
        }

        private void RegisterShield()
        {
            var bg3Data = _registry?.GetInterrupt("Interrupt_Shield");

            var definition = new ReactionDefinition
            {
                Id = ShieldId,
                Name = bg3Data?.DisplayName ?? "Shield",
                Description = bg3Data?.Description ?? "When you are about to be hit, increase your AC by 5. Lasts until your next turn.",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.YouAreAttacked, ReactionTriggerType.YouTakeDamage },
                Priority = 20,
                Range = 0f,
                CanCancel = false,
                CanModify = true,
                Tags = new HashSet<string> { "shield", "spell", "ac_boost", "bg3" },
                AIPolicy = ReactionAIPolicy.DamageThreshold,
                ActionId = "shield"
            };

            _reactions.RegisterReaction(definition);
            if (bg3Data != null)
                _reactionToInterrupt[definition.Id] = bg3Data;

            // Effect: apply AC+5 boost via BoostApplicator
            _effectHandlers[ShieldId] = (reactor, context) =>
            {
                // Shield grants +5 AC until start of next turn
                BoostApplicator.ApplyBoosts(reactor, "AC(5)", "Reaction", "Shield");
                context.Data["boostApplied"] = "AC(5)";
                context.Data["interruptId"] = "Interrupt_Shield";

                // Mark that Shield was used so other systems can track duration
                context.Data["shieldActive"] = true;
            };
        }

        private void RegisterCounterspell()
        {
            var bg3Data = _registry?.GetInterrupt("Interrupt_Counterspell");

            var definition = new ReactionDefinition
            {
                Id = CounterspellId,
                Name = bg3Data?.DisplayName ?? "Counterspell",
                Description = bg3Data?.Description ?? "Stop a spell from being cast.",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.SpellCastNearby },
                Priority = 5, // Highest priority — must resolve before the spell lands
                Range = 18f, // 60 ft in D&D, ~18 Godot units
                CanCancel = true,
                CanModify = false,
                Tags = new HashSet<string> { "counterspell", "spell", "cancel", "bg3" },
                AIPolicy = ReactionAIPolicy.PriorityTargets
            };

            _reactions.RegisterReaction(definition);
            if (bg3Data != null)
                _reactionToInterrupt[definition.Id] = bg3Data;

            // Effect: cancel the triggering spell
            _effectHandlers[CounterspellId] = (reactor, context) =>
            {
                if (context.IsCancellable)
                {
                    context.WasCancelled = true;
                    context.Data["counterspelled"] = true;
                    context.Data["counterspellerLevel"] = 3; // Base level; higher-slot logic TBD
                    context.Data["interruptId"] = "Interrupt_Counterspell";
                }
            };
        }

        private void RegisterUncannyDodge()
        {
            // Uncanny Dodge: not in Interrupt.txt by default, but is a core D&D reaction.
            // BG3 handles it in passives, but we model it as a reaction for gameplay.
            var definition = new ReactionDefinition
            {
                Id = UncannyDodgeId,
                Name = "Uncanny Dodge",
                Description = "When an attacker you can see hits you, halve the attack's damage.",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.YouAreHit },
                Priority = 15,
                Range = 0f,
                CanCancel = false,
                CanModify = true,
                Tags = new HashSet<string> { "uncanny_dodge", "damage:half", "bg3" },
                AIPolicy = ReactionAIPolicy.DamageThreshold
            };

            _reactions.RegisterReaction(definition);

            // Effect: halve incoming damage
            _effectHandlers[UncannyDodgeId] = (reactor, context) =>
            {
                context.Data["damageMultiplier"] = 0.5f;
                context.Data["interruptId"] = "UncannyDodge";
            };
        }

        // ---------------------------------------------------------------
        //  Generic BG3 → ReactionDefinition conversion
        // ---------------------------------------------------------------

        /// <summary>
        /// Converts a <see cref="BG3InterruptData"/> entry into a <see cref="ReactionDefinition"/>.
        /// Maps the BG3 interrupt context to the closest <see cref="ReactionTriggerType"/>.
        /// </summary>
        /// <param name="interrupt">The BG3 interrupt data.</param>
        /// <returns>A reaction definition, or null if the interrupt could not be mapped.</returns>
        public static ReactionDefinition ConvertToReactionDefinition(BG3InterruptData interrupt)
        {
            if (interrupt == null || interrupt.IsContextStub)
                return null;

            var triggers = MapContextToTriggers(interrupt.InterruptContext);
            if (triggers.Count == 0)
                return null;

            var definition = new ReactionDefinition
            {
                Id = $"BG3_{interrupt.InterruptId}",
                Name = interrupt.DisplayName ?? interrupt.InterruptId,
                Description = interrupt.Description ?? string.Empty,
                Triggers = triggers,
                Priority = GetDefaultPriority(interrupt.InterruptContext),
                Range = interrupt.InterruptContextScope == BG3InterruptContextScope.Nearby ? 18f : 0f,
                CanCancel = interrupt.InterruptContext == BG3InterruptContext.OnSpellCast,
                CanModify = interrupt.InterruptContext == BG3InterruptContext.OnPreDamage ||
                            interrupt.InterruptContext == BG3InterruptContext.OnPostRoll,
                Tags = BuildTags(interrupt),
                AIPolicy = DeriveAIPolicy(interrupt)
            };

            return definition;
        }

        // ---------------------------------------------------------------
        //  Mapping helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Maps a BG3 <see cref="BG3InterruptContext"/> to the corresponding
        /// <see cref="ReactionTriggerType"/>(s) in the existing reaction system.
        /// </summary>
        internal static List<ReactionTriggerType> MapContextToTriggers(BG3InterruptContext context)
        {
            return context switch
            {
                BG3InterruptContext.OnLeaveAttackRange => new List<ReactionTriggerType> { ReactionTriggerType.EnemyLeavesReach },
                BG3InterruptContext.OnSpellCast => new List<ReactionTriggerType> { ReactionTriggerType.SpellCastNearby },
                BG3InterruptContext.OnPostRoll => new List<ReactionTriggerType> { ReactionTriggerType.YouAreAttacked },
                BG3InterruptContext.OnPreDamage => new List<ReactionTriggerType> { ReactionTriggerType.YouTakeDamage },
                BG3InterruptContext.OnCastHit => new List<ReactionTriggerType> { ReactionTriggerType.YouAreHit },
                _ => new List<ReactionTriggerType>()
            };
        }

        private static int GetDefaultPriority(BG3InterruptContext context)
        {
            // Lower = fires first.  Counterspell must beat everything.
            return context switch
            {
                BG3InterruptContext.OnSpellCast => 5,
                BG3InterruptContext.OnLeaveAttackRange => 10,
                BG3InterruptContext.OnPostRoll => 20,
                BG3InterruptContext.OnPreDamage => 30,
                BG3InterruptContext.OnCastHit => 40,
                _ => 50
            };
        }

        private static HashSet<string> BuildTags(BG3InterruptData interrupt)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bg3" };

            if (interrupt.CostsReaction)
                tags.Add("costs_reaction");
            if (interrupt.CostsSpellSlot)
                tags.Add("costs_spell_slot");
            if (interrupt.HasRoll)
                tags.Add("has_roll");
            if (!string.IsNullOrEmpty(interrupt.Stack))
                tags.Add($"stack:{interrupt.Stack}");

            return tags;
        }

        private static ReactionAIPolicy DeriveAIPolicy(BG3InterruptData interrupt)
        {
            if (string.IsNullOrEmpty(interrupt.InterruptDefaultValue))
                return ReactionAIPolicy.Always;

            var parts = interrupt.InterruptDefaultValue.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Equals("Enabled", StringComparison.OrdinalIgnoreCase))
                    return ReactionAIPolicy.Always;
                if (trimmed.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
                    return ReactionAIPolicy.Never;
            }

            // "Ask" or "Ask;Enabled" → prompt player, AI uses threshold heuristic
            return ReactionAIPolicy.DamageThreshold;
        }

        // ---------------------------------------------------------------
        //  Event handler
        // ---------------------------------------------------------------

        private void HandleReactionUsed(string reactorId, ReactionDefinition reaction, ReactionTriggerContext context)
        {
            // Only execute effects for reactions we own
            if (reaction == null || string.IsNullOrEmpty(reaction.Id))
                return;

            if (_effectHandlers.TryGetValue(reaction.Id, out var handler))
            {
                // Need to find the combatant — look it up from context data if available
                if (context.Data.TryGetValue("reactor", out var reactorObj) && reactorObj is Combatant reactor)
                {
                    handler(reactor, context);
                }
            }
        }
    }
}
