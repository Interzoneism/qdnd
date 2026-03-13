using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Reactions;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Conditions;
using QDND.Combat.Statuses;
using QDND.Data;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Actions
{
    /// <summary>
    /// Manages ability execution and effect resolution.
    /// </summary>
    public class EffectPipeline
    {
        private readonly Dictionary<string, Effect> _effectHandlers = new();
        private readonly Dictionary<string, ActionDefinition> _actions = new(StringComparer.OrdinalIgnoreCase);
        private ReactionSystem _reactions;
        private IReactionResolver _reactionResolver;
        private StatusManager _statuses;
        private CooldownTracker _cooldowns;
        private ResourceCostEngine _resources;
        private ConcentrationSystem _concentration;
        private QDND.Combat.Services.IAbilityTestPolicy _testPolicy = QDND.Combat.Services.NoOpAbilityTestPolicy.Instance;
        private QDND.Combat.Services.ICombatContext _combatContext;
        private Func<IEnumerable<Combatant>> _getCombatants;
        private CharacterDataRegistry _characterDataRegistry;

        public RulesEngine Rules { get; set; }
        public StatusManager Statuses
        {
            get => _statuses;
            set
            {
                _statuses = value;
                if (Rolls != null)
                    Rolls.Statuses = value;
                if (Validator != null)
                    Validator.Statuses = value;
            }
        }
        public Random Rng { get; set; }

        /// <summary>
        /// Optional centralized action registry for spell and ability lookups.
        /// If set, GetAction() will fallback to registry when action not found locally.
        /// </summary>
        private ActionRegistry _actionRegistry;
        public ActionRegistry ActionRegistry
        {
            get => _actionRegistry;
            set
            {
                _actionRegistry = value;
                if (Cooldowns != null) Cooldowns.ActionRegistry = value;
            }
        }

        /// <summary>
        /// Optional cooldown tracker for ability charges and cooldown timers.
        /// </summary>
        public CooldownTracker? Cooldowns
        {
            get => _cooldowns;
            set
            {
                _cooldowns = value;
                if (Validator != null)
                    Validator.Cooldowns = value;
            }
        }

        /// <summary>
        /// Resource validation/consumption engine for BG3 ActionResources.
        /// </summary>
        public ResourceCostEngine Resources
        {
            get => _resources;
            set
            {
                _resources = value;
                if (Validator != null)
                    Validator.Resources = value;
            }
        }

        public ReactionTriggerDispatcher ReactionTriggers { get; set; }

        public EffectBuilder Builder { get; set; }

        public CombatRollResolver Rolls { get; set; }

        public ActionValidator Validator { get; set; }

        /// <summary>
        /// Optional combat context for service location.
        /// </summary>
        public QDND.Combat.Services.ICombatContext CombatContext
        {
            get => _combatContext;
            set
            {
                _combatContext = value;
                if (Rolls != null)
                    Rolls.CombatContext = value;
            }
        }

        /// <summary>
        /// Optional turn queue service for summon effects.
        /// </summary>
        public QDND.Combat.Services.TurnQueueService TurnQueue { get; set; }

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
        public ReactionSystem Reactions
        {
            get => _reactions;
            set
            {
                _reactions = value;
                if (ReactionTriggers != null)
                    ReactionTriggers.Reactions = value;
            }
        }

        /// <summary>
        /// Optional centralized resolver that can immediately execute interrupts/reactions.
        /// </summary>
        public IReactionResolver ReactionResolver
        {
            get => _reactionResolver;
            set
            {
                _reactionResolver = value;
                if (ReactionTriggers != null)
                    ReactionTriggers.ReactionResolver = value;
            }
        }

        /// <summary>
        /// Optional concentration system for tracking concentration effects.
        /// </summary>
        public ConcentrationSystem Concentration
        {
            get => _concentration;
            set
            {
                _concentration = value;
                if (Validator != null)
                    Validator.Concentration = value;
            }
        }

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
        /// Optional data registry for beast form lookups.
        /// </summary>
        public QDND.Data.DataRegistry DataRegistry { get; set; }

        /// <summary>
        /// Optional inventory service for effects that manipulate items.
        /// </summary>
        // No cascade needed — flows into EffectContext at execution time, not used by sub-components at construction
        public QDND.Combat.Services.InventoryService InventoryService { get; set; }

        /// <summary>
        /// Optional character registry for spellcasting ability lookups in roll resolution.
        /// </summary>
        public CharacterDataRegistry CharacterDataRegistry
        {
            get => _characterDataRegistry;
            set
            {
                _characterDataRegistry = value;
                if (Rolls != null)
                    Rolls.CharacterDataRegistry = value;
            }
        }

        /// <summary>
        /// All combatants in combat (for reaction eligibility checking).
        /// </summary>
        public Func<IEnumerable<Combatant>> GetCombatants
        {
            get => _getCombatants;
            set
            {
                _getCombatants = value;
                if (ReactionTriggers != null)
                    ReactionTriggers.GetCombatants = value;
                if (Rolls != null)
                    Rolls.GetCombatants = value;
            }
        }

        /// <summary>
        /// Policy that controls ability-test bypasses. Defaults to no-op (production).
        /// Set to <see cref="QDND.Combat.Services.TagBasedAbilityTestPolicy"/> for action test scenarios.
        /// </summary>
        public QDND.Combat.Services.IAbilityTestPolicy TestPolicy
        {
            get => _testPolicy;
            set
            {
                _testPolicy = value ?? QDND.Combat.Services.NoOpAbilityTestPolicy.Instance;
                if (Validator != null)
                    Validator.TestPolicy = _testPolicy;
            }
        }

        public event Action<ActionExecutionResult> OnAbilityExecuted;

        /// <summary>
        /// Invoke OnAbilityExecuted from outside the class (e.g. for validation failures).
        /// </summary>
        public void NotifyAbilityExecuted(ActionExecutionResult result) => OnAbilityExecuted?.Invoke(result);

        /// <summary>
        /// Fired when an effect type has no registered handler.
        /// Args: effectType, abilityId
        /// </summary>
        public event Action<string, string> OnEffectUnhandled;

        /// <summary>
        /// Fired before damage is dealt - allows reaction checks for shields/damage reduction.
        /// </summary>
        public event Action<ReactionTriggerEventArgs> OnDamageTrigger
        {
            add => ReactionTriggers.OnDamageTrigger += value;
            remove => ReactionTriggers.OnDamageTrigger -= value;
        }

        /// <summary>
        /// Fired when an ability is cast - allows reaction checks for counterspell-type reactions.
        /// </summary>
        public event Action<ReactionTriggerEventArgs> OnAbilityCastTrigger
        {
            add => ReactionTriggers.OnAbilityCastTrigger += value;
            remove => ReactionTriggers.OnAbilityCastTrigger -= value;
        }

        /// <summary>
        /// Fired when a combatant is attacked (after attack roll, before effects) - allows reactions like Shield.
        /// </summary>
        public event Action<ReactionTriggerEventArgs> OnAttackTrigger
        {
            add => ReactionTriggers.OnAttackTrigger += value;
            remove => ReactionTriggers.OnAttackTrigger -= value;
        }

        /// <summary>
        /// Fired when a combatant is hit (attack succeeded, before damage) - allows reactions like Uncanny Dodge.
        /// </summary>
        public event Action<ReactionTriggerEventArgs> OnHitTrigger
        {
            add => ReactionTriggers.OnHitTrigger += value;
            remove => ReactionTriggers.OnHitTrigger -= value;
        }

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

            // Summon / unsummon effects
            RegisterEffect(new SummonCombatantEffect());
            RegisterEffect(new UnsummonCombatantEffect());
            RegisterEffect(new TeleportSummonEffect());

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
            RegisterEffect(new StabilizeEffect());
            RegisterEffect(new KillEffect());
            RegisterEffect(new ResurrectEffect());

            // Phase 2 parity handlers — fully implemented
            RegisterEffect(new SpawnExtraProjectilesEffect());
            RegisterEffect(new DouseEffect());
            RegisterEffect(new SpawnInventoryItemEffect());
            RegisterEffect(new FireProjectileEffect());
            RegisterEffect(new EqualizeEffect());
            RegisterEffect(new SetStatusDurationEffect());
            RegisterEffect(new PickupEntityEffect());
            RegisterEffect(new SwapPlacesEffect());
            RegisterEffect(new GrantEffect());
            RegisterEffect(new UseSpellEffect());
            RegisterEffect(new ExecuteWeaponFunctorsEffect());
            RegisterEffect(new SurfaceChangeEffect());
            RegisterEffect(new SurfaceClearLayerEffect());
            RegisterEffect(new RemoveStatusByGroupEffect());
            RegisterEffect(new SwitchDeathTypeEffect());
            RegisterEffect(new SetAdvantageEffect());
            RegisterEffect(new SetDisadvantageEffect());

            // Default tracker keeps cooldown behavior working for direct EffectPipeline usage.
            Cooldowns = new CooldownTracker(ActionRegistry);
            ReactionTriggers = new ReactionTriggerDispatcher();
            Builder = new EffectBuilder();
            Resources = new ResourceCostEngine();
            Rolls = new CombatRollResolver(CombatContext, Statuses, GetCombatants);
            Rolls.CharacterDataRegistry = CharacterDataRegistry;
            Validator = new ActionValidator();
            Validator.GetAction = GetAction;
            Validator.Cooldowns = Cooldowns;
            Validator.Resources = Resources;
            Validator.Statuses = Statuses;
            Validator.Concentration = Concentration;
            Validator.TestPolicy = TestPolicy;
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
        /// When re-registering an action that already exists, preserves resourceCosts
        /// and spellLevel from the existing definition if the new one lacks them.
        /// This prevents legacy JSON definitions from stripping BG3-parsed spell costs.
        /// </summary>
        public void RegisterAction(ActionDefinition action)
        {
            if (_actions.TryGetValue(action.Id, out var existing))
            {
                // Preserve existing resourceCosts if new definition has none
                bool newHasResourceCosts = action.Cost?.ResourceCosts != null && action.Cost.ResourceCosts.Count > 0;
                bool existingHasResourceCosts = existing.Cost?.ResourceCosts != null && existing.Cost.ResourceCosts.Count > 0;
                if (existingHasResourceCosts && !newHasResourceCosts)
                {
                    action.Cost ??= new ActionCost();
                    action.Cost.ResourceCosts = new Dictionary<string, int>(existing.Cost.ResourceCosts);
                }

                // Preserve existing spellLevel if new definition has 0
                if (existing.SpellLevel > 0 && action.SpellLevel == 0)
                {
                    action.SpellLevel = existing.SpellLevel;
                }

                // Preserve existing effects if the new definition has none but the existing one does.
                // This prevents BG3-parsed actions from overwriting curated JSON actions that have
                // valid damage formulas with broken/empty definitions.
                bool existingHasEffects = existing.Effects != null && existing.Effects.Count > 0;
                bool newHasEffects = action.Effects != null && action.Effects.Count > 0;
                if (existingHasEffects && !newHasEffects)
                {
                    action.Effects = existing.Effects;
                }
            }
            else if (ActionRegistry != null)
            {
                // BG3-parsed actions are stored in ActionRegistry under BG3 IDs
                // (e.g., "Target_HuntersMark") while legacy JSON actions use game IDs
                // (e.g., "hunters_mark"). Search ActionRegistry for a matching BG3 action
                // to merge spell costs and spell level from.
                bool newHasResourceCosts = action.Cost?.ResourceCosts != null && action.Cost.ResourceCosts.Count > 0;
                if (!newHasResourceCosts || action.SpellLevel == 0)
                {
                    var bg3Match = FindMatchingBg3Action(action.Id);
                    if (bg3Match != null)
                    {
                        if (!newHasResourceCosts)
                        {
                            bool bg3HasCosts = bg3Match.Cost?.ResourceCosts != null && bg3Match.Cost.ResourceCosts.Count > 0;
                            if (bg3HasCosts)
                            {
                                action.Cost ??= new ActionCost();
                                action.Cost.ResourceCosts = new Dictionary<string, int>(bg3Match.Cost.ResourceCosts);
                            }
                        }
                        if (bg3Match.SpellLevel > 0 && action.SpellLevel == 0)
                        {
                            action.SpellLevel = bg3Match.SpellLevel;
                        }
                    }
                }
            }

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
        /// Compute the save DC for a source/action pair. Used by preview UI.
        /// </summary>
        public int GetSaveDC(Combatant source, ActionDefinition action)
        {
            var tags = new HashSet<string>(action?.Tags ?? Enumerable.Empty<string>());
            return Rolls.ComputeSaveDC(source, action, tags);
        }

        /// <summary>
        /// Get a target's saving throw bonus for a given save type. Used by preview UI.
        /// </summary>
        public int GetSaveBonus(Combatant target, string saveType)
        {
            return Rolls.GetSavingThrowBonus(target, saveType);
        }

        /// <summary>
        /// Get the total attack roll bonus for a combatant using a specific action.
        /// Public wrapper for hover UI hit chance display.
        /// </summary>
        public int GetAttackBonus(Combatant source, ActionDefinition action)
        {
            var tags = new HashSet<string>(action?.Tags ?? Enumerable.Empty<string>());
            return Rolls.GetAttackRollBonus(source, action, tags);
        }

        /// <summary>
        /// Check if an ability can be used.
        /// </summary>
        public (bool CanUse, string Reason) CanUseAbility(string actionId, Combatant source)
            => Validator.CanUseAbility(actionId, source);

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
            // Auto-select first variant if action has no base effects but has variants with effects
            // (e.g., Shove which requires choosing Push or Knock Prone)
            else if (action.Effects.Count == 0 && action.Variants?.Count > 0)
            {
                variant = action.Variants[0];
                QDND.Data.RuntimeSafety.Log($"[EffectPipeline] Auto-selecting variant '{variant.VariantId}' for {actionId} (no base effects)");
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
            var effectiveCost = Builder.BuildEffectiveCost(action, variant, options.UpcastLevel);

            // Validate and consume costs unless skipped (for Extra Attack).
            // Test actors go through the normal path: CanUseAbilityWithCost skips their
            // resource checks but enforces cooldown and budget.
            if (!options.SkipCostValidation)
            {
                var (canUse, reason) = Validator.CanUseAbilityWithCost(actionId, source, effectiveCost, options.IgnoreReactionBudgetCheck);
                if (!canUse)
                    return ActionExecutionResult.Failure(actionId, source.Id, reason);

                var budgetCost = ResourceCostEngine.BuildBudgetCostOverride(effectiveCost, options.SkipReactionBudgetConsumption);

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
                    if (budgetCost.UsesBonusAction && source.ActionBudget != null)
                        source.ActionBudget.ConsumeBonusAction();
                    if (budgetCost.UsesReaction && source.ActionBudget != null)
                        source.ActionBudget.ConsumeReaction();
                    if (budgetCost.MovementCost > 0 && source.ActionBudget != null)
                        source.ActionBudget.ConsumeMovement(budgetCost.MovementCost);
                }
                else
                {
                    // Non-weapon actions consume budget and then derive attack pool state
                    // from remaining action charges.
                    source.ActionBudget?.ConsumeCost(budgetCost);
                    if (source.ActionBudget != null && budgetCost.UsesAction)
                    {
                        source.ActionBudget.RefreshAttacksFromAvailableActions();
                    }
                }

                // Consume BG3 ActionResources first
                var (bg3Success, bg3ConsumeReason) = Resources.ConsumeBG3ResourceCost(source, action, effectiveCost);
                if (!bg3Success)
                    return ActionExecutionResult.Failure(actionId, source.Id, bg3ConsumeReason);

                if (source.ActionBudget != null &&
                    effectiveCost.UsesBonusAction &&
                    action.SpellLevel > 0)
                {
                    source.ActionBudget.HasCastLeveledBonusActionSpell = true;
                }
            }

            // Build effective effects list
            var effectiveEffects = Builder.BuildEffectiveEffects(action.Effects, variant, options.UpcastLevel, action.UpcastScaling);
            
            // Issue 1: Resolve dynamic formulas in effects (SpellcastingAbilityModifier, MainMeleeWeapon, etc.)
            var charRegistry = CharacterDataRegistry;
            foreach (var effect in effectiveEffects)
            {
                if (!string.IsNullOrEmpty(effect.DiceFormula))
                {
                    effect.DiceFormula = QDND.Data.Actions.SpellEffectConverter.ResolveDynamicFormula(
                        effect.DiceFormula, source, charRegistry);
                }
                // Also resolve dynamic damage type strings (e.g., MainMeleeWeaponDamageType)
                if (!string.IsNullOrEmpty(effect.DamageType))
                {
                    effect.DamageType = QDND.Data.Actions.SpellEffectConverter.ResolveDynamicFormula(
                        effect.DamageType, source, charRegistry);
                }
            }

            // Build effective projectile count (for multi-projectile upcast scaling like Magic Missile)
            int effectiveProjectileCount = action.ProjectileCount;
            if (options.UpcastLevel > 0 && action.UpcastScaling?.ProjectilesPerLevel > 0)
            {
                effectiveProjectileCount += options.UpcastLevel * action.UpcastScaling.ProjectilesPerLevel;
            }

            // Apply TargetsPerLevel upcast scaling to MaxTargets
            int effectiveMaxTargets = action.MaxTargets;
            if (options.UpcastLevel > 0 && action.UpcastScaling?.TargetsPerLevel > 0)
            {
                effectiveMaxTargets += options.UpcastLevel * action.UpcastScaling.TargetsPerLevel;
                // Only enforce target trimming when upcast target scaling is active
                // (normal MaxTargets enforcement is handled by targeting validation upstream)
                if (targets.Count > effectiveMaxTargets)
                {
                    targets = targets.Take(effectiveMaxTargets).ToList();
                }
            }

            // Build effective tags
            var effectiveTags = Builder.BuildEffectiveTags(action.Tags, variant);

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
                InventoryService = InventoryService,
                TurnQueue = TurnQueue,
                DataRegistry = DataRegistry,
                OnHitTriggerService = OnHitTriggerService,
                Pipeline = this,
                TriggerContext = options.TriggerContext,
                OnBeforeDamage = (src, tgt, dmg, dmgType) =>
                {
                    var triggerArgs = ReactionTriggers.TryTriggerDamageReactions(src, tgt, dmg, dmgType, action.Id);
                    return triggerArgs?.DamageModifier ?? 1.0f;
                }
            };

            // Dispatch ability declared event
            Rules?.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.AbilityDeclared,
                SourceId = source.Id,
                ActionId = actionId,
                Tags = new HashSet<string>(effectiveTags),
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
            var spellCastTrigger = ReactionTriggers.TryTriggerAbilityCastReactionsWithTags(source, action, targets, effectiveTags, options);
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

            // Snapshot positions before execution for forensic logging
            result.SourcePositionBefore = new[] { source.Position.X, source.Position.Y, source.Position.Z };
            foreach (var target in targets)
            {
                result.TargetPositionsBefore[target.Id] = new[] { target.Position.X, target.Position.Y, target.Position.Z };
            }

            // Issue 5: Skip global attack roll for multi-projectile spells (they roll per-projectile)
            bool skipGlobalAttackRoll = effectiveProjectileCount > 1;

            // BG3/5e rule: when casting a new concentration spell, break old concentration
            // BEFORE the attack roll so the old spell's effects (e.g., paralyzed from Hold Person)
            // are removed before advantage/disadvantage is evaluated.
            if (action.RequiresConcentration && Concentration != null)
            {
                Concentration.BreakConcentration(source.Id, "casting new concentration spell");
            }

            // Roll attack if needed
            if (action.AttackType.HasValue && targets.Count > 0 && !skipGlobalAttackRoll)
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
                LOSResult losResult = null;
                if (LOS != null)
                {
                    losResult = LOS.CheckLOS(source, primaryTarget);
                    coverACBonus = losResult.GetACBonus();
                }

                if (isRangedAttack && losResult?.RangedAttacksBlocked == true)
                {
                    return ActionExecutionResult.Failure(actionId, source.Id,
                        "Ranged attack blocked by obscuring cloud");
                }

                var attackQuery = new QueryInput
                {
                    Type = QueryType.AttackRoll,
                    Source = source,
                    Target = primaryTarget,
                    BaseValue = Rolls.GetAttackRollBonus(source, action, effectiveTags) + heightMod
                };
                var attackTags = new HashSet<string>(effectiveTags);
                if (isMeleeAttack) attackTags.Add("melee_attack");
                if (isRangedAttack) attackTags.Add("ranged_attack");
                if (isSpellAttack) attackTags.Add("spell_attack");
                if (isMeleeAttack && source.MainHandWeapon?.IsTwoHanded == true) attackTags.Add("weapon:two_handed");
                if (isMeleeAttack && source.MainHandWeapon?.IsVersatile == true) attackTags.Add("weapon:versatile");
                attackTags.ToList().ForEach(t => attackQuery.Tags.Add(t));

                var condAdvantages = new List<string>();
                var condDisadvantages = new List<string>();
                bool autoCritOnHit = false;
                if (Statuses != null)
                {
                    float attackDistance = source.Position.DistanceTo(primaryTarget.Position);
                    var srcIds = Statuses.GetStatuses(source.Id).SelectMany(s => new[] { s.Definition.Id }.Concat(s.Definition.Tags ?? Enumerable.Empty<string>()));
                    var tgtIds = Statuses.GetStatuses(primaryTarget.Id).SelectMany(s => new[] { s.Definition.Id }.Concat(s.Definition.Tags ?? Enumerable.Empty<string>()));
                    var srcEffects = ConditionEffects.GetAggregateEffects(srcIds, isMeleeAttack);
                    var tgtEffects = ConditionEffects.GetAggregateEffects(tgtIds, isMeleeAttack);
                    condAdvantages.AddRange(srcEffects.AttackAdvantageSources.Select(id => $"Attacker {id}"));
                    condDisadvantages.AddRange(srcEffects.AttackDisadvantageSources.Select(id => $"Attacker {id}"));
                    condAdvantages.AddRange(tgtEffects.DefenseAdvantageSources.Select(id => $"Target {id}"));
                    condDisadvantages.AddRange(tgtEffects.DefenseDisadvantageSources.Select(id => $"Target {id}"));
                    if (CombatRollResolver.ShouldApplyMeleeAutoCrit(tgtEffects.MeleeAutocrits, isMeleeAttack, attackDistance))
                        autoCritOnHit = true;
                    if (Statuses.HasStatus(primaryTarget.Id, "dodging"))
                        condDisadvantages.Add("Target Dodging");
                    if (Statuses.HasStatus(source.Id, "reckless"))
                        condAdvantages.Add("Reckless Attack");
                    if (Statuses.HasStatus(primaryTarget.Id, "reckless"))
                        condAdvantages.Add("Target Reckless");
                    if (Statuses.HasStatus(source.Id, "hidden"))
                        condAdvantages.Add("Hidden Attacker");
                    if (Statuses.HasStatus(source.Id, "helped"))
                        condAdvantages.Add("Helped");
                    if (Statuses.HasStatus(primaryTarget.Id, "hidden"))
                        condDisadvantages.Add("Target Hidden");
                    if (QDND.Combat.Rules.Boosts.BoostEvaluator.HasSourceAdvantageOnAttack(source, primaryTarget, Statuses))
                        condAdvantages.Add("SourceAdvantageOnAttack");
                    if (isRangedAttack)
                    {
                        // Crossbow Expert: removes ranged disadvantage in melee, but ONLY for crossbow attacks
                        bool hasCrossbowExpert = source.ResolvedCharacter?.Sheet?.FeatIds?.Contains("crossbow_expert") == true;
                        bool isAttackingWithCrossbow = source.MainHandWeapon?.WeaponType is
                            WeaponType.LightCrossbow or WeaponType.HandCrossbow or WeaponType.HeavyCrossbow;
                        if (!(hasCrossbowExpert && isAttackingWithCrossbow) &&
                            (Statuses.HasStatus(source.Id, "threatened") ||
                             (GetCombatants != null && Rolls.IsWithinHostileMeleeRange(source))))
                            condDisadvantages.Add("Threatened");
                    }

                    if (losResult != null)
                    {
                        if (losResult.SourceObscurity == ObscurityTier.HeavilyObscured)
                            condDisadvantages.Add("Heavily Obscured (Source)");
                        if (losResult.TargetObscurity == ObscurityTier.HeavilyObscured)
                            condDisadvantages.Add("Heavily Obscured (Target)");
                        if (losResult.LineObscurity == ObscurityTier.HeavilyObscured)
                            condDisadvantages.Add("Heavily Obscured (Between)");
                    }
                }
                if (condAdvantages.Count > 0)
                    attackQuery.Parameters["statusAdvantageSources"] = condAdvantages;
                if (condDisadvantages.Count > 0)
                    attackQuery.Parameters["statusDisadvantageSources"] = condDisadvantages;
                if (autoCritOnHit)
                    attackQuery.Parameters["autoCritOnHit"] = true;

                attackQuery.Parameters["criticalThreshold"] = CombatRollResolver.GetCriticalThreshold(source, isSpellAttack);

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

                CombatRollResolver.ApplyWindowRollSources(attackQuery, beforeAttackContext);

                context.AttackResult = Rules.RollAttack(attackQuery);
                result.AttackResult = context.AttackResult;

                // Mobile feat: record this melee attack for OA exemption tracking
                if (isMeleeAttack)
                    source.AttackedThisTurn.Add(primaryTarget.Id);

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

                // === Fire YouAreAttacked reactions (e.g., Shield, Cutting Words) ===
                if (context.AttackResult != null)
                {
                    var attackReactions = ReactionTriggers.TryTriggerAttackReactions(source, primaryTarget, action,
                        action.AttackType?.ToString(), context.AttackResult?.IsSuccess ?? true);
                    if (attackReactions != null)
                    {
                        int acMod = attackReactions.ACModifier;
                        int rollMod = attackReactions.RollModifier;

                        // Re-evaluate hit if AC was increased or roll was reduced
                        if ((acMod != 0 || rollMod != 0) && context.AttackResult.IsSuccess && !context.AttackResult.IsCritical && Rules != null)
                        {
                            float effectiveTotal = context.AttackResult.FinalValue + rollMod;
                            float targetAC = Rules.GetArmorClass(primaryTarget) + acMod;
                            if (effectiveTotal < targetAC)
                            {
                                context.AttackResult.IsSuccess = false;
                                result.AttackResult = context.AttackResult;
                            }
                        }
                    }
                }

                // === Fire YouAreHit reactions (e.g., Uncanny Dodge, Hellish Rebuke) ===
                if (context.AttackResult?.IsSuccess == true)
                {
                    var hitReactions = ReactionTriggers.TryTriggerHitReactions(source, primaryTarget, 0,
                        action.Effects?.FirstOrDefault(e => e.Type == "deal_damage")?.DamageType ?? "untyped",
                        action.AttackType?.ToString(),
                        context.AttackResult.IsCritical, action.Id);
                    if (hitReactions != null)
                    {
                        context.HitDamageModifier = hitReactions.DamageModifier;
                    }
                }

                // Remove statuses with RemoveOnAttack (e.g., hidden)
                if (Statuses != null)
                {
                    Statuses.RemoveStatusesOnAttack(source.Id);
                }
            }

            // --- Contested check resolution (e.g., Shove: Athletics vs max(Athletics, Acrobatics)) ---
            if (string.Equals(action.ResolutionType, "contest", StringComparison.OrdinalIgnoreCase)
                && targets.Count > 0)
            {
                foreach (var target in targets)
                {
                    int attackerMod = Rolls.GetContestSkillBonus(source, action.ContestAttackerSkill);
                    int defenderMod = Rolls.GetBestContestSkillBonus(target, action.ContestDefenderSkills);

                    string attackerSkillName = action.ContestAttackerSkill ?? "Athletics";
                    string defenderSkillName = action.ContestDefenderSkills ?? "Athletics";

                    var contestResult = Rules.Contest(
                        source, target,
                        attackerMod, defenderMod,
                        attackerSkillName, defenderSkillName,
                        TiePolicy.DefenderWins);

                    result.ContestResult = contestResult;
                    context.ContestResult = contestResult;

                    // Synthesize a SaveResult so downstream on_save_fail conditions work unchanged.
                    // If attacker won → defender failed the "save".
                    var syntheticSave = new QueryResult
                    {
                        Input = new QueryInput
                        {
                            Type = QueryType.Contest,
                            Source = source,
                            Target = target,
                            DC = contestResult.RollA,
                            BaseValue = defenderMod
                        },
                        BaseValue = defenderMod,
                        NaturalRoll = contestResult.NaturalRollB,
                        FinalValue = contestResult.RollB,
                        IsSuccess = contestResult.DefenderWon
                    };

                    context.SaveResult = syntheticSave;
                    result.SaveResult = syntheticSave;
                    context.PerTargetSaveResults[target.Id] = syntheticSave;
                }
            }

            // Roll save if needed
            if (!string.IsNullOrEmpty(action.SaveType)
                && !string.Equals(action.ResolutionType, "contest", StringComparison.OrdinalIgnoreCase)
                && targets.Count > 0)
            {
                // For actions with both attack roll and save: only roll save if attack hit,
                // unless effects exist that need saves regardless of attack result (e.g., Ice Knife splash)
                bool hasEffectsNeedingSave = effectiveEffects.Any(e =>
                    e.Condition is "on_failed_save" or "on_save_fail" or "on_save_success" or "always");
                // For weapon attacks (melee/ranged), a miss means ALL effects are skipped — no save, no status.
                // Only non-weapon attacks (e.g., Ice Knife: ranged spell with AoE save splash on miss) should
                // still roll the save when hasEffectsNeedingSave is true.
                bool isWeaponAttack = action.AttackType == AttackType.MeleeWeapon
                                   || action.AttackType == AttackType.RangedWeapon;
                bool skipSaveDueToMiss = action.AttackType.HasValue 
                    && context.AttackResult != null 
                    && !context.AttackResult.IsSuccess
                    && (!hasEffectsNeedingSave || isWeaponAttack);
                
                if (!skipSaveDueToMiss)
                {
                int saveDC = (action.SaveDC ?? Rolls.ComputeSaveDC(source, action, effectiveTags)) + action.SaveDCBonus;
                context.SaveDC = saveDC;
                foreach (var target in targets)
                {
                    if (Rolls.ShouldAutoFailSave(target, action.SaveType))
                    {
                        context.SaveResult = new QueryResult
                        {
                            Input = new QueryInput
                            {
                                Type = QueryType.SavingThrow,
                                Source = source,
                                Target = target,
                                DC = saveDC,
                                BaseValue = Rolls.GetSavingThrowBonus(target, action.SaveType)
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
                        BaseValue = Rolls.GetSavingThrowBonus(target, action.SaveType)
                    };
                    saveQuery.Tags.Add($"save:{action.SaveType}");

                    // Propagate spell/magic context for racial advantage checks.
                    if (effectiveTags != null &&
                        (effectiveTags.Contains("isspell") || effectiveTags.Contains("spell") || effectiveTags.Contains("magic")))
                    {
                        saveQuery.Tags.Add("spell");
                    }

                    if (action.School == SpellSchool.Illusion)
                    {
                        saveQuery.Tags.Add("school:illusion");
                    }

                    // Identify conditions this effect would inflict (for racial save advantages).
                    if (effectiveEffects != null)
                    {
                        foreach (var effect in effectiveEffects)
                        {
                            if (!string.Equals(effect.Type, "apply_status", StringComparison.OrdinalIgnoreCase))
                                continue;
                            if (string.IsNullOrWhiteSpace(effect.StatusId))
                                continue;

                            var condType = ConditionEffects.IdentifyConditionType(effect.StatusId);
                            if (condType.HasValue)
                            {
                                saveQuery.Tags.Add($"inflicts:{condType.Value.ToString().ToLowerInvariant()}");
                            }
                        }
                    }

                    var saveAbility = CombatRollResolver.ParseAbilityType(action.SaveType);
                    saveQuery.Parameters["ability"] = saveAbility.HasValue ? saveAbility.Value : action.SaveType;

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
                    CombatRollResolver.ApplyWindowRollSources(saveQuery, beforeSaveContext);

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
                } // end if (!skipSaveDueToMiss)
            }

            // Execute effects - check for multi-projectile
            int handledEffectCount = 0;
            int unhandledEffectCount = 0;
            if (effectiveProjectileCount > 1)
            {
                // Multi-projectile spell: execute each projectile separately
                var multiProjectileResults = ExecuteMultiProjectile(
                    action, 
                    source, 
                    targets, 
                    effectiveEffects, 
                    effectiveTags, 
                    context,
                    effectiveProjectileCount,
                    out handledEffectCount,
                    out unhandledEffectCount);
                result.EffectResults.AddRange(multiProjectileResults);

                // Aggregate per-projectile attack results for downstream logging
                var projAttacks = new List<QueryResult>();
                foreach (var er in multiProjectileResults)
                {
                    if (er.Data.ContainsKey("projectileAttackHit"))
                    {
                        projAttacks.Add(new QueryResult
                        {
                            NaturalRoll = er.Data.TryGetValue("projectileAttackNatural", out var nat) ? (int)nat : 0,
                            FinalValue = er.Data.TryGetValue("projectileAttackTotal", out var tot) ? (int)tot : 0,
                            IsSuccess = er.Data.TryGetValue("projectileAttackHit", out var hit) && (bool)hit,
                            IsCritical = er.Data.TryGetValue("projectileAttackCritical", out var crit) && (bool)crit
                        });
                    }
                }
                if (projAttacks.Count > 0)
                    result.ProjectileAttackResults = projAttacks;
            }
            else
            {
                // Single projectile or non-projectile spell
                foreach (var effectDef in effectiveEffects)
                {
                    if (!_effectHandlers.TryGetValue(effectDef.Type, out var handler))
                    {
                        QDND.Data.RuntimeSafety.LogWarning($"[EffectPipeline] Unknown effect type: {effectDef.Type}");
                        OnEffectUnhandled?.Invoke(effectDef.Type, action.Id);
                        unhandledEffectCount++;
                        continue;
                    }

                    handledEffectCount++;
                    var effectResults = handler.Execute(effectDef, context);
                    result.EffectResults.AddRange(effectResults);
                }
            }

            if (handledEffectCount == 0 && unhandledEffectCount > 0)
            {
                result.Success = false;
                result.ErrorMessage = $"All effects were unhandled for action '{action.Id}'";
            }

            // Handle concentration abilities
            if (context.PerTargetSaveResults.Count > 0)
            {
                result.SaveResultsByTarget = new Dictionary<string, QueryResult>(context.PerTargetSaveResults);
            }

            if (action.RequiresConcentration && Concentration != null)
            {
                string concentrationStatusId = action.ConcentrationStatusId;
                var concentrationTargetIds = targets.Count > 0
                    ? targets.Select(t => t.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList()
                    : new List<string> { source.Id };
                string concentrationTargetId = concentrationTargetIds[0];

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
                    TargetId = concentrationTargetId,
                    TargetIds = concentrationTargetIds
                });
            }

            // Consume cooldown/charges
            Cooldowns?.ConsumeCooldown(source.Id, actionId, action);

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
        /// Execute a multi-projectile spell - fires each projectile separately.
        /// Each projectile can target a different combatant and gets its own attack roll.
        /// Used for spells like Magic Missile (3 darts), Scorching Ray (3 beams), Eldritch Blast (1-4 beams).
        /// </summary>
        private List<EffectResult> ExecuteMultiProjectile(
            ActionDefinition action,
            Combatant source,
            List<Combatant> targets,
            List<EffectDefinition> effectiveEffects,
            HashSet<string> effectiveTags,
            EffectContext baseContext,
            int projectileCount,
            out int handledEffectCount,
            out int unhandledEffectCount)
        {
            var allResults = new List<EffectResult>();
            handledEffectCount = 0;
            unhandledEffectCount = 0;

            // If targets list is shorter than projectile count, we'll cycle through targets
            // (e.g., 3 magic missiles on 1 target still fires 3 times)
            for (int i = 0; i < projectileCount; i++)
            {
                // Get the target for this projectile (cycling through targets if needed)
                var targetForProjectile = targets.Count > 0 
                    ? targets[i % targets.Count] 
                    : null;

                if (targetForProjectile == null)
                    continue;

                // Create a per-projectile context
                var projectileContext = new EffectContext
                {
                    Source = baseContext.Source,
                    Targets = new List<Combatant> { targetForProjectile },
                    TargetPosition = baseContext.TargetPosition,
                    Ability = baseContext.Ability,
                    Rules = baseContext.Rules,
                    Statuses = baseContext.Statuses,
                    Surfaces = baseContext.Surfaces,
                    Heights = baseContext.Heights,
                    ForcedMovement = baseContext.ForcedMovement,
                    Rng = baseContext.Rng,
                    CombatContext = baseContext.CombatContext,
                    InventoryService = baseContext.InventoryService,
                    TurnQueue = baseContext.TurnQueue,
                    DataRegistry = baseContext.DataRegistry,
                    OnHitTriggerService = baseContext.OnHitTriggerService,
                    Pipeline = baseContext.Pipeline,
                    TriggerContext = baseContext.TriggerContext,
                    OnBeforeDamage = baseContext.OnBeforeDamage
                };

                // If this is an attack spell, roll a separate attack for this projectile
                if (action.AttackType.HasValue)
                {
                    bool isSpellAttack = action.AttackType == AttackType.MeleeSpell ||
                                         action.AttackType == AttackType.RangedSpell ||
                                         effectiveTags.Contains("spell");
                    bool isMeleeAttack = action.AttackType == AttackType.MeleeWeapon ||
                                         action.AttackType == AttackType.MeleeSpell;
                    bool isRangedAttack = action.AttackType == AttackType.RangedWeapon ||
                                          action.AttackType == AttackType.RangedSpell;

                    int heightMod = 0;
                    if (Heights != null)
                    {
                        heightMod = Heights.GetAttackModifier(source, targetForProjectile);
                    }

                    int coverACBonus = 0;
                    LOSResult losResult = null;
                    if (LOS != null)
                    {
                        losResult = LOS.CheckLOS(source, targetForProjectile);
                        coverACBonus = losResult.GetACBonus();
                    }

                    if (isRangedAttack && losResult?.RangedAttacksBlocked == true)
                    {
                        continue;
                    }

                    var attackQuery = new QueryInput
                    {
                        Type = QueryType.AttackRoll,
                        Source = source,
                        Target = targetForProjectile,
                        BaseValue = Rolls.GetAttackRollBonus(source, action, effectiveTags) + heightMod
                    };

                    var attackTags = new HashSet<string>(effectiveTags);
                    if (isMeleeAttack) attackTags.Add("melee_attack");
                    if (isRangedAttack) attackTags.Add("ranged_attack");
                    if (isSpellAttack) attackTags.Add("spell_attack");
                    if (isMeleeAttack && source.MainHandWeapon?.IsTwoHanded == true) attackTags.Add("weapon:two_handed");
                    if (isMeleeAttack && source.MainHandWeapon?.IsVersatile == true) attackTags.Add("weapon:versatile");
                    attackTags.ToList().ForEach(t => attackQuery.Tags.Add(t));

                    var condAdvantages = new List<string>();
                    var condDisadvantages = new List<string>();
                    bool autoCritOnHit = false;
                    if (Statuses != null)
                    {
                        float attackDistance = source.Position.DistanceTo(targetForProjectile.Position);
                        var srcIds = Statuses.GetStatuses(source.Id).SelectMany(s => new[] { s.Definition.Id }.Concat(s.Definition.Tags ?? Enumerable.Empty<string>()));
                        var tgtIds = Statuses.GetStatuses(targetForProjectile.Id).SelectMany(s => new[] { s.Definition.Id }.Concat(s.Definition.Tags ?? Enumerable.Empty<string>()));
                        var srcEffects = ConditionEffects.GetAggregateEffects(srcIds, isMeleeAttack);
                        var tgtEffects = ConditionEffects.GetAggregateEffects(tgtIds, isMeleeAttack);
                        condAdvantages.AddRange(srcEffects.AttackAdvantageSources.Select(id => $"Attacker {id}"));
                        condDisadvantages.AddRange(srcEffects.AttackDisadvantageSources.Select(id => $"Attacker {id}"));
                        condAdvantages.AddRange(tgtEffects.DefenseAdvantageSources.Select(id => $"Target {id}"));
                        condDisadvantages.AddRange(tgtEffects.DefenseDisadvantageSources.Select(id => $"Target {id}"));
                        if (CombatRollResolver.ShouldApplyMeleeAutoCrit(tgtEffects.MeleeAutocrits, isMeleeAttack, attackDistance))
                            autoCritOnHit = true;
                        if (Statuses.HasStatus(targetForProjectile.Id, "dodging"))
                            condDisadvantages.Add("Target Dodging");
                        if (Statuses.HasStatus(source.Id, "reckless"))
                            condAdvantages.Add("Reckless Attack");
                        if (Statuses.HasStatus(targetForProjectile.Id, "reckless"))
                            condAdvantages.Add("Target Reckless");
                        if (Statuses.HasStatus(source.Id, "hidden"))
                            condAdvantages.Add("Hidden Attacker");
                        if (Statuses.HasStatus(source.Id, "helped"))
                            condAdvantages.Add("Helped");
                        if (Statuses.HasStatus(targetForProjectile.Id, "hidden"))
                            condDisadvantages.Add("Target Hidden");
                        if (isRangedAttack)
                        {
                            // Crossbow Expert: removes ranged disadvantage in melee, but ONLY for crossbow attacks
                            bool hasCrossbowExpert = source.ResolvedCharacter?.Sheet?.FeatIds?.Contains("crossbow_expert") == true;
                            bool isAttackingWithCrossbow = source.MainHandWeapon?.WeaponType is
                                WeaponType.LightCrossbow or WeaponType.HandCrossbow or WeaponType.HeavyCrossbow;
                            if (!(hasCrossbowExpert && isAttackingWithCrossbow) &&
                                (Statuses.HasStatus(source.Id, "threatened") ||
                                 (GetCombatants != null && Rolls.IsWithinHostileMeleeRange(source))))
                                condDisadvantages.Add("Threatened");
                        }

                        if (losResult != null)
                        {
                            if (losResult.SourceObscurity == ObscurityTier.HeavilyObscured)
                                condDisadvantages.Add("Heavily Obscured (Source)");
                            if (losResult.TargetObscurity == ObscurityTier.HeavilyObscured)
                                condDisadvantages.Add("Heavily Obscured (Target)");
                            if (losResult.LineObscurity == ObscurityTier.HeavilyObscured)
                                condDisadvantages.Add("Heavily Obscured (Between)");
                        }
                    }
                    if (condAdvantages.Count > 0)
                        attackQuery.Parameters["statusAdvantageSources"] = condAdvantages;
                    if (condDisadvantages.Count > 0)
                        attackQuery.Parameters["statusDisadvantageSources"] = condDisadvantages;
                    if (autoCritOnHit)
                        attackQuery.Parameters["autoCritOnHit"] = true;

                    attackQuery.Parameters["criticalThreshold"] = CombatRollResolver.GetCriticalThreshold(source, isSpellAttack);

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
                        Target = targetForProjectile,
                        Ability = action,
                        QueryInput = attackQuery,
                        Random = projectileContext.Rng,
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
                        // This projectile was cancelled, skip to next
                        continue;
                    }

                    CombatRollResolver.ApplyWindowRollSources(attackQuery, beforeAttackContext);

                    projectileContext.AttackResult = Rules.RollAttack(attackQuery);

                    Rules?.RuleWindows.Dispatch(RuleWindow.AfterAttackRoll, new RuleEventContext
                    {
                        Source = source,
                        Target = targetForProjectile,
                        Ability = action,
                        QueryInput = attackQuery,
                        QueryResult = projectileContext.AttackResult,
                        Random = projectileContext.Rng,
                        IsMeleeWeaponAttack = action.AttackType == AttackType.MeleeWeapon,
                        IsRangedWeaponAttack = action.AttackType == AttackType.RangedWeapon,
                        IsSpellAttack = isSpellAttack,
                        IsCriticalHit = projectileContext.AttackResult?.IsCritical == true
                    });

                    // === Fire YouAreAttacked reactions per projectile ===
                    if (projectileContext.AttackResult != null)
                    {
                        var attackReactions = ReactionTriggers.TryTriggerAttackReactions(source, targetForProjectile, action,
                            action.AttackType?.ToString(), projectileContext.AttackResult?.IsSuccess ?? true);
                        if (attackReactions != null)
                        {
                            int acMod = attackReactions.ACModifier;
                            int rollMod = attackReactions.RollModifier;
                            if ((acMod != 0 || rollMod != 0) && projectileContext.AttackResult.IsSuccess && !projectileContext.AttackResult.IsCritical && Rules != null)
                            {
                                float effectiveTotal = projectileContext.AttackResult.FinalValue + rollMod;
                                float targetAC = Rules.GetArmorClass(targetForProjectile) + acMod;
                                if (effectiveTotal < targetAC)
                                {
                                    projectileContext.AttackResult.IsSuccess = false;
                                }
                            }
                        }
                    }

                    // === Fire YouAreHit reactions per projectile ===
                    if (projectileContext.AttackResult?.IsSuccess == true)
                    {
                        var hitReactions = ReactionTriggers.TryTriggerHitReactions(source, targetForProjectile, 0,
                            action.Effects?.FirstOrDefault(e => e.Type == "deal_damage")?.DamageType ?? "untyped",
                            action.AttackType?.ToString(),
                            projectileContext.AttackResult.IsCritical, action.Id);
                        if (hitReactions != null)
                        {
                            projectileContext.HitDamageModifier = hitReactions.DamageModifier;
                        }
                    }
                }
                else if (effectiveTags.Any(tag => string.Equals(tag, "auto_hit", StringComparison.OrdinalIgnoreCase)))
                {
                    // Auto-hit projectiles do not re-check hit chance, but still need the
                    // YouAreAttacked reaction window so Shield can apply before damage.
                    ReactionTriggers.TryTriggerAttackReactions(source, targetForProjectile, action, "auto_hit", attackHit: true);
                }
                // Note: Multi-projectile spells with saves (rare) would roll saves here
                // For now, we assume multi-projectile = attack-based or auto-hit (Magic Missile)

                // Execute effects for this projectile
                foreach (var effectDef in effectiveEffects)
                {
                    if (!_effectHandlers.TryGetValue(effectDef.Type, out var handler))
                    {
                        QDND.Data.RuntimeSafety.LogWarning($"[EffectPipeline] Unknown effect type: {effectDef.Type}");
                        OnEffectUnhandled?.Invoke(effectDef.Type, action.Id);
                        unhandledEffectCount++;
                        continue;
                    }

                    handledEffectCount++;
                    var effectResults = handler.Execute(effectDef, projectileContext);

                    // Attach per-projectile attack result to each effect result for logging
                    foreach (var er in effectResults)
                    {
                        er.Data["projectileIndex"] = i;

                        if (projectileContext.AttackResult != null)
                        {
                            er.Data["projectileAttackNatural"] = projectileContext.AttackResult.NaturalRoll;
                            er.Data["projectileAttackTotal"] = (int)projectileContext.AttackResult.FinalValue;
                            er.Data["projectileAttackHit"] = projectileContext.AttackResult.IsSuccess;
                            er.Data["projectileAttackCritical"] = projectileContext.AttackResult.IsCritical;
                        }
                    }

                    allResults.AddRange(effectResults);
                }
            }

            return allResults;
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
            Cooldowns?.ProcessTurnStart(combatantId);
        }

        /// <summary>
        /// Process round end (tick round-based cooldowns).
        /// </summary>
        public void ProcessRoundEnd()
        {
            Cooldowns?.ProcessRoundEnd();
        }

        public ReactionTriggerEventArgs TryTriggerDamageReactions(
            Combatant source,
            Combatant target,
            int damageAmount,
            string damageType,
            string actionId = null)
            => ReactionTriggers.TryTriggerDamageReactions(source, target, damageAmount, damageType, actionId);

        public void TryTriggerAllyDownedReactions(Combatant killer, Combatant downed)
            => ReactionTriggers.TryTriggerAllyDownedReactions(killer, downed);

        /// <summary>
        /// Search ActionRegistry for a BG3-parsed action whose normalized ID matches the given game ID.
        /// BG3 IDs use PascalCase with prefixes (e.g., "Target_HuntersMark") while game IDs use
        /// snake_case (e.g., "hunters_mark").
        /// </summary>
        private ActionDefinition FindMatchingBg3Action(string gameId)
        {
            if (ActionRegistry == null || string.IsNullOrEmpty(gameId))
                return null;

            foreach (var bg3Action in ActionRegistry.GetAllActions())
            {
                if (string.Equals(bg3Action.Id, gameId, StringComparison.OrdinalIgnoreCase))
                    return bg3Action;

                var normalized = NormalizeBg3IdToGameId(bg3Action.Id);
                if (string.Equals(normalized, gameId, StringComparison.OrdinalIgnoreCase))
                    return bg3Action;
            }

            return null;
        }

        /// <summary>
        /// Normalize a BG3 ID (e.g., "Target_HuntersMark") to a game ID (e.g., "hunters_mark")
        /// by stripping known prefixes and converting PascalCase to snake_case.
        /// </summary>
        private static string NormalizeBg3IdToGameId(string bg3Id)
        {
            if (string.IsNullOrEmpty(bg3Id))
                return null;

            // Strip known BG3 prefixes
            string stripped = bg3Id;
            foreach (var prefix in new[] { "Target_", "Projectile_", "Shout_", "Zone_" })
            {
                if (stripped.StartsWith(prefix, StringComparison.Ordinal))
                {
                    stripped = stripped.Substring(prefix.Length);
                    break;
                }
            }

            // Convert PascalCase to snake_case
            var sb = new StringBuilder();
            for (int i = 0; i < stripped.Length; i++)
            {
                char c = stripped[i];
                if (char.IsUpper(c) && i > 0)
                {
                    char prev = stripped[i - 1];
                    if (char.IsLower(prev) || char.IsDigit(prev))
                        sb.Append('_');
                }
                sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Reset for new combat.
        /// </summary>
        public void Reset()
        {
            Cooldowns?.Reset();
        }

        /// <summary>
        /// Export all cooldown states.
        /// </summary>
        public List<Persistence.CooldownSnapshot> ExportCooldowns()
        {
            return Cooldowns?.ExportCooldowns() ?? new List<Persistence.CooldownSnapshot>();
        }

        /// <summary>
        /// Import cooldown states from snapshots.
        /// </summary>
        public void ImportCooldowns(List<Persistence.CooldownSnapshot> snapshots)
        {
            Cooldowns?.ImportCooldowns(snapshots);
        }
    }
}
