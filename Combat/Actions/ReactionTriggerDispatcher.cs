using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Reactions;

namespace QDND.Combat.Actions
{
    public class ReactionTriggerDispatcher
    {
        public ReactionSystem Reactions { get; set; }
        public IReactionResolver ReactionResolver { get; set; }
        public Func<IEnumerable<Combatant>> GetCombatants { get; set; }

        public event Action<ReactionTriggerEventArgs> OnDamageTrigger;
        public event Action<ReactionTriggerEventArgs> OnAbilityCastTrigger;
        public event Action<ReactionTriggerEventArgs> OnAttackTrigger;
        public event Action<ReactionTriggerEventArgs> OnHitTrigger;

        /// <summary>
        /// Trigger ability cast reactions with effective tags.
        /// </summary>
        public ReactionTriggerEventArgs TryTriggerAbilityCastReactionsWithTags(
            Combatant source,
            ActionDefinition action,
            List<Combatant> targets,
            HashSet<string> effectiveTags,
            ActionExecutionOptions options = null)
        {
            if (Reactions == null || GetCombatants == null)
                return null;

            bool isSpell = effectiveTags.Contains("spell") || effectiveTags.Contains("magic");
            if (!isSpell)
                return null;

            var context = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.SpellCastNearby,
                TriggerSourceId = source.Id,
                AffectedId = targets.FirstOrDefault()?.Id,
                ActionId = action.Id,
                TriggerSpellLevel = action.SpellLevel + (options?.UpcastLevel ?? 0),
                Position = source.Position,
                IsCancellable = !effectiveTags.Contains("uncounterable"),
                Data = new Dictionary<string, object>
                {
                    { "actionName", action.Name },
                    { "targetCount", targets.Count },
                    { "priorityTarget", (object)true }
                }
            };

            var potentialReactors = GetCombatants()
                .Where(c => c.Id != source.Id && c.Faction != source.Faction);
            var potentialList = potentialReactors.ToList();

            List<(string CombatantId, ReactionDefinition Reaction)> eligibleReactors;
            bool cancelledByResolver = false;
            if (ReactionResolver != null)
            {
                var resolution = ReactionResolver.ResolveTrigger(
                    context,
                    potentialList,
                    new ReactionResolutionOptions
                    {
                        ActionLabel = $"ability:{action.Id}",
                        AllowPromptDeferral = false
                    });
                eligibleReactors = resolution.EligibleReactors;
                cancelledByResolver = resolution.TriggerCancelled;
            }
            else
            {
                eligibleReactors = Reactions.GetEligibleReactors(context, potentialList);
            }

            var args = new ReactionTriggerEventArgs
            {
                Context = context,
                EligibleReactors = eligibleReactors,
                Cancel = cancelledByResolver
            };

            if (eligibleReactors.Count > 0)
            {
                OnAbilityCastTrigger?.Invoke(args);
            }

            return args;
        }

        /// <summary>
        /// Check for SpellCastNearby reactions when an ability is cast.
        /// Returns the trigger args with eligible reactors, or null if no reactions system.
        /// </summary>
        public ReactionTriggerEventArgs TryTriggerAbilityCastReactions(
            Combatant source,
            ActionDefinition action,
            List<Combatant> targets)
        {
            if (Reactions == null || GetCombatants == null)
                return null;

            // Only trigger for abilities with "spell" tag or similar
            bool isSpell = action.Tags.Contains("spell") || action.Tags.Contains("magic");
            if (!isSpell)
                return null;

            // Create trigger context
            var context = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.SpellCastNearby,
                TriggerSourceId = source.Id,
                ActionId = action.Id,
                Position = source.Position,
                IsCancellable = !action.Tags.Contains("uncounterable"),
                Data = new Dictionary<string, object>
                {
                    { "actionName", action.Name },
                    { "targetCount", targets.Count },
                    { "priorityTarget", (object)true }
                }
            };

            // Get all combatants that could react (enemies of the caster)
            var potentialReactors = GetCombatants()
                .Where(c => c.Id != source.Id && c.Faction != source.Faction);

            var eligibleReactors = Reactions.GetEligibleReactors(context, potentialReactors);

            var args = new ReactionTriggerEventArgs
            {
                Context = context,
                EligibleReactors = eligibleReactors,
                Cancel = false
            };

            // Fire the event if there are eligible reactors
            if (eligibleReactors.Count > 0)
            {
                OnAbilityCastTrigger?.Invoke(args);
            }

            return args;
        }

        /// <summary>
        /// Check for damage reactions when damage is about to be dealt.
        /// Returns the trigger args with eligible reactors, or null if no reactions system.
        /// </summary>
        public ReactionTriggerEventArgs TryTriggerDamageReactions(
            Combatant source,
            Combatant target,
            int damageAmount,
            string damageType,
            string actionId = null)
        {
            if (Reactions == null || GetCombatants == null)
                return null;

            // Create trigger context for YouTakeDamage (target's perspective)
            var context = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.YouTakeDamage,
                TriggerSourceId = source.Id,
                AffectedId = target.Id,
                ActionId = actionId,
                Value = damageAmount,
                Position = target.Position,
                IsCancellable = false, // Damage is generally not cancellable, but can be modified
                Data = new Dictionary<string, object>
                {
                    { "damageType", damageType ?? "untyped" },
                    { "originalDamage", damageAmount }
                }
            };

            // Get eligible reactors (the target and potentially allies)
            var eligibleReactors = new List<(string CombatantId, ReactionDefinition Reaction)>();
            float damageModifier = 1.0f;

            // Check target for YouTakeDamage reactions (like Shield)
            if (ReactionResolver != null)
            {
                var selfResolution = ReactionResolver.ResolveTrigger(
                    context,
                    new[] { target },
                    new ReactionResolutionOptions
                    {
                        ActionLabel = $"damage:{actionId ?? "unknown"}:self",
                        AllowPromptDeferral = false
                    });
                eligibleReactors.AddRange(selfResolution.EligibleReactors);
                damageModifier *= selfResolution.DamageModifier;
            }
            else
            {
                eligibleReactors.AddRange(Reactions.GetEligibleReactors(context, new[] { target }));
            }

            // Also check for AllyTakesDamage reactions from allies
            var allyContext = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.AllyTakesDamage,
                TriggerSourceId = source.Id,
                AffectedId = target.Id,
                ActionId = actionId,
                Value = damageAmount,
                Position = target.Position,
                IsCancellable = false,
                Data = new Dictionary<string, object>
                {
                    { "damageType", damageType ?? "untyped" },
                    { "originalDamage", damageAmount }
                }
            };

            var allies = GetCombatants()
                .Where(c => c.Id != target.Id && c.Faction == target.Faction);
            var allyList = allies.ToList();
            if (ReactionResolver != null)
            {
                var allyResolution = ReactionResolver.ResolveTrigger(
                    allyContext,
                    allyList,
                    new ReactionResolutionOptions
                    {
                        ActionLabel = $"damage:{actionId ?? "unknown"}:ally",
                        AllowPromptDeferral = false
                    });
                eligibleReactors.AddRange(allyResolution.EligibleReactors);
                damageModifier *= allyResolution.DamageModifier;
            }
            else
            {
                eligibleReactors.AddRange(Reactions.GetEligibleReactors(allyContext, allyList));
            }

            var args = new ReactionTriggerEventArgs
            {
                Context = context,
                EligibleReactors = eligibleReactors,
                Cancel = false,
                DamageModifier = damageModifier
            };

            // Fire the event if there are eligible reactors
            if (eligibleReactors.Count > 0)
            {
                OnDamageTrigger?.Invoke(args);
            }

            return args;
        }

        /// <summary>
        /// Fires AllyDowned reactions for allies of a combatant reduced to 0 HP.
        /// </summary>
        public void TryTriggerAllyDownedReactions(Combatant killer, Combatant downed)
        {
            if (downed == null || Reactions == null || GetCombatants == null)
                return;

            var context = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.AllyDowned,
                TriggerSourceId = killer?.Id ?? string.Empty,
                AffectedId = downed.Id,
                Position = downed.Position,
                IsCancellable = false,
                Data = new Dictionary<string, object>
                {
                    { "downedFaction", downed.Faction.ToString() }
                }
            };

            var allies = GetCombatants()
                .Where(c => c.Id != downed.Id && c.Faction == downed.Faction && c.IsActive)
                .ToList();

            if (allies.Count == 0)
                return;

            if (ReactionResolver != null)
            {
                ReactionResolver.ResolveTrigger(
                    context,
                    allies,
                    new ReactionResolutionOptions
                    {
                        ActionLabel = $"ally_downed:{downed.Id}",
                        AllowPromptDeferral = false
                    });
                return;
            }

            var eligibleReactors = Reactions.GetEligibleReactors(context, allies);
            foreach (var (combatantId, reaction) in eligibleReactors)
            {
                Reactions.CreatePrompt(combatantId, reaction, context);
            }
        }

        /// <summary>
        /// Fires YouAreAttacked reactions after an attack roll is made but before effects execute.
        /// Gives reactions like Shield, Cutting Words, Warding Flare, and Defensive Duelist
        /// a chance to modify AC or the roll.
        /// Returns the trigger args with ACModifier and RollModifier.
        /// </summary>
        public ReactionTriggerEventArgs TryTriggerAttackReactions(
            Combatant attacker,
            Combatant target,
            ActionDefinition action,
            string attackType = null,
            bool attackHit = true)
        {
            if (Reactions == null || GetCombatants == null)
                return null;

            var context = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.YouAreAttacked,
                TriggerSourceId = attacker.Id,
                AffectedId = target.Id,
                ActionId = action?.Id,
                Position = target.Position,
                IsCancellable = false,
                Data = new Dictionary<string, object>
                {
                    { "attackType", attackType ?? "unknown" },
                    { "actionId", action?.Id ?? "unknown" },
                    { "attackerId", attacker.Id },
                    { "attackWouldHit", (object)attackHit }
                }
            };

            var eligibleReactors = new List<(string CombatantId, ReactionDefinition Reaction)>();
            int acModifier = 0;
            int rollModifier = 0;

            // Check target for YouAreAttacked reactions (e.g., Shield, Defensive Duelist)
            if (ReactionResolver != null)
            {
                var selfResolution = ReactionResolver.ResolveTrigger(
                    context,
                    new[] { target },
                    new ReactionResolutionOptions
                    {
                        ActionLabel = $"attacked:{action?.Id ?? "unknown"}:self",
                        AllowPromptDeferral = false
                    });
                eligibleReactors.AddRange(selfResolution.EligibleReactors);
            }
            else
            {
                eligibleReactors.AddRange(Reactions.GetEligibleReactors(context, new[] { target }));
            }

            // Also check allies for reactions that trigger when an ally is attacked
            var allies = GetCombatants()
                .Where(c => c.Id != target.Id && c.Faction == target.Faction);
            var allyList = allies.ToList();
            if (allyList.Count > 0)
            {
                if (ReactionResolver != null)
                {
                    var allyResolution = ReactionResolver.ResolveTrigger(
                        context,
                        allyList,
                        new ReactionResolutionOptions
                        {
                            ActionLabel = $"attacked:{action?.Id ?? "unknown"}:ally",
                            AllowPromptDeferral = false
                        });
                    eligibleReactors.AddRange(allyResolution.EligibleReactors);
                }
                else
                {
                    eligibleReactors.AddRange(Reactions.GetEligibleReactors(context, allyList));
                }
            }

            // Read AC/roll modifiers from context data if reactions populated them
            if (context.Data.TryGetValue("acModifier", out var acObj) && acObj is int acVal)
                acModifier = acVal;
            if (context.Data.TryGetValue("rollModifier", out var rollObj) && rollObj is int rollVal)
                rollModifier = rollVal;

            var args = new ReactionTriggerEventArgs
            {
                Context = context,
                EligibleReactors = eligibleReactors,
                Cancel = false,
                ACModifier = acModifier,
                RollModifier = rollModifier
            };

            if (eligibleReactors.Count > 0)
            {
                OnAttackTrigger?.Invoke(args);
            }

            return args;
        }

        /// <summary>
        /// Fires YouAreHit reactions after an attack hits but before damage is calculated.
        /// For reactions like Hellish Rebuke (counter-damage), Uncanny Dodge (halve damage),
        /// and Deflect Missiles (reduce ranged damage).
        /// Returns the trigger args with DamageModifier.
        /// </summary>
        public ReactionTriggerEventArgs TryTriggerHitReactions(
            Combatant attacker,
            Combatant target,
            int damageAmount,
            string damageType,
            string attackType = null,
            bool isCritical = false,
            string actionId = null)
        {
            if (Reactions == null || GetCombatants == null)
                return null;

            var context = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.YouAreHit,
                TriggerSourceId = attacker.Id,
                AffectedId = target.Id,
                ActionId = actionId,
                Value = damageAmount,
                Position = target.Position,
                IsCancellable = false,
                Data = new Dictionary<string, object>
                {
                    { "attackType", attackType ?? "unknown" },
                    { "isCritical", isCritical },
                    { "damageAmount", damageAmount },
                    { "damageType", damageType ?? "untyped" },
                    { "actionId", actionId ?? "unknown" }
                }
            };

            var eligibleReactors = new List<(string CombatantId, ReactionDefinition Reaction)>();
            float damageModifier = 1.0f;

            // Check target for YouAreHit reactions (e.g., Uncanny Dodge, Deflect Missiles)
            if (ReactionResolver != null)
            {
                var selfResolution = ReactionResolver.ResolveTrigger(
                    context,
                    new[] { target },
                    new ReactionResolutionOptions
                    {
                        ActionLabel = $"hit:{actionId ?? "unknown"}:self",
                        AllowPromptDeferral = false
                    });
                eligibleReactors.AddRange(selfResolution.EligibleReactors);
                damageModifier *= selfResolution.DamageModifier;
            }
            else
            {
                eligibleReactors.AddRange(Reactions.GetEligibleReactors(context, new[] { target }));
            }

            var args = new ReactionTriggerEventArgs
            {
                Context = context,
                EligibleReactors = eligibleReactors,
                Cancel = false,
                DamageModifier = damageModifier
            };

            if (eligibleReactors.Count > 0)
            {
                OnHitTrigger?.Invoke(args);
            }

            return args;
        }
    }
}