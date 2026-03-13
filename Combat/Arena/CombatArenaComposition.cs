using System;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.AI;
using QDND.Combat.Environment;
using QDND.Combat.Movement;
using QDND.Combat.Reactions;
using QDND.Combat.Rules;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Combat.Statuses;
using QDND.Combat.Targeting;
using QDND.Combat.UI;
using QDND.Data;
using QDND.Data.AI;
using QDND.Data.CharacterModel;
using QDND.Data.Interrupts;
using QDND.Data.Passives;
using QDND.Data.Stats;
using QDND.Data.Statuses;

namespace QDND.Combat.Arena
{
    internal sealed class CombatArenaComposition : IDisposable
    {
        internal CombatStateMachine StateMachine;
        internal TurnQueueService TurnQueue;
        internal CommandService CommandService;
        internal CombatLog CombatLog;
        internal ScenarioLoader ScenarioLoader;
        internal DataRegistry DataRegistry;
        internal CharacterDataRegistry CharacterDataRegistry;
        internal RulesEngine RulesEngine;
        internal StatusManager StatusManager;
        internal StatusTickProcessor StatusTickProcessor;
        internal ConcentrationSystem ConcentrationSystem;
        internal EffectPipeline EffectPipeline;
        internal TargetValidator TargetValidator;
        internal ActionRegistry ActionRegistry;
        internal StatsRegistry StatsRegistry;
        internal StatusRegistry BG3StatusRegistry;
        internal QDND.Combat.Statuses.BG3StatusIntegration BG3StatusIntegration;
        internal StatusInteractionRules StatusInteractionRules;
        internal PassiveRegistry PassiveRegistry;
        internal InterruptRegistry InterruptRegistry;
        internal BG3AIRegistry BG3AiRegistry;
        internal AIDecisionPipeline AIPipeline;
        internal MovementService MovementService;
        internal CombatMovementCoordinator MovementCoordinator;
        internal ReactionSystem ReactionSystem;
        internal ReactionCoordinator ReactionCoordinator;
        internal IReactionResolver ReactionResolver;
        internal ResolutionStack ResolutionStack;
        internal SurfaceManager SurfaceManager;
        internal ResourceManager ResourceManager;
        internal RestService RestService;
        internal ForcedMovementService ForcedMovementService;
        internal QDND.Combat.Rules.Functors.FunctorExecutor FunctorExecutor;
        internal MetamagicService MetamagicService;
        internal TurnLifecycleService TurnLifecycleService;
        internal ActionExecutionService ActionExecutionService;
        internal CombatPresentationService PresentationService;
        internal IVfxPlaybackService VfxPlaybackService;
        internal ActionBarModel ActionBarModel;
        internal TurnTrackerModel TurnTrackerModel;
        internal ResourceBarModel ResourceBarModel;
        internal ActionBarService ActionBarService;
        internal InventoryService InventoryService;
        internal SelectionService SelectionService;
        internal ScenarioBootService ScenarioBootService;
        internal ICombatantRegistry CombatantRegistry;
        internal Action DisposeAction;

        public void Dispose()
        {
            DisposeAction?.Invoke();
            DisposeAction = null;
        }
    }
}
