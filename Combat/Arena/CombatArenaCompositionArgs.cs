using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Movement;
using QDND.Combat.Rules;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Combat.Statuses;
using QDND.Combat.Targeting.Visuals;
using QDND.Combat.UI;
using QDND.Combat.UI.Base;
using QDND.Combat.VFX;
using QDND.Tools.AutoBattler;

namespace QDND.Combat.Arena
{
    public sealed class CombatArenaCompositionArgs
    {
        public CombatArena ArenaScene { get; init; }
        public CombatContext CombatContext { get; init; }
        public CombatCameraService CameraService { get; init; }
        public MovementPreview MovementPreview { get; init; }
        public CombatInputHandler InputHandler { get; init; }
        public RangeIndicator RangeIndicator { get; init; }
        public ReactionPromptUI ReactionPromptUI { get; init; }
        public CanvasLayer HudLayer { get; init; }
        public CombatVFXManager VfxManager { get; init; }
        public Node3D CombatantsContainer { get; init; }
        public Dictionary<string, CombatantVisual> CombatantVisuals { get; init; }
        public Dictionary<string, List<Vector3>> PendingJumpWorldPaths { get; init; }
        public HashSet<string> OneTimeLogKeys { get; init; }
        public SpecialMovementService SpecialMovementService { get; init; }
        public float TileSize { get; init; }
        public float DefaultMovePoints { get; init; }
        public bool VerboseLogging { get; init; }
        public AutoBattleConfig AutoBattleConfig { get; init; }
        public int RandomSeed { get; init; }
        public int? ScenarioSeedOverride { get; init; }
        public int? AutoBattleSeedOverride { get; init; }
        public int ResolvedScenarioSeed { get; init; }
        public ScenarioBootService.DynamicScenarioMode DynamicScenarioMode { get; init; }
        public string DynamicActionTestId { get; init; }
        public List<string> DynamicActionBatchIds { get; init; }
        public int DynamicCharacterLevel { get; init; }
        public int DynamicTeamSize { get; init; }
        public Action<string> Log { get; init; }
        public Action<string, string> LogOnce { get; init; }
        public Action<string> LogError { get; init; }
        public Func<string, Combatant> ResolveCombatant { get; init; }
        public Func<IEnumerable<Combatant>> GetCombatants { get; init; }
        public Func<Random> GetRandom { get; init; }
        public Func<bool> IsAutoBattleMode { get; init; }
        public Func<bool> UseBuiltInAI { get; init; }
        public Func<string> GetSelectedCombatantId { get; init; }
        public Func<bool> GetIsPlayerTurn { get; init; }
        public Func<string> GetActiveCombatantId { get; init; }
        public Func<AutoBattleConfig> GetAutoBattleConfig { get; init; }
        public Func<string, bool> CanPlayerControl { get; init; }
        public Func<float, SceneTreeTimer> CreateFloatTimer { get; init; }
        public Func<double, SceneTreeTimer> CreateDoubleTimer { get; init; }
        public Func<Vector3, float, bool> IsWorldPositionBlocked { get; init; }
        public Func<Combatant, Vector3, JumpPathResult> BuildJumpPath { get; init; }
        public Func<Combatant, float> GetJumpDistanceLimit { get; init; }
        public Func<Combatant, AIAction, List<AIAction>, bool> ExecuteAIMovementWithFallback { get; init; }
        public Func<Combatant, bool> ExecuteDash { get; init; }
        public Func<Combatant, bool> ExecuteDisengage { get; init; }
        public Func<bool> ShouldAllowVictory { get; init; }
        public Action CheckAndEndCombat { get; init; }
        public Action<Combatant> ExecuteAITurn { get; init; }
        public Action<string> SelectCombatant { get; init; }
        public Action<Combatant> CenterCameraOnCombatant { get; init; }
        public Action<string> PopulateActionBar { get; init; }
        public Action<string> RefreshActionBarUsability { get; init; }
        public Action ClearSelection { get; init; }
        public Action<string, Vector3, bool> FaceCombatantTowardsGridPoint { get; init; }
        public Action<string, long?> ResumeDecisionStateIfExecuting { get; init; }
        public Action DispatchThreatenedStatusesSync { get; init; }
        public Action<RuleWindow, Combatant, Combatant> DispatchRuleWindow { get; init; }
        public Action<StateTransitionEvent> HandleStateChanged { get; init; }
        public Action<TurnChangeEvent> HandleTurnChanged { get; init; }
        public Action<CommandExecutedEvent> HandleCommandExecuted { get; init; }
        public Action<StatusInstance> HandleStatusApplied { get; init; }
        public Action<StatusInstance> HandleStatusRemoved { get; init; }
        public Action<StatusInstance> HandleStatusTick { get; init; }
        public Action<SurfaceInstance> HandleSurfaceCreated { get; init; }
        public Action<SurfaceInstance> HandleSurfaceRemoved { get; init; }
        public Action<SurfaceInstance, SurfaceInstance> HandleSurfaceTransformed { get; init; }
        public Action<SurfaceInstance> HandleSurfaceGeometryChanged { get; init; }
        public Action<SurfaceInstance, Combatant, SurfaceTrigger> HandleSurfaceTriggered { get; init; }
        public Action<string, bool> SetConcentratingVisual { get; init; }
        public Action<Combatant, ActionDefinition> NotifyAIAbilityUsed { get; init; }
    }
}
