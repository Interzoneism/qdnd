using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Data.CharacterModel;
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
        public const string OpportunityAttackId = ReactionIds.OpportunityAttack;

        /// <summary>Well-known reaction ID for Shield spell reaction.</summary>
        public const string ShieldId = ReactionIds.Shield;

        /// <summary>Well-known reaction ID for Counterspell.</summary>
        public const string CounterspellId = ReactionIds.Counterspell;

        /// <summary>Well-known reaction ID for Uncanny Dodge.</summary>
        public const string UncannyDodgeId = "BG3_UncannyDodge";

        /// <summary>Well-known reaction ID for Deflect Missiles.</summary>
        public const string DeflectMissilesId = "BG3_DeflectMissiles";

        /// <summary>Well-known reaction ID for Hellish Rebuke.</summary>
        public const string HellishRebukeId = "BG3_HellishRebuke";

        /// <summary>Well-known reaction ID for Cutting Words (Bard).</summary>
        public const string CuttingWordsId = "BG3_CuttingWords";

        /// <summary>Well-known reaction ID for Sentinel (general).</summary>
        public const string SentinelId = "BG3_Sentinel";

        /// <summary>Well-known reaction ID for Sentinel OA enhancement.</summary>
        public const string SentinelOAId = "BG3_Sentinel_OA";

        /// <summary>Well-known reaction ID for Sentinel ally-defense variant.</summary>
        public const string SentinelAllyDefenseId = "BG3_Sentinel_AllyDefense";

        /// <summary>Well-known reaction ID for Mage Slayer.</summary>
        public const string MageSlayerId = "BG3_MageSlayer";

        /// <summary>Well-known reaction ID for War Caster.</summary>
        public const string WarCasterId = "BG3_WarCaster";

        /// <summary>Well-known reaction ID for Warding Flare (Light Cleric).</summary>
        public const string WardingFlareId = "BG3_WardingFlare";

        /// <summary>Well-known reaction ID for Destructive Wrath (handled inline by DealDamageEffect).</summary>
        public const string DestructiveWrathId = "BG3_DestructiveWrath";

        /// <summary>Well-known reaction ID for Bardic Inspiration (belongs in PassiveRuleProvider).</summary>
        public const string BardicInspirationId = "BG3_BardicInspiration";

        /// <summary>Well-known reaction ID for Defensive Duelist.</summary>
        public const string DefensiveDuelistId = "BG3_DefensiveDuelist";

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
            RegisterDeflectMissiles();
            RegisterHellishRebuke();
            RegisterCuttingWords();
            RegisterSentinelOA();
            RegisterSentinelAllyDefense();
            RegisterMageSlayer();
            RegisterWarCaster();
            RegisterWardingFlare();
            RegisterDefensiveDuelist();

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
        /// <param name="hasDeflectMissiles">Whether the combatant has the Deflect Missiles feature.</param>
        public void GrantCoreReactions(
            Combatant combatant,
            bool hasShield = false,
            bool hasCounterspell = false,
            bool hasUncannyDodge = false,
            bool hasDeflectMissiles = false,
            bool hasHellishRebuke = false,
            bool hasCuttingWords = false,
            bool hasSentinel = false,
            bool hasMageSlayer = false,
            bool hasWarCaster = false,
            bool hasWardingFlare = false,
            bool hasDefensiveDuelist = false)
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

            if (hasDeflectMissiles)
                _reactions.GrantReaction(combatant.Id, DeflectMissilesId);

            if (hasHellishRebuke)
                _reactions.GrantReaction(combatant.Id, HellishRebukeId);

            if (hasCuttingWords)
                _reactions.GrantReaction(combatant.Id, CuttingWordsId);

            if (hasSentinel)
            {
                _reactions.GrantReaction(combatant.Id, SentinelOAId);
                _reactions.GrantReaction(combatant.Id, SentinelAllyDefenseId);
            }

            if (hasMageSlayer)
                _reactions.GrantReaction(combatant.Id, MageSlayerId);

            if (hasWarCaster)
                _reactions.GrantReaction(combatant.Id, WarCasterId);

            if (hasWardingFlare)
                _reactions.GrantReaction(combatant.Id, WardingFlareId);

            if (hasDefensiveDuelist)
                _reactions.GrantReaction(combatant.Id, DefensiveDuelistId);
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
                Range = CombatRules.OpportunityAttackRangeMeters,
                CanCancel = false,
                CanModify = false,
                Tags = new HashSet<string> { "opportunity_attack", "melee", "bg3" },
                AIPolicy = ReactionAIPolicy.Always,
                ActionId = "main_hand_attack"
            };

            _reactions.RegisterReaction(definition);
            if (bg3Data != null)
                _reactionToInterrupt[definition.Id] = bg3Data;

            // Effect: use melee attack (actual attack resolution is handled by the combat pipeline;
            // the handler here marks the context so the combat system knows to queue the attack).
            _effectHandlers[OpportunityAttackId] = (reactor, context) =>
            {
                context.Data["executeAttack"] = true; // Backward compatibility marker
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
                Tags = new HashSet<string> { "shield", "spell", "ac_boost", "requires_hit", "bg3" },
                AIPolicy = ReactionAIPolicy.DamageThreshold,
                ActionId = "shield"
            };

            _reactions.RegisterReaction(definition);
            if (bg3Data != null)
                _reactionToInterrupt[definition.Id] = bg3Data;

            // Effect: apply AC+5 boost via BoostApplicator
            _effectHandlers[ShieldId] = (reactor, context) =>
            {
                // Remove any existing Shield reaction boosts to prevent permanent stacking
                BoostApplicator.RemoveBoosts(reactor, "Reaction", "Shield");

                // Shield grants +5 AC until start of next turn
                BoostApplicator.ApplyBoosts(reactor, "AC(5)", "Reaction", "Shield");
                context.Data["boostApplied"] = "AC(5)";
                context.Data["interruptId"] = "Interrupt_Shield";
                context.Data["acModifier"] = 5; // +5 AC for current attack re-evaluation

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
                Range = CombatRules.CounterspellRangeMeters,
                CanCancel = true,
                CanModify = false,
                Tags = new HashSet<string> { "counterspell", "spell", "cancel", "bg3" },
                AIPolicy = ReactionAIPolicy.PriorityTargets,
                ActionId = "counterspell"
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

        private void RegisterDeflectMissiles()
        {
            var definition = new ReactionDefinition
            {
                Id = DeflectMissilesId,
                Name = "Deflect Missiles",
                Description = "Use your reaction to reduce ranged weapon attack damage by 1d10 + DEX modifier + Monk Level.",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.YouAreHit },
                Priority = 20,
                Range = 0f,
                CanCancel = false,
                CanModify = true,
                Tags = new HashSet<string> { "deflect_missiles", "monk", "damage:reduce", "bg3" },
                AIPolicy = ReactionAIPolicy.DamageThreshold
            };

            _reactions.RegisterReaction(definition);

            // Effect: reduce incoming ranged weapon damage by 1d10 + DEX mod + monk level
            _effectHandlers[DeflectMissilesId] = (reactor, context) =>
            {
                // Only for ranged weapon attacks
                var attackTypeStr = context.Data?.ContainsKey("attackType") == true
                    ? context.Data["attackType"]?.ToString() : null;
                if (!string.Equals(attackTypeStr, "RangedWeapon", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(attackTypeStr, "rangedWeapon", StringComparison.OrdinalIgnoreCase))
                    return;

                // Calculate reduction: 1d10 + DEX mod + monk level
                int dexMod = reactor?.GetAbilityModifier(AbilityType.Dexterity) ?? 0;
                int monkLevel = reactor?.ResolvedCharacter?.Sheet?.GetClassLevel("monk") ?? 0;
                var rng = new Random();
                int reduction = rng.Next(1, 11) + dexMod + monkLevel;

                // Apply damage reduction via multiplier
                if (context.Data.TryGetValue("damageAmount", out var dmgObj) && dmgObj is int damageAmount && damageAmount > 0)
                {
                    int reducedDamage = Math.Max(0, damageAmount - reduction);
                    context.Data["damageMultiplier"] = (float)reducedDamage / damageAmount;
                }
                else
                {
                    // Fallback: store the flat reduction for the damage pipeline to use
                    context.Data["damageReduction"] = reduction;
                }
                context.Data["interruptId"] = "DeflectMissiles";
            };
        }

        private void RegisterHellishRebuke()
        {
            var definition = new ReactionDefinition
            {
                Id = HellishRebukeId,
                Name = "Hellish Rebuke",
                Description = "When hit by an attack, deal 2d10 fire damage to the attacker. DEX save for half.",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.YouAreHit },
                Priority = 40,
                Range = 0f,
                CanCancel = false,
                CanModify = false,
                Tags = new HashSet<string> { "hellish_rebuke", "spell", "costs_reaction", "costs_spell_slot", "bg3" },
                AIPolicy = ReactionAIPolicy.DamageThreshold
            };

            _reactions.RegisterReaction(definition);

            _effectHandlers[HellishRebukeId] = (reactor, context) =>
            {
                var rng = new Random();
                int damage = 0;
                for (int i = 0; i < 2; i++)
                    damage += rng.Next(1, 11); // 2d10

                context.Data["counterDamage"] = damage;
                context.Data["counterDamageType"] = "fire";
                context.Data["counterAttackTargetId"] = context.TriggerSourceId;
                context.Data["interruptId"] = "HellishRebuke";
            };
        }

        private void RegisterCuttingWords()
        {
            var definition = new ReactionDefinition
            {
                Id = CuttingWordsId,
                Name = "Cutting Words",
                Description = "Subtract your Bardic Inspiration die from an enemy's attack roll.",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.YouAreAttacked },
                Priority = 20,
                Range = 18f, // 60ft
                CanCancel = false,
                CanModify = true,
                Tags = new HashSet<string> { "cutting_words", "bard", "costs_reaction", "bg3" },
                AIPolicy = ReactionAIPolicy.DamageThreshold
            };

            _reactions.RegisterReaction(definition);

            _effectHandlers[CuttingWordsId] = (reactor, context) =>
            {
                var rng = new Random();
                int rollValue = rng.Next(1, 9); // 1d8 bardic inspiration die
                context.Data["rollModifier"] = -rollValue;
                context.Data["interruptId"] = "CuttingWords";
            };
        }

        private void RegisterSentinelOA()
        {
            var definition = new ReactionDefinition
            {
                Id = SentinelOAId,
                Name = "Sentinel (OA Enhancement)",
                Description = "Your opportunity attacks reduce the target's speed to 0.",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.EnemyLeavesReach },
                Priority = 9, // Before normal OA
                Range = CombatRules.OpportunityAttackRangeMeters,
                CanCancel = false,
                CanModify = false,
                Tags = new HashSet<string> { "sentinel", "oa_enhancement", "feat", "melee", "bg3" },
                AIPolicy = ReactionAIPolicy.Always,
                ActionId = "main_hand_attack"
            };

            _reactions.RegisterReaction(definition);

            _effectHandlers[SentinelOAId] = (reactor, context) =>
            {
                context.Data["targetSpeedZero"] = true;
                context.Data["executeAttack"] = true;
                context.Data["attackType"] = "melee";
                context.Data["interruptId"] = "Sentinel_OA";
            };
        }

        private void RegisterSentinelAllyDefense()
        {
            var definition = new ReactionDefinition
            {
                Id = SentinelAllyDefenseId,
                Name = "Sentinel (Ally Defense)",
                Description = "When an enemy hits an ally within 5ft of you, make a melee attack reaction.",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.AllyTakesDamage },
                Priority = 10,
                Range = CombatRules.DefaultMeleeReachMeters,
                CanCancel = false,
                CanModify = false,
                Tags = new HashSet<string> { "sentinel", "feat", "melee", "bg3" },
                AIPolicy = ReactionAIPolicy.Always,
                ActionId = "main_hand_attack"
            };

            _reactions.RegisterReaction(definition);

            _effectHandlers[SentinelAllyDefenseId] = (reactor, context) =>
            {
                context.Data["executeAttack"] = true;
                context.Data["attackType"] = "melee";
                context.Data["interruptId"] = "Sentinel_AllyDefense";
            };
        }

        private void RegisterMageSlayer()
        {
            var definition = new ReactionDefinition
            {
                Id = MageSlayerId,
                Name = "Mage Slayer",
                Description = "When an enemy within melee range casts a spell, make a reaction melee attack.",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.SpellCastNearby },
                Priority = 15,
                Range = CombatRules.DefaultMeleeReachMeters,
                CanCancel = false,
                CanModify = false,
                Tags = new HashSet<string> { "mage_slayer", "feat", "melee", "bg3" },
                AIPolicy = ReactionAIPolicy.Always,
                ActionId = "main_hand_attack"
            };

            _reactions.RegisterReaction(definition);

            _effectHandlers[MageSlayerId] = (reactor, context) =>
            {
                context.Data["executeAttack"] = true;
                context.Data["attackType"] = "melee";
                context.Data["interruptId"] = "MageSlayer";
            };
        }

        private void RegisterWarCaster()
        {
            var definition = new ReactionDefinition
            {
                Id = WarCasterId,
                Name = "War Caster",
                Description = "Cast a cantrip instead of making a melee opportunity attack.",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.EnemyLeavesReach },
                Priority = 8, // Higher priority than normal OA
                Range = CombatRules.DefaultMeleeReachMeters,
                CanCancel = false,
                CanModify = false,
                Tags = new HashSet<string> { "war_caster", "feat", "spell", "bg3" },
                AIPolicy = ReactionAIPolicy.Always
            };

            _reactions.RegisterReaction(definition);

            _effectHandlers[WarCasterId] = (reactor, context) =>
            {
                context.Data["executeSpell"] = true;
                context.Data["spellId"] = "shocking_grasp";
                context.Data["interruptId"] = "WarCaster";
            };
        }

        private void RegisterWardingFlare()
        {
            var definition = new ReactionDefinition
            {
                Id = WardingFlareId,
                Name = "Warding Flare",
                Description = "Impose disadvantage on an enemy's attack roll against you or an ally within 30ft.",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.YouAreAttacked },
                Priority = 20,
                Range = 9f, // 30ft
                CanCancel = false,
                CanModify = true,
                Tags = new HashSet<string> { "warding_flare", "cleric", "light_domain", "costs_reaction", "bg3" },
                AIPolicy = ReactionAIPolicy.DamageThreshold
            };

            _reactions.RegisterReaction(definition);

            _effectHandlers[WardingFlareId] = (reactor, context) =>
            {
                context.Data["rollModifier"] = -5; // Simplified disadvantage
                context.Data["disadvantageApplied"] = true;
                context.Data["interruptId"] = "WardingFlare";
            };
        }

        private void RegisterDefensiveDuelist()
        {
            var definition = new ReactionDefinition
            {
                Id = DefensiveDuelistId,
                Name = "Defensive Duelist",
                Description = "When hit with a melee attack while holding a finesse weapon, add proficiency bonus to AC.",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.YouAreAttacked },
                Priority = 20,
                Range = 0f,
                CanCancel = false,
                CanModify = true,
                Tags = new HashSet<string> { "defensive_duelist", "feat", "costs_reaction", "bg3" },
                AIPolicy = ReactionAIPolicy.DamageThreshold
            };

            _reactions.RegisterReaction(definition);

            _effectHandlers[DefensiveDuelistId] = (reactor, context) =>
            {
                // Only works against melee attacks
                var attackTypeStr = context.Data?.ContainsKey("attackType") == true
                    ? context.Data["attackType"]?.ToString() : null;
                if (attackTypeStr == null || !attackTypeStr.Contains("Melee", StringComparison.OrdinalIgnoreCase))
                    return;

                int profBonus = reactor?.ProficiencyBonus ?? 3;
                context.Data["acModifier"] = profBonus;
                context.Data["interruptId"] = "DefensiveDuelist";
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
