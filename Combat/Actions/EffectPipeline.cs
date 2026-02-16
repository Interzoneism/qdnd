using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Reactions;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Actions
{
    /// <summary>
    /// Event args for reaction trigger events.
    /// </summary>
    public class ReactionTriggerEventArgs : EventArgs
    {
        /// <summary>
        /// The trigger context with all details.
        /// </summary>
        public ReactionTriggerContext Context { get; set; }

        /// <summary>
        /// List of eligible reactors (combatantId, reaction).
        /// </summary>
        public List<(string CombatantId, ReactionDefinition Reaction)> EligibleReactors { get; set; } = new();

        /// <summary>
        /// Set to true to cancel the triggering action (if cancellable).
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// Optional damage modifier (e.g., for shield reactions).
        /// </summary>
        public float DamageModifier { get; set; } = 1.0f;
    }
    /// <summary>
    /// Result of executing an action.
    /// </summary>
    public class ActionExecutionResult
    {
        public bool Success { get; set; }
        public string ActionId { get; set; }
        public string SourceId { get; set; }
        public List<string> TargetIds { get; set; } = new();
        public List<EffectResult> EffectResults { get; set; } = new();
        public QueryResult AttackResult { get; set; }
        public QueryResult SaveResult { get; set; }
        public string ErrorMessage { get; set; }
        public long ExecutedAt { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public static ActionExecutionResult Failure(string actionId, string sourceId, string error)
        {
            return new ActionExecutionResult
            {
                Success = false,
                ActionId = actionId,
                SourceId = sourceId,
                ErrorMessage = error
            };
        }
    }

    /// <summary>
    /// Manages ability execution and effect resolution.
    /// </summary>
    public class EffectPipeline
    {
        private readonly Dictionary<string, Effect> _effectHandlers = new();
        private readonly Dictionary<string, ActionDefinition> _actions = new();
        private readonly Dictionary<string, ActionCooldownState> _cooldowns = new();

        public RulesEngine Rules { get; set; }
        public StatusManager Statuses { get; set; }
        public Random Rng { get; set; }

        /// <summary>
        /// Optional centralized action registry for spell and ability lookups.
        /// If set, GetAction() will fallback to registry when action not found locally.
        /// </summary>
        public ActionRegistry ActionRegistry { get; set; }

        /// <summary>
        /// Optional combat context for service location.
        /// </summary>
        public QDND.Combat.Services.ICombatContext CombatContext { get; set; }

        /// <summary>
        /// Optional height service for attack modifiers from elevation.
        /// </summary>
        public HeightService Heights { get; set; }

        /// <summary>
        /// Optional LOS service for cover AC bonuses.
        /// </summary>
        public LOSService LOS { get; set; }

        /// <summary>
        /// Optional reaction system for triggering reactions on damage/ability cast.
        /// </summary>
        public ReactionSystem Reactions { get; set; }

        /// <summary>
        /// Optional centralized resolver that can immediately execute interrupts/reactions.
        /// </summary>
        public IReactionResolver ReactionResolver { get; set; }

        /// <summary>
        /// Optional concentration system for tracking concentration effects.
        /// </summary>
        public ConcentrationSystem Concentration { get; set; }

        /// <summary>
        /// Optional surface manager for effects that create or rely on surfaces.
        /// </summary>
        public SurfaceManager Surfaces { get; set; }

        /// <summary>
        /// Optional forced movement service for push/pull/teleport with collision detection and fall damage.
        /// </summary>
        public QDND.Combat.Movement.ForcedMovementService ForcedMovement { get; set; }

        /// <summary>
        /// Optional on-hit trigger service for Divine Smite, Hex, GWM bonus attacks, etc.
        /// </summary>
        public QDND.Combat.Services.OnHitTriggerService OnHitTriggerService { get; set; }

        /// <summary>
        /// All combatants in combat (for reaction eligibility checking).
        /// </summary>
        public Func<IEnumerable<Combatant>> GetCombatants { get; set; }

        public event Action<ActionExecutionResult> OnAbilityExecuted;

        /// <summary>
        /// Fired when an effect type has no registered handler.
        /// Args: effectType, abilityId
        /// </summary>
        public event Action<string, string> OnEffectUnhandled;

        /// <summary>
        /// Fired before damage is dealt - allows reaction checks for shields/damage reduction.
        /// </summary>
        public event EventHandler<ReactionTriggerEventArgs> OnDamageTrigger;

        /// <summary>
        /// Fired when an ability is cast - allows reaction checks for counterspell-type reactions.
        /// </summary>
        public event EventHandler<ReactionTriggerEventArgs> OnAbilityCastTrigger;

        public EffectPipeline()
        {
            // Register default effect handlers
            RegisterEffect(new DealDamageEffect());
            RegisterEffect(new HealEffect());
            RegisterEffect(new ReviveEffect());
            RegisterEffect(new ApplyStatusEffect());
            RegisterEffect(new RemoveStatusEffect());
            RegisterEffect(new ModifyResourceEffect());
            RegisterEffect(new SleepPoolEffect());

            // Movement and surface effect stubs (full implementation in Phase C)
            RegisterEffect(new TeleportEffect());
            RegisterEffect(new ForcedMoveEffect());
            RegisterEffect(new PullEffect());
            RegisterEffect(new SpawnSurfaceEffect());

            // Summon effect
            RegisterEffect(new SummonCombatantEffect());

            // Spawn object effect
            RegisterEffect(new SpawnObjectEffect());

            // Interrupt/counter effects
            RegisterEffect(new InterruptEffect());
            RegisterEffect(new CounterEffect());

            // Grant action effect
            RegisterEffect(new GrantActionEffect());

            // Wild Shape transformation effects
            RegisterEffect(new TransformEffect());
            RegisterEffect(new RevertTransformEffect());

            // Phase 2: BG3 functor effects
            RegisterEffect(new BreakConcentrationEffect());
            RegisterEffect(new RestoreResourceEffect());
            RegisterEffect(new GainTempHPEffect());
            RegisterEffect(new CreateExplosionEffect());

            // Phase 2 parity handlers (parsed functors with placeholder runtime behavior)
            RegisterEffect(new NoOpFunctorEffect("spawn_extra_projectiles"));
            RegisterEffect(new NoOpFunctorEffect("douse"));
            RegisterEffect(new NoOpFunctorEffect("spawn_inventory_item"));
            RegisterEffect(new NoOpFunctorEffect("fire_projectile"));
            RegisterEffect(new NoOpFunctorEffect("equalize"));
            RegisterEffect(new NoOpFunctorEffect("set_status_duration"));
            RegisterEffect(new NoOpFunctorEffect("pickup_entity"));
            RegisterEffect(new NoOpFunctorEffect("swap_places"));
            RegisterEffect(new NoOpFunctorEffect("grant"));
            RegisterEffect(new NoOpFunctorEffect("use_spell"));
        }

        /// <summary>
        /// Register an effect handler.
        /// </summary>
        public void RegisterEffect(Effect effect)
        {
            _effectHandlers[effect.Type] = effect;
        }

        /// <summary>
        /// Get the currently registered effect handler types.
        /// </summary>
        public IReadOnlyCollection<string> GetRegisteredEffectTypes()
        {
            return _effectHandlers.Keys.ToList();
        }

        /// <summary>
        /// Register an ability definition.
        /// Registers to both local dictionary and ActionRegistry if available.
        /// </summary>
        public void RegisterAction(ActionDefinition action)
        {
            _actions[action.Id] = action;
            
            // Also register to centralized registry if available
            ActionRegistry?.RegisterAction(action, overwrite: true);
        }

        /// <summary>
        /// Get an ability definition.
        /// First checks local dictionary, then falls back to ActionRegistry if available.
        /// </summary>
        public ActionDefinition GetAction(string actionId)
        {
            // Check local cache first
            if (_actions.TryGetValue(actionId, out var action))
                return action;
            
            // Fallback to centralized registry
            return ActionRegistry?.GetAction(actionId);
        }

        /// <summary>
        /// Check if an ability can be used.
        /// </summary>
        public (bool CanUse, string Reason) CanUseAbility(string actionId, Combatant source)
        {
            if (!_actions.TryGetValue(actionId, out var action))
            {
                action = ActionRegistry?.GetAction(actionId);
                if (action == null)
                    return (false, "Unknown action");
            }

            // Check if this is a test actor with the matching test action - bypass all resource checks
            var testTag = source.Tags?.FirstOrDefault(t => t.StartsWith("action_test_actor:", StringComparison.OrdinalIgnoreCase));
            if (testTag != null)
            {
                var parts = testTag.Split(':');
                if (parts.Length > 1)
                {
                    string testActionId = parts[1];
                    if (string.Equals(actionId, testActionId, StringComparison.OrdinalIgnoreCase))
                    {
                        // Bypass all resource and budget checks for the test action
                        return (true, "");
                    }
                }
            }

            // Check cooldown
            var cooldownKey = $"{source.Id}:{actionId}";
            if (_cooldowns.TryGetValue(cooldownKey, out var cooldown))
            {
                if (cooldown.CurrentCharges <= 0)
                    return (false, $"On cooldown ({cooldown.RemainingCooldown} turns)");
            }

            // Check requirements
            foreach (var req in action.Requirements)
            {
                bool met = CheckRequirement(req, source);
                if (req.Inverted ? met : !met)
                    return (false, $"Requirement not met: {req.Type}");
            }

            // Check if source is alive
            if (!source.IsActive)
                return (false, "Source is incapacitated");

            // Check for Silence blocking verbal spells
            if (Statuses?.HasStatus(source.Id, "silenced") == true &&
                action.Components.HasFlag(SpellComponents.Verbal))
            {
                return (false, "Cannot cast: Silenced (spell requires verbal component)");
            }

            // Check status-based action blocks
            var blockedReason = GetBlockedByStatusReason(source, actionId, action.Cost);
            if (blockedReason != null)
                return (false, blockedReason);

            // Check action economy budget
            if (source.ActionBudget != null)
            {
                var (canPay, budgetReason) = source.ActionBudget.CanPayCost(action.Cost);
                if (!canPay)
                    return (false, budgetReason);
            }

            // Check BG3 ActionResources first for resource costs
            var (bg3CanPay, bg3Reason) = ValidateBG3ResourceCost(source, action);
            if (!bg3CanPay)
                return (false, bg3Reason);

            // Legacy fallback: check resources not tracked by BG3 ActionResources
            var legacyCosts = GetLegacyFallbackCosts(source, action.Cost);
            if (legacyCosts.Count > 0 && source.ResourcePool != null &&
                !source.ResourcePool.CanPay(legacyCosts, out var resourceReason))
            {
                return (false, resourceReason);
            }

            return (true, null);
        }

        /// <summary>
        /// Execute an action.
        /// </summary>
        public ActionExecutionResult ExecuteAction(
            string actionId,
            Combatant source,
            List<Combatant> targets)
        {
            return ExecuteAction(actionId, source, targets, ActionExecutionOptions.Default);
        }

        /// <summary>
        /// Execute an ability with variant and upcast options.
        /// </summary>
        public ActionExecutionResult ExecuteAction(
            string actionId,
            Combatant source,
            List<Combatant> targets,
            ActionExecutionOptions options)
        {
            options ??= ActionExecutionOptions.Default;

            if (!_actions.TryGetValue(actionId, out var action))
            {
                // Fall back to centralized ActionRegistry (BG3 spells)
                action = ActionRegistry?.GetAction(actionId);
                if (action == null)
                    return ActionExecutionResult.Failure(actionId, source.Id, "Unknown action");
            }

            // Validate variant if specified
            ActionVariant variant = null;
            if (!string.IsNullOrEmpty(options.VariantId))
            {
                variant = action.Variants.Find(v => v.VariantId == options.VariantId);
                if (variant == null)
                    return ActionExecutionResult.Failure(actionId, source.Id, $"Unknown variant: {options.VariantId}");
            }

            // Validate upcast level
            if (options.UpcastLevel > 0 && !action.CanUpcast)
                return ActionExecutionResult.Failure(actionId, source.Id, "Ability does not support upcasting");

            if (options.UpcastLevel > 0 && action.UpcastScaling != null &&
                action.UpcastScaling.MaxUpcastLevel > 0 &&
                options.UpcastLevel > action.UpcastScaling.MaxUpcastLevel)
            {
                return ActionExecutionResult.Failure(actionId, source.Id,
                    $"Upcast level {options.UpcastLevel} exceeds maximum {action.UpcastScaling.MaxUpcastLevel}");
            }

            // Build effective cost (base + variant + upcast)
            var effectiveCost = BuildEffectiveCost(action, variant, options.UpcastLevel);

            // Validate and consume costs unless skipped (for Extra Attack)
            if (!options.SkipCostValidation)
            {
                var (canUse, reason) = CanUseAbilityWithCost(actionId, source, effectiveCost);
                if (!canUse)
                    return ActionExecutionResult.Failure(actionId, source.Id, reason);

                // Extra Attack handling: weapon attacks consume from attack pool
                bool isWeaponAttack = action.AttackType == AttackType.MeleeWeapon ||
                                    action.AttackType == AttackType.RangedWeapon;

                if (isWeaponAttack && effectiveCost.UsesAction)
                {
                    // Weapon attacks use the attack pool (Extra Attack)
                    if (source.ActionBudget != null)
                    {
                        if (source.ActionBudget.AttacksRemaining <= 0)
                        {
                            return ActionExecutionResult.Failure(actionId, source.Id, "No attacks remaining");
                        }
                        
                        // Consume one attack from the pool
                        source.ActionBudget.ConsumeAttack();
                    }
                    
                    // Consume bonus action, reaction, movement normally
                    if (effectiveCost.UsesBonusAction && source.ActionBudget != null)
                        source.ActionBudget.ConsumeBonusAction();
                    if (effectiveCost.UsesReaction && source.ActionBudget != null)
                        source.ActionBudget.ConsumeReaction();
                    if (effectiveCost.MovementCost > 0 && source.ActionBudget != null)
                        source.ActionBudget.ConsumeMovement(effectiveCost.MovementCost);
                }
                else
                {
                    // Non-weapon actions (spells, etc.) consume action economy normally
                    // and reset the attack pool
                    if (source.ActionBudget != null && effectiveCost.UsesAction)
                    {
                        source.ActionBudget.ResetAttacks();
                    }
                    source.ActionBudget?.ConsumeCost(effectiveCost);
                }

                // Consume BG3 ActionResources first
                var (bg3Success, bg3ConsumeReason) = ConsumeBG3ResourceCost(source, action, effectiveCost);
                if (!bg3Success)
                    return ActionExecutionResult.Failure(actionId, source.Id, bg3ConsumeReason);

                // Legacy fallback: consume resources not tracked by BG3 ActionResources
                var legacyConsumeCosts = GetLegacyFallbackCosts(source, effectiveCost);
                if (legacyConsumeCosts.Count > 0 && source.ResourcePool != null &&
                    !source.ResourcePool.Consume(legacyConsumeCosts, out var resourceConsumeReason))
                {
                    return ActionExecutionResult.Failure(actionId, source.Id, resourceConsumeReason);
                }
            }

            // Build effective effects list
            var effectiveEffects = BuildEffectiveEffects(action.Effects, variant, options.UpcastLevel, action.UpcastScaling);

            // Build effective tags
            var effectiveTags = BuildEffectiveTags(action.Tags, variant);

            // Create context
            var context = new EffectContext
            {
                Source = source,
                Targets = targets,
                TargetPosition = options.TargetPosition,
                Ability = action,
                Rules = Rules,
                Statuses = Statuses,
                Surfaces = Surfaces,
                Heights = Heights,
                ForcedMovement = ForcedMovement,
                Rng = Rng ?? new Random(),
                CombatContext = CombatContext,
                OnHitTriggerService = OnHitTriggerService,
                TriggerContext = options.TriggerContext,
                OnBeforeDamage = (src, tgt, dmg, dmgType) =>
                {
                    var triggerArgs = TryTriggerDamageReactions(src, tgt, dmg, dmgType, action.Id);
                    return triggerArgs?.DamageModifier ?? 1.0f;
                }
            };

            // Dispatch ability declared event
            Rules?.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.AbilityDeclared,
                SourceId = source.Id,
                ActionId = actionId,
                Data = new Dictionary<string, object>
                {
                    { "targetCount", targets.Count },
                    { "variantId", options.VariantId ?? "" },
                    { "upcastLevel", options.UpcastLevel }
                }
            });

            // Canonical action-declare rule window for passives/interrupts.
            var actionDeclareContext = new RuleEventContext
            {
                Source = source,
                Ability = action,
                Random = context.Rng
            };
            foreach (var tag in effectiveTags)
            {
                actionDeclareContext.Tags.Add(tag);
            }
            Rules?.RuleWindows.Dispatch(RuleWindow.OnDeclareAction, actionDeclareContext);
            if (actionDeclareContext.Cancel)
            {
                return ActionExecutionResult.Failure(actionId, source.Id, "Action was cancelled by a passive rule");
            }

            // Check for SpellCastNearby reactions (counterspell, etc.)
            var spellCastTrigger = TryTriggerAbilityCastReactionsWithTags(source, action, targets, effectiveTags);
            if (spellCastTrigger?.Cancel == true && spellCastTrigger.Context.IsCancellable)
            {
                return ActionExecutionResult.Failure(actionId, source.Id, "Ability was countered by a reaction");
            }

            var result = new ActionExecutionResult
            {
                Success = true,
                ActionId = actionId,
                SourceId = source.Id,
                TargetIds = targets.Select(t => t.Id).ToList()
            };

            // Roll attack if needed
            if (action.AttackType.HasValue && targets.Count > 0)
            {
                var primaryTarget = targets[0];
                bool isSpellAttack = action.AttackType == AttackType.MeleeSpell ||
                                     action.AttackType == AttackType.RangedSpell ||
                                     effectiveTags.Contains("spell");
                bool isMeleeAttack = action.AttackType == AttackType.MeleeWeapon ||
                                     action.AttackType == AttackType.MeleeSpell;
                bool isRangedAttack = action.AttackType == AttackType.RangedWeapon ||
                                      action.AttackType == AttackType.RangedSpell;

                if (isRangedAttack && Statuses?.HasStatus(source.Id, "blinded") == true)
                {
                    float distance = source.Position.DistanceTo(primaryTarget.Position);
                    if (distance > 3f)
                    {
                        return ActionExecutionResult.Failure(actionId, source.Id, "Blinded limits ranged attacks to 3m");
                    }
                }

                int heightMod = 0;
                if (Heights != null)
                {
                    heightMod = Heights.GetAttackModifier(source, primaryTarget);
                }

                int coverACBonus = 0;
                if (LOS != null)
                {
                    var losResult = LOS.CheckLOS(source, primaryTarget);
                    coverACBonus = losResult.GetACBonus();
                }

                var attackQuery = new QueryInput
                {
                    Type = QueryType.AttackRoll,
                    Source = source,
                    Target = primaryTarget,
                    BaseValue = GetAttackRollBonus(source, action, effectiveTags) + heightMod
                };
                var attackTags = new HashSet<string>(effectiveTags);
                if (isMeleeAttack) attackTags.Add("melee_attack");
                if (isRangedAttack) attackTags.Add("ranged_attack");
                if (isSpellAttack) attackTags.Add("spell_attack");
                attackTags.ToList().ForEach(t => attackQuery.Tags.Add(t));

                var statusAttackContext = GetStatusAttackContext(source, primaryTarget, action);
                if (statusAttackContext.AdvantageSources.Count > 0)
                {
                    attackQuery.Parameters["statusAdvantageSources"] = statusAttackContext.AdvantageSources;
                }

                if (statusAttackContext.DisadvantageSources.Count > 0)
                {
                    attackQuery.Parameters["statusDisadvantageSources"] = statusAttackContext.DisadvantageSources;
                }

                if (statusAttackContext.AutoCritOnHit)
                {
                    attackQuery.Parameters["autoCritOnHit"] = true;
                }

                attackQuery.Parameters["criticalThreshold"] = GetCriticalThreshold(source, isSpellAttack);

                if (coverACBonus != 0)
                {
                    attackQuery.Parameters["coverACBonus"] = coverACBonus;
                }

                if (heightMod != 0)
                {
                    attackQuery.Parameters["heightModifier"] = heightMod;
                }

                var beforeAttackContext = new RuleEventContext
                {
                    Source = source,
                    Target = primaryTarget,
                    Ability = action,
                    QueryInput = attackQuery,
                    Random = context.Rng,
                    IsMeleeWeaponAttack = action.AttackType == AttackType.MeleeWeapon,
                    IsRangedWeaponAttack = action.AttackType == AttackType.RangedWeapon,
                    IsSpellAttack = isSpellAttack
                };
                foreach (var tag in attackTags)
                {
                    beforeAttackContext.Tags.Add(tag);
                }
                Rules?.RuleWindows.Dispatch(RuleWindow.BeforeAttackRoll, beforeAttackContext);
                if (beforeAttackContext.Cancel)
                {
                    return ActionExecutionResult.Failure(actionId, source.Id, "Attack was cancelled by a passive rule");
                }

                ApplyWindowRollSources(attackQuery, beforeAttackContext);

                context.AttackResult = Rules.RollAttack(attackQuery);
                result.AttackResult = context.AttackResult;

                Rules?.RuleWindows.Dispatch(RuleWindow.AfterAttackRoll, new RuleEventContext
                {
                    Source = source,
                    Target = primaryTarget,
                    Ability = action,
                    QueryInput = attackQuery,
                    QueryResult = context.AttackResult,
                    Random = context.Rng,
                    IsMeleeWeaponAttack = action.AttackType == AttackType.MeleeWeapon,
                    IsRangedWeaponAttack = action.AttackType == AttackType.RangedWeapon,
                    IsSpellAttack = isSpellAttack,
                    IsCriticalHit = context.AttackResult?.IsCritical == true
                });

                // Remove statuses with RemoveOnAttack (e.g., hidden)
                if (Statuses != null)
                {
                    Statuses.RemoveStatusesOnAttack(source.Id);
                }
            }

            // Roll save if needed
            if (!string.IsNullOrEmpty(action.SaveType) && targets.Count > 0)
            {
                int saveDC = action.SaveDC ?? ComputeSaveDC(source, action, effectiveTags);
                foreach (var target in targets)
                {
                    if (ShouldAutoFailSave(target, action.SaveType))
                    {
                        context.SaveResult = new QueryResult
                        {
                            Input = new QueryInput
                            {
                                Type = QueryType.SavingThrow,
                                Source = source,
                                Target = target,
                                DC = saveDC,
                                BaseValue = GetSavingThrowBonus(target, action.SaveType)
                            },
                            BaseValue = 0,
                            NaturalRoll = 1,
                            FinalValue = 1,
                            IsSuccess = false,
                            IsCriticalFailure = true
                        };
                        result.SaveResult = context.SaveResult;
                        
                        // Store per-target save result for auto-fail case
                        context.PerTargetSaveResults[target.Id] = context.SaveResult;
                        continue;
                    }

                    var saveQuery = new QueryInput
                    {
                        Type = QueryType.SavingThrow,
                        Source = source,
                        Target = target,
                        DC = saveDC,
                        BaseValue = GetSavingThrowBonus(target, action.SaveType)
                    };
                    saveQuery.Tags.Add($"save:{action.SaveType}");

                    var beforeSaveContext = new RuleEventContext
                    {
                        Source = source,
                        Target = target,
                        Ability = action,
                        QueryInput = saveQuery,
                        Random = context.Rng
                    };
                    foreach (var tag in saveQuery.Tags)
                    {
                        beforeSaveContext.Tags.Add(tag);
                    }
                    Rules?.RuleWindows.Dispatch(RuleWindow.BeforeSavingThrow, beforeSaveContext);
                    if (beforeSaveContext.Cancel)
                    {
                        context.SaveResult = new QueryResult
                        {
                            Input = saveQuery,
                            BaseValue = saveQuery.BaseValue,
                            FinalValue = saveQuery.BaseValue,
                            IsSuccess = false
                        };
                        result.SaveResult = context.SaveResult;
                        context.PerTargetSaveResults[target.Id] = context.SaveResult;
                        continue;
                    }

                    saveQuery.BaseValue += beforeSaveContext.TotalSaveBonus;
                    ApplyWindowRollSources(saveQuery, beforeSaveContext);

                    context.SaveResult = Rules.RollSave(saveQuery);
                    result.SaveResult = context.SaveResult;
                    
                    // Store per-target save result
                    context.PerTargetSaveResults[target.Id] = context.SaveResult;

                    Rules?.RuleWindows.Dispatch(RuleWindow.AfterSavingThrow, new RuleEventContext
                    {
                        Source = source,
                        Target = target,
                        Ability = action,
                        QueryInput = saveQuery,
                        QueryResult = context.SaveResult,
                        Random = context.Rng
                    });
                }
            }

            // Execute effects
            foreach (var effectDef in effectiveEffects)
            {
                if (!_effectHandlers.TryGetValue(effectDef.Type, out var handler))
                {
                    Godot.GD.PushWarning($"Unknown effect type: {effectDef.Type}");
                    OnEffectUnhandled?.Invoke(effectDef.Type, action.Id);
                    continue;
                }

                var effectResults = handler.Execute(effectDef, context);
                result.EffectResults.AddRange(effectResults);
            }

            // Handle concentration abilities
            if (action.RequiresConcentration && Concentration != null)
            {
                string concentrationStatusId = action.ConcentrationStatusId;
                string concentrationTargetId = targets.Count > 0 ? targets[0].Id : source.Id;

                if (string.IsNullOrEmpty(concentrationStatusId))
                {
                    var applyStatusEffect = effectiveEffects.FirstOrDefault(e => e.Type == "apply_status");
                    if (applyStatusEffect != null)
                    {
                        concentrationStatusId = applyStatusEffect.StatusId;
                    }
                }

                Concentration.StartConcentration(new ConcentrationInfo
                {
                    CombatantId = source.Id,
                    ActionId = actionId,
                    StatusId = concentrationStatusId,
                    TargetId = concentrationTargetId
                });
            }

            // Consume cooldown/charges
            ConsumeCooldown(source.Id, actionId, action);

            // Dispatch ability resolved event
            Rules?.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.AbilityResolved,
                SourceId = source.Id,
                ActionId = actionId,
                Data = new Dictionary<string, object>
                {
                    { "success", result.Success },
                    { "effectCount", result.EffectResults.Count },
                    { "variantId", options.VariantId ?? "" },
                    { "upcastLevel", options.UpcastLevel }
                }
            });

            Rules?.RuleWindows.Dispatch(RuleWindow.OnActionComplete, new RuleEventContext
            {
                Source = source,
                Ability = action,
                QueryResult = result.AttackResult ?? result.SaveResult,
                Random = context.Rng
            });

            OnAbilityExecuted?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Build the effective cost including base, variant, and upcast costs.
        /// </summary>
        private ActionCost BuildEffectiveCost(ActionDefinition action, ActionVariant variant, int upcastLevel)
        {
            var effectiveCost = new ActionCost
            {
                UsesAction = action.Cost?.UsesAction == true,
                UsesBonusAction = action.Cost?.UsesBonusAction == true,
                UsesReaction = action.Cost?.UsesReaction == true,
                MovementCost = action.Cost?.MovementCost ?? 0,
                ResourceCosts = action.Cost?.ResourceCosts != null
                    ? new Dictionary<string, int>(action.Cost.ResourceCosts)
                    : new Dictionary<string, int>()
            };

            // Apply action type override from variant (e.g., Quickened Spell metamagic)
            if (variant?.ActionTypeOverride != null)
            {
                // Reset all action types first
                effectiveCost.UsesAction = false;
                effectiveCost.UsesBonusAction = false;
                effectiveCost.UsesReaction = false;

                // Set the overridden action type
                switch (variant.ActionTypeOverride.ToLowerInvariant())
                {
                    case "action":
                        effectiveCost.UsesAction = true;
                        break;
                    case "bonus":
                    case "bonus_action":
                        effectiveCost.UsesBonusAction = true;
                        break;
                    case "reaction":
                        effectiveCost.UsesReaction = true;
                        break;
                }
            }

            // Add variant costs
            if (variant?.AdditionalCost != null)
            {
                if (variant.AdditionalCost.UsesAction) effectiveCost.UsesAction = true;
                if (variant.AdditionalCost.UsesBonusAction) effectiveCost.UsesBonusAction = true;
                if (variant.AdditionalCost.UsesReaction) effectiveCost.UsesReaction = true;
                effectiveCost.MovementCost += variant.AdditionalCost.MovementCost;

                foreach (var (key, value) in variant.AdditionalCost.ResourceCosts)
                {
                    if (effectiveCost.ResourceCosts.ContainsKey(key))
                        effectiveCost.ResourceCosts[key] += value;
                    else
                        effectiveCost.ResourceCosts[key] = value;
                }
            }

            // Handle upcast costs
            if (upcastLevel > 0 && action.UpcastScaling != null)
            {
                // D&D 5e spell slot model: when upcasting, replace the base spell slot
                // with a higher-level slot (e.g., spell_slot_1 → spell_slot_2 for +1 upcast)
                var slotKeys = effectiveCost.ResourceCosts.Keys
                    .Where(k => k.StartsWith("spell_slot_"))
                    .ToList();

                if (slotKeys.Count > 0)
                {
                    // Find the base spell slot level from resource costs
                    foreach (var slotKey in slotKeys)
                    {
                        string levelStr = slotKey.Replace("spell_slot_", "");
                        if (int.TryParse(levelStr, out int baseLevel))
                        {
                            int amount = effectiveCost.ResourceCosts[slotKey];
                            int newLevel = baseLevel + upcastLevel;

                            // Remove the original slot cost
                            effectiveCost.ResourceCosts.Remove(slotKey);

                            // Add the higher-level slot cost
                            string newKey = $"spell_slot_{newLevel}";
                            effectiveCost.ResourceCosts[newKey] = amount;
                        }
                    }
                }
                else
                {
                    // Fallback: use the generic resource key model
                    string resourceKey = action.UpcastScaling.ResourceKey;
                    int additionalCost = upcastLevel * action.UpcastScaling.CostPerLevel;

                    if (effectiveCost.ResourceCosts.ContainsKey(resourceKey))
                        effectiveCost.ResourceCosts[resourceKey] += additionalCost;
                    else
                        effectiveCost.ResourceCosts[resourceKey] = action.UpcastScaling.BaseCost + additionalCost;
                }
            }

            return effectiveCost;
        }

        /// <summary>
        /// Build the effective effects list with variant and upcast modifications.
        /// </summary>
        private List<EffectDefinition> BuildEffectiveEffects(
            List<EffectDefinition> baseEffects,
            ActionVariant variant,
            int upcastLevel,
            UpcastScaling upcastScaling)
        {
            var effectiveEffects = new List<EffectDefinition>();

            foreach (var baseEffect in baseEffects)
            {
                // Clone the effect
                var effect = CloneEffectDefinition(baseEffect);

                // Apply variant modifications
                if (variant != null)
                {
                    ApplyVariantToEffect(effect, variant);
                }

                // Apply upcast modifications
                if (upcastLevel > 0 && upcastScaling != null)
                {
                    ApplyUpcastToEffect(effect, upcastLevel, upcastScaling);
                }

                effectiveEffects.Add(effect);
            }

            // Add variant additional effects
            if (variant?.AdditionalEffects != null)
            {
                foreach (var additionalEffect in variant.AdditionalEffects)
                {
                    var cloned = CloneEffectDefinition(additionalEffect);

                    // Apply upcast to additional effects too
                    if (upcastLevel > 0 && upcastScaling != null)
                    {
                        ApplyUpcastToEffect(cloned, upcastLevel, upcastScaling);
                    }

                    effectiveEffects.Add(cloned);
                }
            }

            return effectiveEffects;
        }

        /// <summary>
        /// Apply variant modifications to an effect.
        /// </summary>
        private void ApplyVariantToEffect(EffectDefinition effect, ActionVariant variant)
        {
            // Replace damage type
            if (!string.IsNullOrEmpty(variant.ReplaceDamageType) && !string.IsNullOrEmpty(effect.DamageType))
            {
                effect.DamageType = variant.ReplaceDamageType;
            }

            // Add flat damage
            if (variant.AdditionalDamage != 0 && effect.Type == "damage")
            {
                effect.Value += variant.AdditionalDamage;
            }

            // Add additional dice
            if (!string.IsNullOrEmpty(variant.AdditionalDice) && effect.Type == "damage")
            {
                effect.DiceFormula = CombineDiceFormulas(effect.DiceFormula, variant.AdditionalDice);
            }

            // Replace status ID
            if (!string.IsNullOrEmpty(variant.ReplaceStatusId) && effect.Type == "apply_status")
            {
                effect.StatusId = variant.ReplaceStatusId;
            }
        }

        /// <summary>
        /// Apply upcast scaling to an effect.
        /// </summary>
        private void ApplyUpcastToEffect(EffectDefinition effect, int upcastLevel, UpcastScaling scaling)
        {
            if (effect.Type != "damage" && effect.Type != "heal" && effect.Type != "apply_status")
                return;

            // Calculate effective scaling steps based on perLevel
            int perLevel = scaling.PerLevel ?? 1;
            int scalingSteps = perLevel > 0 ? upcastLevel / perLevel : upcastLevel;

            if (scalingSteps <= 0)
                return;

            // Add flat damage per level
            if (scaling.DamagePerLevel != 0)
            {
                effect.Value += scaling.DamagePerLevel * scalingSteps;
            }

            // Add dice per level
            if (!string.IsNullOrEmpty(scaling.DicePerLevel))
            {
                for (int i = 0; i < scalingSteps; i++)
                {
                    effect.DiceFormula = CombineDiceFormulas(effect.DiceFormula, scaling.DicePerLevel);
                }
            }

            // Duration scaling for status effects
            if (effect.Type == "apply_status" && scaling.DurationPerLevel != 0)
            {
                effect.StatusDuration += scaling.DurationPerLevel * scalingSteps;
            }

            // Target scaling (for abilities like Invisibility)
            // This modifies the max targets, handled at ability level, not per-effect
            // But we track it here for completeness
        }

        /// <summary>
        /// Combine two dice formulas (e.g., "2d6+3" + "1d6" = "3d6+3").
        /// </summary>
        private string CombineDiceFormulas(string formula1, string formula2)
        {
            if (string.IsNullOrEmpty(formula1)) return formula2;
            if (string.IsNullOrEmpty(formula2)) return formula1;

            // Parse both formulas
            var (count1, sides1, bonus1) = ParseDiceFormula(formula1);
            var (count2, sides2, bonus2) = ParseDiceFormula(formula2);

            // If same die type, combine counts
            if (sides1 == sides2 && sides1 > 0)
            {
                int totalCount = count1 + count2;
                int totalBonus = bonus1 + bonus2;
                if (totalBonus > 0)
                    return $"{totalCount}d{sides1}+{totalBonus}";
                else if (totalBonus < 0)
                    return $"{totalCount}d{sides1}{totalBonus}";
                else
                    return $"{totalCount}d{sides1}";
            }

            // Different die types - just add bonus
            int combinedBonus = bonus1 + bonus2 + (count2 > 0 ? 0 : 0);
            if (sides2 > 0)
            {
                // Different die types, approximate by adding average
                int avgAdd = (int)Math.Round(count2 * (1 + sides2) / 2.0);
                combinedBonus += avgAdd;
            }
            else
            {
                combinedBonus += bonus2;
            }

            if (combinedBonus > bonus1)
            {
                if (bonus1 != 0)
                {
                    string baseFormula = formula1.Contains("+") ? formula1[..formula1.IndexOf('+')]
                        : formula1.Contains("-") ? formula1[..formula1.IndexOf('-')] : formula1;
                    return combinedBonus >= 0 ? $"{baseFormula}+{combinedBonus}" : $"{baseFormula}{combinedBonus}";
                }
            }

            // Fallback: return formula1 with added dice as bonus approximation
            return formula1;
        }

        /// <summary>
        /// Parse a dice formula into components.
        /// </summary>
        private (int count, int sides, int bonus) ParseDiceFormula(string formula)
        {
            if (string.IsNullOrEmpty(formula))
                return (0, 0, 0);

            formula = formula.ToLower().Replace(" ", "");

            int bonus = 0;
            int plusIdx = formula.IndexOf('+');
            int minusIdx = formula.LastIndexOf('-');
            if (minusIdx == 0) minusIdx = -1; // Ignore leading minus

            int bonusIdx = -1;
            if (plusIdx > 0) bonusIdx = plusIdx;
            else if (minusIdx > 0) bonusIdx = minusIdx;

            if (bonusIdx > 0)
            {
                if (int.TryParse(formula[bonusIdx..], out bonus))
                {
                    formula = formula[..bonusIdx];
                }
            }

            int dIdx = formula.IndexOf('d');
            if (dIdx < 0)
            {
                if (int.TryParse(formula, out int flat))
                    return (0, 0, flat + bonus);
                return (0, 0, bonus);
            }

            string countStr = dIdx == 0 ? "1" : formula[..dIdx];
            string sidesStr = formula[(dIdx + 1)..];

            int.TryParse(countStr, out int count);
            int.TryParse(sidesStr, out int sides);

            return (count, sides, bonus);
        }

        /// <summary>
        /// Clone an effect definition.
        /// </summary>
        private EffectDefinition CloneEffectDefinition(EffectDefinition original)
        {
            return new EffectDefinition
            {
                Type = original.Type,
                Value = original.Value,
                DiceFormula = original.DiceFormula,
                DamageType = original.DamageType,
                StatusId = original.StatusId,
                StatusDuration = original.StatusDuration,
                StatusStacks = original.StatusStacks,
                TargetType = original.TargetType,
                Condition = original.Condition,
                SaveTakesHalf = original.SaveTakesHalf,
                Scaling = new Dictionary<string, float>(original.Scaling),
                Parameters = new Dictionary<string, object>(original.Parameters)
            };
        }

        /// <summary>
        /// Build effective tags with variant modifications.
        /// </summary>
        private HashSet<string> BuildEffectiveTags(HashSet<string> baseTags, ActionVariant variant)
        {
            var effectiveTags = new HashSet<string>(baseTags);

            if (variant != null)
            {
                foreach (var tag in variant.AdditionalTags)
                    effectiveTags.Add(tag);

                foreach (var tag in variant.RemoveTags)
                    effectiveTags.Remove(tag);
            }

            return effectiveTags;
        }

        /// <summary>
        /// Check if an ability can be used with a specific cost.
        /// </summary>
        private (bool CanUse, string Reason) CanUseAbilityWithCost(string actionId, Combatant source, ActionCost cost)
        {
            if (!_actions.TryGetValue(actionId, out var action))
            {
                action = ActionRegistry?.GetAction(actionId);
                if (action == null)
                    return (false, "Unknown action");
            }

            // Check cooldown
            var cooldownKey = $"{source.Id}:{actionId}";
            if (_cooldowns.TryGetValue(cooldownKey, out var cooldown))
            {
                if (cooldown.CurrentCharges <= 0)
                    return (false, $"On cooldown ({cooldown.RemainingCooldown} turns)");
            }

            // Check requirements
            foreach (var req in action.Requirements)
            {
                bool met = CheckRequirement(req, source);
                if (req.Inverted ? met : !met)
                    return (false, $"Requirement not met: {req.Type}");
            }

            // Check if source is alive
            if (!source.IsActive)
                return (false, "Source is incapacitated");

            // Check status-based action blocks
            var blockedReason = GetBlockedByStatusReason(source, actionId, cost);
            if (blockedReason != null)
                return (false, blockedReason);

            // Check action economy budget with effective cost
            if (source.ActionBudget != null)
            {
                var (canPay, budgetReason) = source.ActionBudget.CanPayCost(cost);
                if (!canPay)
                    return (false, budgetReason);
            }

            // Check BG3 ActionResources first for resource costs
            var (bg3CanPay, bg3Reason) = ValidateBG3ResourceCost(source, action, cost);
            if (!bg3CanPay)
                return (false, bg3Reason);

            // Legacy fallback: check resources not tracked by BG3 ActionResources
            var legacyCosts = GetLegacyFallbackCosts(source, cost);
            if (legacyCosts.Count > 0 && source.ResourcePool != null &&
                !source.ResourcePool.CanPay(legacyCosts, out var resourceReason))
            {
                return (false, resourceReason);
            }

            return (true, null);
        }

        /// <summary>
        /// Validates whether the combatant's BG3 ActionResources can cover the resource costs of an action.
        /// Maps ActionCost fields to BG3 resource names and checks availability.
        /// Resources not tracked by ActionResources are skipped (the legacy pool handles them).
        /// </summary>
        /// <param name="combatant">The combatant whose resources to check.</param>
        /// <param name="action">The action definition (used for SpellLevel-based implicit spell slot costs).</param>
        /// <param name="cost">Optional cost override (for variants/upcasting). Uses action.Cost if null.</param>
        /// <returns>(canPay, failReason). Returns (true, null) when all BG3-tracked resources are sufficient.</returns>
        internal (bool CanPay, string Reason) ValidateBG3ResourceCost(
            Combatant combatant, ActionDefinition action, ActionCost cost = null)
        {
            cost ??= action?.Cost;
            if (cost == null) return (true, null);

            var pool = combatant.ActionResources;
            if (pool == null) return (true, null);

            // Map action economy fields → BG3 resource names
            if (cost.UsesAction && pool.HasResource("ActionPoint") && !pool.Has("ActionPoint", 1))
                return (false, "No ActionPoint available");
            if (cost.UsesBonusAction && pool.HasResource("BonusActionPoint") && !pool.Has("BonusActionPoint", 1))
                return (false, "No BonusActionPoint available");
            if (cost.UsesReaction && pool.HasResource("ReactionActionPoint") && !pool.Has("ReactionActionPoint", 1))
                return (false, "No ReactionActionPoint available");
            if (cost.MovementCost > 0 && pool.HasResource("Movement") && !pool.Has("Movement", cost.MovementCost))
                return (false, $"Insufficient Movement ({pool.GetCurrent("Movement")}/{cost.MovementCost})");

            // Map ResourceCosts dictionary → BG3 resources
            if (cost.ResourceCosts != null)
            {
                foreach (var (key, amount) in cost.ResourceCosts)
                {
                    if (amount <= 0) continue;

                    // spell_slot_N → SpellSlot at level N
                    if (key.StartsWith("spell_slot_", StringComparison.OrdinalIgnoreCase))
                    {
                        string levelStr = key.Substring("spell_slot_".Length);
                        if (int.TryParse(levelStr, out int level) && pool.HasResource("SpellSlot"))
                        {
                            if (!pool.Has("SpellSlot", amount, level))
                                return (false, $"Insufficient SpellSlot level {level} ({pool.GetCurrent("SpellSlot", level)}/{amount})");
                        }
                        continue;
                    }

                    // Direct resource name lookup
                    if (pool.HasResource(key) && !pool.Has(key, amount))
                        return (false, $"Insufficient {key} ({pool.GetCurrent(key)}/{amount})");
                }
            }

            // Implicit spell slot cost: spells with SpellLevel > 0 and no explicit spell_slot in ResourceCosts
            if (action?.SpellLevel > 0 && pool.HasResource("SpellSlot"))
            {
                bool hasExplicitSlot = cost.ResourceCosts?.Keys
                    .Any(k => k.StartsWith("spell_slot_", StringComparison.OrdinalIgnoreCase)) == true;
                if (!hasExplicitSlot && !pool.Has("SpellSlot", 1, action.SpellLevel))
                    return (false, $"Insufficient SpellSlot level {action.SpellLevel}");
            }

            return (true, null);
        }

        /// <summary>
        /// Consumes BG3 ActionResources for the resource costs of an action.
        /// Maps ActionCost fields to BG3 resource names and consumes them.
        /// Resources not tracked by ActionResources are skipped (the legacy pool handles them).
        /// Logs each resource consumption for debugging.
        /// </summary>
        /// <param name="combatant">The combatant whose resources to consume.</param>
        /// <param name="action">The action definition (used for SpellLevel-based implicit spell slot costs).</param>
        /// <param name="cost">Optional cost override (for variants/upcasting). Uses action.Cost if null.</param>
        /// <returns>(success, failReason).</returns>
        internal (bool Success, string Reason) ConsumeBG3ResourceCost(
            Combatant combatant, ActionDefinition action, ActionCost cost = null)
        {
            cost ??= action?.Cost;
            if (cost == null) return (true, null);

            var pool = combatant.ActionResources;
            if (pool == null) return (true, null);

            // Consume action economy from BG3 resources
            if (cost.UsesAction && pool.HasResource("ActionPoint"))
            {
                if (!pool.Consume("ActionPoint", 1))
                    return (false, "Failed to consume ActionPoint");
                Godot.GD.Print($"[BG3Resource] {combatant.Name} consumed 1 ActionPoint");
            }
            if (cost.UsesBonusAction && pool.HasResource("BonusActionPoint"))
            {
                if (!pool.Consume("BonusActionPoint", 1))
                    return (false, "Failed to consume BonusActionPoint");
                Godot.GD.Print($"[BG3Resource] {combatant.Name} consumed 1 BonusActionPoint");
            }
            if (cost.UsesReaction && pool.HasResource("ReactionActionPoint"))
            {
                if (!pool.Consume("ReactionActionPoint", 1))
                    return (false, "Failed to consume ReactionActionPoint");
                Godot.GD.Print($"[BG3Resource] {combatant.Name} consumed 1 ReactionActionPoint");
            }
            if (cost.MovementCost > 0 && pool.HasResource("Movement"))
            {
                if (!pool.Consume("Movement", cost.MovementCost))
                    return (false, $"Failed to consume {cost.MovementCost} Movement");
                Godot.GD.Print($"[BG3Resource] {combatant.Name} consumed {cost.MovementCost} Movement");
            }

            // Consume resource costs
            if (cost.ResourceCosts != null)
            {
                foreach (var (key, amount) in cost.ResourceCosts)
                {
                    if (amount <= 0) continue;

                    // spell_slot_N → SpellSlot at level N
                    if (key.StartsWith("spell_slot_", StringComparison.OrdinalIgnoreCase))
                    {
                        string levelStr = key.Substring("spell_slot_".Length);
                        if (int.TryParse(levelStr, out int level) && pool.HasResource("SpellSlot"))
                        {
                            if (!pool.Consume("SpellSlot", amount, level))
                                return (false, $"Failed to consume SpellSlot level {level}");
                            Godot.GD.Print($"[BG3Resource] {combatant.Name} consumed {amount} SpellSlot(L{level})");
                        }
                        continue;
                    }

                    // Direct resource name lookup
                    if (pool.HasResource(key))
                    {
                        if (!pool.Consume(key, amount))
                            return (false, $"Failed to consume {key}");
                        Godot.GD.Print($"[BG3Resource] {combatant.Name} consumed {amount} {key}");
                    }
                }
            }

            // Implicit spell slot consumption
            if (action?.SpellLevel > 0 && pool.HasResource("SpellSlot"))
            {
                bool hasExplicitSlot = cost.ResourceCosts?.Keys
                    .Any(k => k.StartsWith("spell_slot_", StringComparison.OrdinalIgnoreCase)) == true;
                if (!hasExplicitSlot)
                {
                    if (!pool.Consume("SpellSlot", 1, action.SpellLevel))
                        return (false, $"Failed to consume SpellSlot level {action.SpellLevel}");
                    Godot.GD.Print($"[BG3Resource] {combatant.Name} consumed 1 SpellSlot(L{action.SpellLevel})");
                }
            }

            return (true, null);
        }

        /// <summary>
        /// Returns resource costs from ActionCost.ResourceCosts that are NOT tracked by the combatant's
        /// BG3 ActionResources pool. These costs need to be validated/consumed by the legacy CombatantResourcePool.
        /// </summary>
        /// <param name="combatant">The combatant whose ActionResources to check.</param>
        /// <param name="cost">The action cost containing resource requirements.</param>
        /// <returns>Dictionary of resource costs that need legacy pool handling.</returns>
        private static Dictionary<string, int> GetLegacyFallbackCosts(Combatant combatant, ActionCost cost)
        {
            if (cost?.ResourceCosts == null || cost.ResourceCosts.Count == 0)
                return new Dictionary<string, int>();

            var pool = combatant.ActionResources;
            var legacy = new Dictionary<string, int>();

            foreach (var (key, amount) in cost.ResourceCosts)
            {
                if (amount <= 0) continue;

                // spell_slot_N is handled by BG3 SpellSlot if available
                if (key.StartsWith("spell_slot_", StringComparison.OrdinalIgnoreCase) &&
                    pool?.HasResource("SpellSlot") == true)
                    continue;

                // Direct resource tracked by BG3
                if (pool?.HasResource(key) == true)
                    continue;

                // Not handled by BG3 → needs legacy pool
                legacy[key] = amount;
            }

            return legacy;
        }

        private static AbilityType? ParseAbilityType(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
                return null;

            return actionName.Trim().ToLowerInvariant() switch
            {
                "str" or "strength" => AbilityType.Strength,
                "dex" or "dexterity" => AbilityType.Dexterity,
                "con" or "constitution" => AbilityType.Constitution,
                "int" or "intelligence" => AbilityType.Intelligence,
                "wis" or "wisdom" => AbilityType.Wisdom,
                "cha" or "charisma" => AbilityType.Charisma,
                _ => null
            };
        }

        private static int GetAbilityModifier(Combatant combatant, AbilityType action)
        {
            if (combatant?.Stats == null)
                return 0;

            return action switch
            {
                AbilityType.Strength => combatant.Stats.StrengthModifier,
                AbilityType.Dexterity => combatant.Stats.DexterityModifier,
                AbilityType.Constitution => combatant.Stats.ConstitutionModifier,
                AbilityType.Intelligence => combatant.Stats.IntelligenceModifier,
                AbilityType.Wisdom => combatant.Stats.WisdomModifier,
                AbilityType.Charisma => combatant.Stats.CharismaModifier,
                _ => 0
            };
        }

        private int GetAttackRollBonus(Combatant source, ActionDefinition action, HashSet<string> effectiveTags)
        {
            if (source == null || source.ResolvedCharacter == null)
                return 0;

            int proficiency = Math.Max(0, source.ProficiencyBonus);
            int abilityMod = 0;

            if (source.Stats != null && action.AttackType.HasValue)
            {
                switch (action.AttackType.Value)
                {
                    case AttackType.MeleeWeapon:
                    {
                        var weapon = source.MainHandWeapon;
                        bool isFinesse = weapon?.IsFinesse == true || effectiveTags.Contains("finesse");
                        abilityMod = isFinesse
                            ? Math.Max(source.Stats.StrengthModifier, source.Stats.DexterityModifier)
                            : source.Stats.StrengthModifier;
                        
                        // Check weapon proficiency
                        if (weapon != null && !IsWeaponProficient(source, weapon))
                            proficiency = 0;
                        break;
                    }
                    case AttackType.RangedWeapon:
                    {
                        var weapon = source.MainHandWeapon;
                        // Try to find the ranged weapon
                        if (weapon != null && !weapon.IsRanged && source.OffHandWeapon?.IsRanged == true)
                            weapon = source.OffHandWeapon;
                        
                        abilityMod = source.Stats.DexterityModifier;
                        
                        // Thrown weapons use STR
                        if (weapon?.IsThrown == true && !weapon.IsRanged)
                            abilityMod = source.Stats.StrengthModifier;
                        
                        // Check weapon proficiency
                        if (weapon != null && !IsWeaponProficient(source, weapon))
                            proficiency = 0;
                        break;
                    }
                    case AttackType.MeleeSpell:
                    case AttackType.RangedSpell:
                        abilityMod = GetSpellcastingAbilityModifier(source);
                        break;
                }
            }

            return abilityMod + proficiency;
        }

        /// <summary>
        /// Check if a combatant is proficient with a specific weapon.
        /// </summary>
        private bool IsWeaponProficient(Combatant combatant, QDND.Data.CharacterModel.WeaponDefinition weapon)
        {
            if (combatant.ResolvedCharacter?.Proficiencies == null)
                return true; // Old-style units are always proficient
            
            var profs = combatant.ResolvedCharacter.Proficiencies;
            
            // Check category proficiency (Simple, Martial)
            if (profs.IsProficientWithWeaponCategory(weapon.Category))
                return true;
            
            // Check specific weapon proficiency
            if (profs.IsProficientWithWeapon(weapon.WeaponType))
                return true;
            
            return false;
        }

        private int GetSavingThrowBonus(Combatant target, string saveType)
        {
            if (target == null)
                return 0;

            var action = ParseAbilityType(saveType);
            if (!action.HasValue)
                return 0;

            int bonus = action.Value switch
            {
                AbilityType.Strength => target.Stats?.StrengthModifier ?? 0,
                AbilityType.Dexterity => target.Stats?.DexterityModifier ?? 0,
                AbilityType.Constitution => target.Stats?.ConstitutionModifier ?? 0,
                AbilityType.Intelligence => target.Stats?.IntelligenceModifier ?? 0,
                AbilityType.Wisdom => target.Stats?.WisdomModifier ?? 0,
                AbilityType.Charisma => target.Stats?.CharismaModifier ?? 0,
                _ => 0
            };

            // If no stats are present but we do have a resolved character, fall back to resolver-based modifier.
            if (target.Stats == null && target.ResolvedCharacter != null)
            {
                bonus = GetAbilityModifier(target, action.Value);
            }

            if (target.ResolvedCharacter?.Proficiencies.IsProficientInSave(action.Value) == true)
            {
                bonus += Math.Max(0, target.ProficiencyBonus);
            }

            return bonus;
        }

        private int ComputeSaveDC(Combatant source, ActionDefinition action, HashSet<string> effectiveTags)
        {
            if (source?.ResolvedCharacter == null)
                return 10;

            int proficiency = Math.Max(0, source?.ProficiencyBonus ?? 0);
            bool isSpell = effectiveTags.Contains("spell") || effectiveTags.Contains("magic");

            if (isSpell)
            {
                return 8 + proficiency + GetSpellcastingAbilityModifier(source);
            }

            if (action.AttackType == AttackType.MeleeWeapon || action.AttackType == AttackType.RangedWeapon)
            {
                int strMod = source?.Stats?.StrengthModifier ?? 0;
                int dexMod = source?.Stats?.DexterityModifier ?? 0;
                return 8 + proficiency + Math.Max(strMod, dexMod);
            }

            return 10 + proficiency;
        }

        private int GetSpellcastingAbilityModifier(Combatant source)
        {
            if (source?.Stats == null || source.ResolvedCharacter == null)
                return 0;

            var latestClassLevel = source.ResolvedCharacter?.Sheet?.ClassLevels?.LastOrDefault();
            string classId = latestClassLevel?.ClassId?.ToLowerInvariant();

            return classId switch
            {
                "wizard" => source.Stats.IntelligenceModifier,
                "cleric" or "druid" or "ranger" or "monk" => source.Stats.WisdomModifier,
                "bard" or "sorcerer" or "warlock" or "paladin" => source.Stats.CharismaModifier,
                _ => Math.Max(source.Stats.IntelligenceModifier, Math.Max(source.Stats.WisdomModifier, source.Stats.CharismaModifier))
            };
        }

        private static int GetCriticalThreshold(Combatant source, bool isSpellAttack)
        {
            if (source?.ResolvedCharacter?.Features == null)
                return 20;

            bool hasImprovedCritical = source.ResolvedCharacter.Features.Any(f =>
                string.Equals(f.Id, "improved_critical", StringComparison.OrdinalIgnoreCase));
            bool hasSpellSniper = source.ResolvedCharacter.Sheet?.FeatIds?.Any(f =>
                string.Equals(f, "spell_sniper", StringComparison.OrdinalIgnoreCase)) == true;

            if (!isSpellAttack && hasImprovedCritical)
                return 19;
            if (isSpellAttack && hasSpellSniper)
                return 19;

            return 20;
        }

        private static void ApplyWindowRollSources(QueryInput query, RuleEventContext windowContext)
        {
            if (query == null || windowContext == null)
                return;

            MergeParameterSources(query.Parameters, "statusAdvantageSources", windowContext.AdvantageSources);
            MergeParameterSources(query.Parameters, "statusDisadvantageSources", windowContext.DisadvantageSources);
        }

        private static void MergeParameterSources(Dictionary<string, object> parameters, string key, List<string> toAdd)
        {
            if (parameters == null || toAdd == null || toAdd.Count == 0)
                return;

            var merged = new List<string>();
            if (parameters.TryGetValue(key, out var existing))
            {
                switch (existing)
                {
                    case IEnumerable<string> list:
                        merged.AddRange(list.Where(v => !string.IsNullOrWhiteSpace(v)));
                        break;
                    case string single when !string.IsNullOrWhiteSpace(single):
                        merged.Add(single);
                        break;
                }
            }

            merged.AddRange(toAdd.Where(v => !string.IsNullOrWhiteSpace(v)));
            parameters[key] = merged.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private (List<string> AdvantageSources, List<string> DisadvantageSources, bool AutoCritOnHit)
            GetStatusAttackContext(Combatant source, Combatant target, ActionDefinition action)
        {
            var advantages = new List<string>();
            var disadvantages = new List<string>();
            bool autoCritOnHit = false;

            if (Statuses == null || source == null || target == null || !action.AttackType.HasValue)
                return (advantages, disadvantages, autoCritOnHit);

            bool isMeleeAttack = action.AttackType == AttackType.MeleeWeapon ||
                                 action.AttackType == AttackType.MeleeSpell;
            bool isRangedOrSpellAttack = action.AttackType == AttackType.RangedWeapon ||
                                         action.AttackType == AttackType.RangedSpell;
            float distance = source.Position.DistanceTo(target.Position);

            // Prone: melee attacks have advantage, ranged attacks have disadvantage
            if (Statuses.HasStatus(target.Id, "prone"))
            {
                if (isMeleeAttack)
                {
                    advantages.Add("Prone Target");
                }
                else if (isRangedOrSpellAttack)
                {
                    disadvantages.Add("Prone Target");
                }
            }

            if (Statuses.HasStatus(target.Id, "blinded"))
            {
                advantages.Add("Target Blinded");
            }

            // Invisible target: attacks against invisible targets have disadvantage
            if (Statuses.HasStatus(target.Id, "invisible") || Statuses.HasStatus(target.Id, "greater_invisible"))
            {
                disadvantages.Add("Target Invisible");
            }

            // Blinded attacker: disadvantage on attacks
            if (Statuses.HasStatus(source.Id, "blinded"))
            {
                disadvantages.Add("Attacker Blinded");
            }

            if (Statuses.HasStatus(target.Id, "stunned"))
            {
                advantages.Add("Target Stunned");
            }

            // Restrained: attacks against restrained targets have advantage
            if (Statuses.HasStatus(target.Id, "restrained"))
            {
                advantages.Add("Target Restrained");
            }

            // Dodge mechanic: if target is dodging, attacks against them have disadvantage
            if (Statuses.HasStatus(target.Id, "dodging"))
            {
                disadvantages.Add("Target Dodging");
            }

            // Threatened condition: check if attacker is within 1.5m of any hostile for ranged/spell attacks
            if (isRangedOrSpellAttack)
            {
                // First check for existing threatened status (legacy support)
                if (Statuses.HasStatus(source.Id, "threatened"))
                {
                    disadvantages.Add("Threatened");
                }
                // Also dynamically check proximity to hostiles
                else if (GetCombatants != null && IsWithinHostileMeleeRange(source))
                {
                    disadvantages.Add("Threatened");
                }
            }

            if (Statuses.HasStatus(target.Id, "paralyzed"))
            {
                advantages.Add("Target Paralyzed");
                if (isMeleeAttack && distance <= 3f)
                {
                    autoCritOnHit = true;
                }
            }

            if (Statuses.HasStatus(target.Id, "asleep") && isMeleeAttack && distance <= 1.5f)
            {
                autoCritOnHit = true;
            }

            // Reckless Attack: enemies have advantage against a reckless barbarian
            if (Statuses.HasStatus(target.Id, "reckless"))
            {
                advantages.Add("Target Reckless");
            }

            return (advantages, disadvantages, autoCritOnHit);
        }

        /// <summary>
        /// Check if a combatant is within melee range (1.5m) of any hostile combatant.
        /// </summary>
        private bool IsWithinHostileMeleeRange(Combatant combatant)
        {
            if (GetCombatants == null)
                return false;

            const float meleeRange = 1.5f;

            foreach (var other in GetCombatants())
            {
                // Skip self
                if (other.Id == combatant.Id)
                    continue;

                // Skip non-hostile (same faction or inactive)
                if (other.Faction == combatant.Faction || !other.IsActive)
                    continue;

                // Check distance
                float dist = combatant.Position.DistanceTo(other.Position);
                if (dist <= meleeRange)
                    return true;
            }

            return false;
        }

        private bool ShouldAutoFailSave(Combatant target, string saveType)
        {
            if (Statuses == null || target == null || string.IsNullOrWhiteSpace(saveType))
                return false;

            string normalized = saveType.Trim().ToLowerInvariant();
            bool isStrengthOrDexterity = normalized == "strength" || normalized == "dexterity";
            if (!isStrengthOrDexterity)
                return false;

            return Statuses.HasStatus(target.Id, "paralyzed") || Statuses.HasStatus(target.Id, "stunned");
        }

        private string GetBlockedByStatusReason(Combatant source, string actionId, ActionCost cost)
        {
            if (Statuses == null || source == null)
                return null;

            var activeStatuses = Statuses.GetStatuses(source.Id);
            foreach (var status in activeStatuses)
            {
                var blocked = status.Definition.BlockedActions;
                if (blocked == null || blocked.Count == 0)
                    continue;

                if (blocked.Contains("*"))
                    return $"{status.Definition.Name} prevents acting";
                if (blocked.Contains(actionId))
                    return $"{status.Definition.Name} blocks {actionId}";
                if (cost?.UsesAction == true && blocked.Contains("action"))
                    return $"{status.Definition.Name} blocks actions";
                if (cost?.UsesBonusAction == true && blocked.Contains("bonus_action"))
                    return $"{status.Definition.Name} blocks bonus actions";
                if (cost?.UsesReaction == true && blocked.Contains("reaction"))
                    return $"{status.Definition.Name} blocks reactions";
                if (cost?.MovementCost > 0 && blocked.Contains("movement"))
                    return $"{status.Definition.Name} blocks movement";
            }

            return null;
        }

        /// <summary>
        /// Trigger ability cast reactions with effective tags.
        /// </summary>
        private ReactionTriggerEventArgs TryTriggerAbilityCastReactionsWithTags(
            Combatant source,
            ActionDefinition action,
            List<Combatant> targets,
            HashSet<string> effectiveTags)
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
                Position = source.Position,
                IsCancellable = !effectiveTags.Contains("uncounterable"),
                Data = new Dictionary<string, object>
                {
                    { "actionName", action.Name },
                    { "targetCount", targets.Count }
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
                OnAbilityCastTrigger?.Invoke(this, args);
            }

            return args;
        }

        /// <summary>
        /// Preview an ability's expected outcomes.
        /// </summary>
        public Dictionary<string, (float Min, float Max, float Avg)> PreviewAbility(
            string actionId,
            Combatant source,
            List<Combatant> targets)
        {
            var previews = new Dictionary<string, (float, float, float)>();

            if (!_actions.TryGetValue(actionId, out var action))
            {
                action = ActionRegistry?.GetAction(actionId);
                if (action == null)
                    return previews;
            }

            var context = new EffectContext
            {
                Source = source,
                Targets = targets,
                Ability = action,
                Rules = Rules,
                Statuses = Statuses,
                Surfaces = Surfaces,
                ForcedMovement = ForcedMovement
            };

            foreach (var effectDef in action.Effects)
            {
                if (_effectHandlers.TryGetValue(effectDef.Type, out var handler))
                {
                    var preview = handler.Preview(effectDef, context);
                    previews[effectDef.Type] = preview;
                }
            }

            return previews;
        }

        /// <summary>
        /// Process turn start (tick cooldowns).
        /// </summary>
        public void ProcessTurnStart(string combatantId)
        {
            var toRemove = new List<string>();

            foreach (var (key, cooldown) in _cooldowns)
            {
                if (key.StartsWith(combatantId + ":"))
                {
                    if (cooldown.DecrementType == "turn")
                    {
                        cooldown.RemainingCooldown--;
                        if (cooldown.RemainingCooldown <= 0)
                        {
                            cooldown.CurrentCharges = Math.Min(
                                cooldown.CurrentCharges + 1,
                                cooldown.MaxCharges
                            );
                            cooldown.RemainingCooldown = 0;
                        }
                    }
                }
            }

            foreach (var key in toRemove)
            {
                _cooldowns.Remove(key);
            }
        }

        /// <summary>
        /// Process round end (tick round-based cooldowns).
        /// </summary>
        public void ProcessRoundEnd()
        {
            foreach (var (key, cooldown) in _cooldowns)
            {
                if (cooldown.DecrementType == "round")
                {
                    cooldown.RemainingCooldown--;
                    if (cooldown.RemainingCooldown <= 0)
                    {
                        cooldown.CurrentCharges = Math.Min(
                            cooldown.CurrentCharges + 1,
                            cooldown.MaxCharges
                        );
                        cooldown.RemainingCooldown = 0;
                    }
                }
            }
        }

        private void ConsumeCooldown(string combatantId, string actionId, ActionDefinition action)
        {
            // Only track cooldowns for abilities that have a cooldown defined
            if (action.Cooldown.TurnCooldown == 0 && action.Cooldown.RoundCooldown == 0)
                return;

            var key = $"{combatantId}:{actionId}";

            if (!_cooldowns.TryGetValue(key, out var cooldown))
            {
                cooldown = new ActionCooldownState
                {
                    MaxCharges = action.Cooldown.MaxCharges,
                    CurrentCharges = action.Cooldown.MaxCharges,
                    DecrementType = action.Cooldown.TurnCooldown > 0 ? "turn" : "round"
                };
                _cooldowns[key] = cooldown;
            }

            cooldown.CurrentCharges--;
            if (cooldown.CurrentCharges < cooldown.MaxCharges)
            {
                cooldown.RemainingCooldown = action.Cooldown.TurnCooldown > 0
                    ? action.Cooldown.TurnCooldown
                    : action.Cooldown.RoundCooldown;
            }
        }

        private bool CheckRequirement(ActionRequirement req, Combatant source)
        {
            return req.Type switch
            {
                "hp_above" => source.Resources.CurrentHP > float.Parse(req.Value),
                "hp_below" => source.Resources.CurrentHP < float.Parse(req.Value),
                "has_status" => Statuses?.HasStatus(source.Id, req.Value) ?? false,
                _ => true // Unknown requirements pass by default
            };
        }

        /// <summary>
        /// Check for SpellCastNearby reactions when an ability is cast.
        /// Returns the trigger args with eligible reactors, or null if no reactions system.
        /// </summary>
        private ReactionTriggerEventArgs TryTriggerAbilityCastReactions(
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
                    { "targetCount", targets.Count }
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
                OnAbilityCastTrigger?.Invoke(this, args);
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
                OnDamageTrigger?.Invoke(this, args);
            }

            return args;
        }

        /// <summary>
        /// Reset for new combat.
        /// </summary>
        public void Reset()
        {
            _cooldowns.Clear();
        }

        /// <summary>
        /// Export all cooldown states.
        /// </summary>
        public List<Persistence.CooldownSnapshot> ExportCooldowns()
        {
            var snapshots = new List<Persistence.CooldownSnapshot>();

            foreach (var (key, cooldown) in _cooldowns)
            {
                var parts = key.Split(':');
                if (parts.Length != 2)
                    continue;

                snapshots.Add(new Persistence.CooldownSnapshot
                {
                    CombatantId = parts[0],
                    ActionId = parts[1],
                    MaxCharges = cooldown.MaxCharges,
                    CurrentCharges = cooldown.CurrentCharges,
                    RemainingCooldown = cooldown.RemainingCooldown,
                    DecrementType = cooldown.DecrementType
                });
            }

            return snapshots;
        }

        /// <summary>
        /// Import cooldown states from snapshots.
        /// </summary>
        public void ImportCooldowns(List<Persistence.CooldownSnapshot> snapshots)
        {
            if (snapshots == null)
                return;

            // Clear existing cooldowns
            _cooldowns.Clear();

            // Restore from snapshots
            foreach (var snapshot in snapshots)
            {
                var key = $"{snapshot.CombatantId}:{snapshot.ActionId}";
                _cooldowns[key] = new ActionCooldownState
                {
                    MaxCharges = snapshot.MaxCharges,
                    CurrentCharges = snapshot.CurrentCharges,
                    RemainingCooldown = snapshot.RemainingCooldown,
                    DecrementType = snapshot.DecrementType ?? "turn"
                };
            }
        }
    }

    /// <summary>
    /// Tracks cooldown state for an action.
    /// </summary>
    internal class ActionCooldownState
    {
        public int MaxCharges { get; set; }
        public int CurrentCharges { get; set; }
        public int RemainingCooldown { get; set; }
        public string DecrementType { get; set; } // "turn" or "round"
    }
}
