using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Movement;
using QDND.Combat.Reactions;
using QDND.Combat.Rules;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Combat.Statuses;
using QDND.Combat.Targeting;
using QDND.Combat.UI;
using QDND.Combat.UI.Base;
using QDND.Combat.VFX;
using QDND.Data;
using QDND.Data.AI;
using QDND.Data.CharacterModel;
using QDND.Data.Icons;
using QDND.Tools.AutoBattler;

namespace QDND.Combat.Arena
{
    internal static class CombatArenaComposer
    {
        internal static CombatArenaComposition Compose(CombatArenaCompositionArgs args)
        {
            var composition = new CombatArenaComposition();
            composition.CombatantRegistry = args.CombatContext.Combatants;

            composition.StateMachine = new CombatStateMachine();
            composition.TurnQueue = new TurnQueueService();
            composition.CommandService = new CommandService();
            composition.CombatLog = new CombatLog();
            composition.ScenarioLoader = new ScenarioLoader();

            composition.CommandService.StateMachine = composition.StateMachine;
            composition.CommandService.TurnQueue = composition.TurnQueue;

            composition.StateMachine.OnStateChanged += args.HandleStateChanged;
            composition.TurnQueue.OnTurnChanged += args.HandleTurnChanged;
            composition.CommandService.OnCommandExecuted += args.HandleCommandExecuted;

            args.CombatContext.RegisterService(composition.StateMachine);
            args.CombatContext.RegisterService(composition.TurnQueue);
            args.CombatContext.RegisterService(composition.CommandService);
            args.CombatContext.RegisterService(composition.CombatLog);
            args.CombatContext.RegisterService(composition.ScenarioLoader);
            args.CombatContext.RegisterService<ICombatantRegistry>(composition.CombatantRegistry);

            var registries = RegistryInitializer.Bootstrap(
                dataPath: ProjectSettings.GlobalizePath("res://Data"),
                bg3DataPath: ProjectSettings.GlobalizePath("res://BG3_Data"),
                verboseLogging: args.VerboseLogging,
                scenarioLoader: composition.ScenarioLoader,
                combatContext: args.CombatContext,
                log: args.Log,
                logError: args.LogError,
                resolveCombatant: args.ResolveCombatant,
                getAllCombatantIds: () => composition.CombatantRegistry.GetAll().Select(c => c.Id),
                removeSurfacesByCreator: creatorId => composition.SurfaceManager?.RemoveSurfacesByCreator(creatorId),
                removeSurfaceById: instanceId => composition.SurfaceManager?.RemoveSurfaceById(instanceId),
                removeSummonsByOwner: ownerId =>
                {
                    var summons = args.CombatContext.GetAllCombatants()
                        .Where(c => c.OwnerId == ownerId && c.Id != ownerId)
                        .ToList();
                    foreach (var summon in summons)
                    {
                        summon.LifeState = CombatantLifeState.Dead;
                        summon.Resources.CurrentHP = 0;
                        composition.TurnQueue.RemoveCombatant(summon.Id);
                        composition.RulesEngine?.Events.Dispatch(new RuleEvent
                        {
                            Type = RuleEventType.CombatantDied,
                            SourceId = ownerId,
                            TargetId = summon.Id,
                            Data = new Dictionary<string, object> { { "cause", "concentration_broken" } }
                        });
                        args.Log($"[ConcentrationSystem] Unsummoned {summon.Name} (concentration broken)");
                    }
                });

            composition.DataRegistry = registries.DataRegistry;
            composition.RulesEngine = registries.RulesEngine;
            composition.StatusManager = registries.StatusManager;
            args.SpecialMovementService.SetStatusQuery(
                (combatantId, statusId) => composition.StatusManager?.HasStatus(combatantId, statusId) == true);
            composition.MetamagicService = registries.MetamagicService;
            composition.ConcentrationSystem = registries.ConcentrationSystem;
            composition.EffectPipeline = registries.EffectPipeline;
            composition.ActionRegistry = registries.ActionRegistry;
            composition.StatsRegistry = registries.StatsRegistry;
            composition.BG3StatusRegistry = registries.BG3StatusRegistry;
            composition.BG3StatusIntegration = registries.BG3StatusIntegration;
            composition.PassiveRegistry = registries.PassiveRegistry;
            composition.InterruptRegistry = registries.InterruptRegistry;
            composition.FunctorExecutor = registries.FunctorExecutor;
            HudIcons.SetIconService(registries.IconService);
            var charRegistry = registries.CharRegistry;
            composition.CharacterDataRegistry = charRegistry;

            composition.BG3AiRegistry = new BG3AIRegistry();
            var bg3AiPath = Path.Combine(ProjectSettings.GlobalizePath("res://BG3_Data"), "AI");
            if (composition.BG3AiRegistry.LoadFromDirectory(bg3AiPath))
            {
                args.Log($"BG3 AI Registry: {composition.BG3AiRegistry.Archetypes.Count} archetypes, {composition.BG3AiRegistry.SurfaceCombos.Count} combos loaded");
            }
            else
            {
                args.Log($"BG3 AI Registry load completed with {composition.BG3AiRegistry.Errors.Count} errors");
            }

            foreach (var warning in composition.BG3AiRegistry.Warnings.Take(10))
            {
                GD.PushWarning($"[BG3AI] {warning}");
            }

            foreach (var error in composition.BG3AiRegistry.Errors.Take(10))
            {
                GD.PushError($"[BG3AI] {error}");
            }

            args.CombatContext.RegisterService(composition.BG3AiRegistry);

            var reactionAliasResolver = new ReactionAliasResolver();
            composition.ReactionSystem = new ReactionSystem(composition.RulesEngine.Events, reactionAliasResolver)
            {
                StrictGrantValidation = true
            };
            composition.ReactionSystem.AdditionalEligibilityCheck = (reactor, reaction, context) =>
            {
                if (composition.EffectPipeline == null || reactor == null || string.IsNullOrWhiteSpace(reaction?.ActionId))
                {
                    return true;
                }

                if (reaction.Tags != null && reaction.Tags.Contains("opportunity_attack"))
                {
                    return true;
                }

                var (canUse, _) = composition.EffectPipeline.CanUseAbility(reaction.ActionId, reactor);
                return canUse;
            };

            var bg3ReactionIntegration = new BG3ReactionIntegration(composition.ReactionSystem, composition.InterruptRegistry);
            bg3ReactionIntegration.RegisterCoreInterrupts();
            args.CombatContext.RegisterService(bg3ReactionIntegration);
            args.Log("BG3 Reaction Integration wired (OpportunityAttack, Shield, Counterspell, UncannyDodge)");

            composition.ResolutionStack = new ResolutionStack();
            composition.ReactionCoordinator = new ReactionCoordinator(
                composition.ReactionSystem,
                (prompt, cb) => args.ReactionPromptUI.Show(prompt, cb),
                composition.StateMachine,
                composition.EffectPipeline,
                args.CombatContext,
                composition.TargetValidator,
                composition.TurnQueue,
                composition.CombatantRegistry,
                bg3ReactionIntegration,
                composition.CombatLog,
                args.IsAutoBattleMode,
                args.GetRandom,
                args.Log);

            composition.ReactionResolver = new ReactionResolver(composition.ReactionSystem, composition.ResolutionStack, seed: 42)
            {
                GetCombatants = () => composition.CombatantRegistry.GetAll(),
                PromptDecisionProvider = composition.ReactionCoordinator.ResolveSynchronousReactionPromptDecision,
                AIDecisionProvider = composition.ReactionCoordinator.DecideAIReaction
            };

            composition.ReactionCoordinator.SetReactionResolver(composition.ReactionResolver);
            composition.ReactionCoordinator.SetShowSlotPicker((prompt, callback) =>
            {
                var reactor = args.CombatContext.GetCombatant(prompt.ReactorId);
                var pool = reactor?.ActionResources;
                var slots = ReactionSystem.GetAvailableSpellSlots(pool, 3);
                var hud = args.HudLayer?.GetNodeOrNull<HudController>("HudController");
                if (hud != null)
                {
                    hud.ShowSpellSlotPicker("Counterspell", slots, callback);
                }
                else
                {
                    callback?.Invoke(-1);
                }
            });

            composition.ReactionSystem.OnPromptCreated += composition.ReactionCoordinator.OnReactionPrompt;
            composition.ReactionSystem.OnReactionUsed += composition.ReactionCoordinator.OnReactionUsed;

            composition.EffectPipeline.Reactions = composition.ReactionSystem;
            composition.EffectPipeline.ReactionResolver = composition.ReactionResolver;
            composition.EffectPipeline.GetCombatants = () => composition.CombatantRegistry.GetAll();
            composition.EffectPipeline.CombatContext = args.CombatContext;
            composition.EffectPipeline.TurnQueue = composition.TurnQueue;
            composition.EffectPipeline.DataRegistry = composition.DataRegistry;
            composition.EffectPipeline.CharacterDataRegistry = charRegistry;

            composition.SurfaceManager = new SurfaceManager(composition.RulesEngine.Events, composition.StatusManager);
            composition.SurfaceManager.Rules = composition.RulesEngine;
            composition.SurfaceManager.OnSurfaceCreated += args.HandleSurfaceCreated;
            composition.SurfaceManager.OnSurfaceRemoved += args.HandleSurfaceRemoved;
            composition.SurfaceManager.OnSurfaceTransformed += args.HandleSurfaceTransformed;
            composition.SurfaceManager.OnSurfaceTriggered += args.HandleSurfaceTriggered;
            composition.SurfaceManager.OnSurfaceGeometryChanged += args.HandleSurfaceGeometryChanged;
            composition.SurfaceManager.ResolveCombatants = () => composition.CombatantRegistry.GetAll();
            args.CombatContext.RegisterService(composition.SurfaceManager);

            var losService = new LOSService();
            losService.SetSurfaceManager(composition.SurfaceManager);
            var heightService = new HeightService(composition.RulesEngine.Events);

            composition.ForcedMovementService = new ForcedMovementService(
                events: composition.RulesEngine.Events,
                surfaces: composition.SurfaceManager,
                height: heightService);

            composition.EffectPipeline.LOS = losService;
            composition.EffectPipeline.Heights = heightService;
            composition.EffectPipeline.ForcedMovement = composition.ForcedMovementService;

            composition.TargetValidator = new TargetValidator(losService, c => c.Position);
            composition.TargetValidator.Statuses = composition.StatusManager;
            composition.TargetValidator.ConditionEval = QDND.Combat.Rules.Conditions.ConditionEvaluator.Instance;
            composition.ReactionCoordinator.SetTargetValidator(composition.TargetValidator);

            composition.StatusManager.OnStatusApplied += args.HandleStatusApplied;
            composition.StatusManager.OnStatusRemoved += args.HandleStatusRemoved;
            composition.StatusManager.OnStatusTick += args.HandleStatusTick;

            composition.StatusTickProcessor = new StatusTickProcessor(composition.RulesEngine, composition.CombatLog, composition.StatusManager)
            {
                Log = args.Log,
                OnShowDamage = (id, amount, damageType) =>
                {
                    if (args.CombatantVisuals.TryGetValue(id, out var visual))
                    {
                        visual.ShowDamage(amount, damageType: damageType);
                    }
                },
                OnShowHealing = (id, amount) =>
                {
                    if (args.CombatantVisuals.TryGetValue(id, out var visual))
                    {
                        visual.ShowHealing(amount);
                    }
                },
                ResolveCombatant = args.ResolveCombatant
            };

            args.CombatContext.RegisterService(composition.DataRegistry);
            args.CombatContext.RegisterService(composition.RulesEngine);
            args.CombatContext.RegisterService(composition.StatusManager);
            args.CombatContext.RegisterService(composition.ConcentrationSystem);

            Action<string, ConcentrationInfo> concentrationStarted = (combatantId, _) => args.SetConcentratingVisual(combatantId, true);
            Action<string, ConcentrationInfo, string> concentrationBroken = (combatantId, _, _) => args.SetConcentratingVisual(combatantId, false);
            composition.ConcentrationSystem.OnConcentrationStarted += concentrationStarted;
            composition.ConcentrationSystem.OnConcentrationBroken += concentrationBroken;

            composition.ResourceManager = new ResourceManager();
            args.CombatContext.RegisterService(composition.ResourceManager);

            composition.RestService = new RestService(composition.ResourceManager);
            args.CombatContext.RegisterService(composition.RestService);

            var inventoryService = new InventoryService(charRegistry, composition.StatsRegistry, args.CombatContext, registries.ItemDefinitionRegistry);
            args.CombatContext.RegisterService(inventoryService);
            composition.InventoryService = inventoryService;
            composition.EffectPipeline.InventoryService = inventoryService;
            Action<string, EquipSlot> onEquipmentChanged = (combatantId, _) =>
            {
                if (string.Equals(combatantId, args.GetActiveCombatantId(), StringComparison.Ordinal))
                {
                    composition.ActionBarService?.Populate(combatantId);
                }
            };
            Action<string> onInventoryChanged = combatantId =>
            {
                if (string.Equals(combatantId, args.GetActiveCombatantId(), StringComparison.Ordinal))
                {
                    composition.ActionBarService?.Populate(combatantId);
                }
            };
            inventoryService.OnEquipmentChanged += onEquipmentChanged;
            inventoryService.OnInventoryChanged += onInventoryChanged;

            if (composition.FunctorExecutor != null)
            {
                composition.FunctorExecutor.EffectPipeline = composition.EffectPipeline;
                composition.FunctorExecutor.SurfaceManager = composition.SurfaceManager;
                composition.FunctorExecutor.ForcedMovement = composition.ForcedMovementService;
                composition.FunctorExecutor.InventoryService = inventoryService;
                composition.FunctorExecutor.BreakConcentrationAction = (combatantId, reason) =>
                    composition.ConcentrationSystem?.BreakConcentration(combatantId, reason);
                composition.FunctorExecutor.CounterspellAction = (sourceId, targetId) =>
                    composition.ConcentrationSystem?.BreakConcentration(targetId, "Counterspell");
            }

            composition.StatusManager.ResolveCombatant = args.ResolveCombatant;
            composition.StatusInteractionRules = new StatusInteractionRules(composition.StatusManager, args.ResolveCombatant);

            args.CombatContext.RegisterService(composition.EffectPipeline);
            args.CombatContext.RegisterService(composition.TargetValidator);
            args.CombatContext.RegisterService(composition.ResolutionStack);
            args.CombatContext.RegisterService(composition.ReactionSystem);
            args.CombatContext.RegisterService<IReactionResolver>(composition.ReactionResolver);
            args.CombatContext.RegisterService(losService);
            args.CombatContext.RegisterService(heightService);

            composition.MovementService = new MovementService(
                composition.RulesEngine.Events,
                composition.SurfaceManager,
                composition.ReactionSystem,
                composition.StatusManager);
            composition.MovementService.GetCombatants = () => composition.CombatantRegistry.GetAll();
            composition.MovementService.ResolveCombatant = args.ResolveCombatant;
            composition.MovementService.ReactionResolver = composition.ReactionResolver;
            composition.MovementService.PathNodeSpacing = 0.75f;
            composition.MovementService.IsWorldPositionBlocked = args.IsWorldPositionBlocked;
            args.CombatContext.RegisterService(composition.MovementService);

            composition.AIPipeline = new AIDecisionPipeline(
                composition.CombatantRegistry,
                composition.RulesEngine,
                composition.EffectPipeline,
                composition.TargetValidator,
                losService,
                composition.SurfaceManager,
                composition.MovementService,
                composition.DataRegistry,
                composition.StatusManager,
                composition.ReactionSystem,
                inventoryService,
                composition.ConcentrationSystem,
                composition.TurnQueue,
                heightService,
                null,
                args.SpecialMovementService,
                composition.ForcedMovementService,
                charRegistry,
                composition.BG3StatusRegistry);
            args.CombatContext.RegisterService(composition.AIPipeline);

            composition.MovementCoordinator = new CombatMovementCoordinator(
                composition.MovementService,
                args.MovementPreview,
                args.RangeIndicator,
                args.InputHandler,
                args.CombatContext,
                composition.CombatantRegistry,
                args.CombatantVisuals,
                composition.StatusManager,
                composition.CombatLog,
                composition.StateMachine,
                args.CameraService,
                args.TileSize,
                args.DefaultMovePoints,
                args.GetSelectedCombatantId,
                args.GetIsPlayerTurn,
                args.GetActiveCombatantId,
                args.GetAutoBattleConfig,
                () => composition.ActionExecutionService.AllocateActionId(),
                args.CanPlayerControl,
                args.RefreshActionBarUsability,
                combatant => composition.TurnLifecycleService?.UpdateResourceModelFromCombatant(combatant),
                args.ResumeDecisionStateIfExecuting,
                args.DispatchThreatenedStatusesSync,
                args.DispatchRuleWindow,
                args.CreateDoubleTimer,
                args.Log);

            composition.EffectPipeline.Surfaces = composition.SurfaceManager;

            args.CombatContext.RegisterService(args.CameraService.CameraHooks);

            composition.ActionBarModel = new ActionBarModel();
            composition.ActionBarService = new ActionBarService(
                composition.CombatantRegistry,
                composition.ActionRegistry,
                composition.ActionBarModel,
                composition.PassiveRegistry,
                composition.EffectPipeline,
                inventoryService,
                composition.ConcentrationSystem,
                charRegistry,
                args.LogOnce);

            composition.SelectionService = new SelectionService(
                args.CombatContext,
                composition.EffectPipeline,
                composition.PassiveRegistry,
                args.CanPlayerControl,
                args.Log,
                args.RefreshActionBarUsability,
                args.PopulateActionBar,
                id => composition.ActionBarModel.SelectAction(id),
                () => composition.ActionBarModel.ClearSelection());

            composition.TurnTrackerModel = new TurnTrackerModel();
            composition.ResourceBarModel = new ResourceBarModel();

            composition.PresentationService = new CombatPresentationService(
                args.CombatantVisuals,
                args.PendingJumpWorldPaths,
                composition.TurnQueue,
                args.CameraService,
                composition.TurnTrackerModel,
                args.TileSize);
            args.CombatContext.RegisterService(composition.PresentationService.PresentationBus);

            var vfxConfig = VfxConfigLoader.LoadDefault();
            args.VfxManager.ConfigureRuntimeCaps(vfxConfig.ActiveCap, vfxConfig.InitialPoolSize);
            var vfxResolver = new VfxRuleResolver(vfxConfig);
            composition.VfxPlaybackService = new VfxPlaybackService(
                composition.PresentationService.PresentationBus,
                args.VfxManager,
                args.CombatContext,
                args.TileSize,
                vfxResolver);
            args.CombatContext.RegisterService<IVfxRuleResolver>(vfxResolver);
            args.CombatContext.RegisterService<IVfxPlaybackService>(composition.VfxPlaybackService);

            composition.PresentationService.SetPreviewDependencies(
                args.CombatContext,
                composition.EffectPipeline,
                composition.RulesEngine,
                composition.TargetValidator);

            var auraSystem = new AuraSystem(
                composition.StatusManager,
                () => composition.CombatantRegistry.GetAll(),
                id => args.CombatContext.GetCombatant(id));
            args.CombatContext.RegisterService(auraSystem);

            composition.TurnLifecycleService = new TurnLifecycleService(
                composition.TurnQueue,
                composition.StateMachine,
                composition.EffectPipeline,
                composition.StatusManager,
                composition.SurfaceManager,
                composition.RulesEngine,
                composition.ResourceManager,
                composition.PresentationService,
                composition.CombatLog,
                composition.ActionBarModel,
                composition.TurnTrackerModel,
                composition.ResourceBarModel,
                args.CombatantVisuals,
                args.DefaultMovePoints,
                args.ArenaScene,
                args.ArenaScene,
                args.ArenaScene,
                args.ArenaScene,
                args.ArenaScene,
                args.Log,
                auraSystem);
            composition.TurnLifecycleService.AllowVictoryHook = args.ShouldAllowVictory;

            composition.ActionExecutionService = new ActionExecutionService(
                composition.EffectPipeline,
                args.CombatContext,
                composition.StateMachine,
                composition.TurnQueue,
                composition.TargetValidator,
                composition.ActionBarModel,
                composition.ResourceBarModel,
                composition.PresentationService,
                composition.SurfaceManager,
                composition.StatusManager,
                composition.RulesEngine,
                composition.CombatLog,
                composition.CombatantRegistry,
                inventoryService,
                args.PendingJumpWorldPaths,
                args.TileSize,
                args.FaceCombatantTowardsGridPoint,
                args.ArenaScene,
                args.ArenaScene,
                args.ArenaScene,
                args.ArenaScene,
                args.Log);
            composition.EffectPipeline.OnAbilityExecuted += composition.ActionExecutionService.OnAbilityExecuted;
            composition.ActionExecutionService.OnAIAbilityNotify = args.NotifyAIAbilityUsed;

            var bootConfig = new ScenarioBootConfig
            {
                ScenarioSeedOverride = args.ScenarioSeedOverride,
                AutoBattleSeedOverride = args.AutoBattleSeedOverride,
                RandomSeed = args.RandomSeed,
                ResolvedScenarioSeed = args.ResolvedScenarioSeed,
                DynamicMode = args.DynamicScenarioMode,
                DynamicActionTestId = args.DynamicActionTestId,
                DynamicActionBatchIds = args.DynamicActionBatchIds,
                DynamicCharacterLevel = args.DynamicCharacterLevel,
                DynamicTeamSize = args.DynamicTeamSize,
                AutoBattleConfig = args.AutoBattleConfig,
            };
            var bootVisuals = new ScenarioBootVisuals
            {
                Arena = args.ArenaScene,
                CombatantsContainer = args.CombatantsContainer,
                CombatantVisuals = args.CombatantVisuals,
                TileSize = args.TileSize,
            };
            composition.ScenarioBootService = new ScenarioBootService(
                args.CombatContext,
                composition.FunctorExecutor,
                composition.ForcedMovementService,
                charRegistry,
                composition.ActionRegistry,
                composition.EffectPipeline,
                composition.AIPipeline,
                composition.ScenarioLoader,
                composition.TurnQueue,
                composition.PassiveRegistry,
                composition.MetamagicService,
                composition.StatusManager,
                composition.CombatLog,
                inventoryService,
                losService,
                composition.MovementCoordinator.ApplyDefaultMovementToCombatants,
                composition.ReactionCoordinator.GrantBaselineReactions,
                args.Log,
                args.OneTimeLogKeys,
                bootConfig,
                bootVisuals);

            composition.DisposeAction = () =>
            {
                composition.StateMachine.OnStateChanged -= args.HandleStateChanged;
                composition.TurnQueue.OnTurnChanged -= args.HandleTurnChanged;
                composition.CommandService.OnCommandExecuted -= args.HandleCommandExecuted;

                composition.ReactionSystem.OnPromptCreated -= composition.ReactionCoordinator.OnReactionPrompt;
                composition.ReactionSystem.OnReactionUsed -= composition.ReactionCoordinator.OnReactionUsed;

                composition.SurfaceManager.OnSurfaceCreated -= args.HandleSurfaceCreated;
                composition.SurfaceManager.OnSurfaceRemoved -= args.HandleSurfaceRemoved;
                composition.SurfaceManager.OnSurfaceTransformed -= args.HandleSurfaceTransformed;
                composition.SurfaceManager.OnSurfaceTriggered -= args.HandleSurfaceTriggered;
                composition.SurfaceManager.OnSurfaceGeometryChanged -= args.HandleSurfaceGeometryChanged;

                composition.StatusManager.OnStatusApplied -= args.HandleStatusApplied;
                composition.StatusManager.OnStatusRemoved -= args.HandleStatusRemoved;
                composition.StatusManager.OnStatusTick -= args.HandleStatusTick;

                composition.ConcentrationSystem.OnConcentrationStarted -= concentrationStarted;
                composition.ConcentrationSystem.OnConcentrationBroken -= concentrationBroken;

                inventoryService.OnEquipmentChanged -= onEquipmentChanged;
                inventoryService.OnInventoryChanged -= onInventoryChanged;

                composition.EffectPipeline.OnAbilityExecuted -= composition.ActionExecutionService.OnAbilityExecuted;
            };

            return composition;
        }
    }
}
