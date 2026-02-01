using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Abilities;
using QDND.Combat.Abilities.Effects;
using QDND.Combat.Statuses;
using QDND.Combat.Targeting;
using QDND.Combat.AI;
using QDND.Data;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Main combat arena scene controller. Handles visual representation of combat,
    /// spawns combatant visuals, manages camera, and coordinates UI.
    /// </summary>
    public partial class CombatArena : Node3D
    {
        [Export] public string ScenarioPath = "res://Data/Scenarios/minimal_combat.json";
        [Export] public bool VerboseLogging = true;
        [Export] public PackedScene CombatantVisualScene;
        [Export] public float TileSize = 2.0f;
        
        // Node references (set in _Ready or via editor)
        private Camera3D _camera;
        private Node3D _combatantsContainer;
        private CanvasLayer _hudLayer;
        
        // Combat backend
        private CombatContext _combatContext;
        private CombatStateMachine _stateMachine;
        private TurnQueueService _turnQueue;
        private CommandService _commandService;
        private CombatLog _combatLog;
        private ScenarioLoader _scenarioLoader;
        private RulesEngine _rulesEngine;
        private StatusManager _statusManager;
        private EffectPipeline _effectPipeline;
        private TargetValidator _targetValidator;
        private DataRegistry _dataRegistry;
        private AIDecisionPipeline _aiPipeline;
        
        // Visual tracking
        private Dictionary<string, CombatantVisual> _combatantVisuals = new();
        private List<Combatant> _combatants = new();
        private Random _rng;
        
        // Input state
        private string _selectedCombatantId;
        private string _selectedAbilityId;
        private bool _isPlayerTurn;
        
        public CombatContext Context => _combatContext;
        public string SelectedCombatantId => _selectedCombatantId;
        public string SelectedAbilityId => _selectedAbilityId;
        public bool IsPlayerTurn => _isPlayerTurn;

        public override void _Ready()
        {
            Log("=== COMBAT ARENA INITIALIZING ===");
            
            // Get node references
            _camera = GetNodeOrNull<Camera3D>("TacticalCamera");
            _combatantsContainer = GetNodeOrNull<Node3D>("Combatants");
            _hudLayer = GetNodeOrNull<CanvasLayer>("HUD");
            
            if (_combatantsContainer == null)
            {
                _combatantsContainer = new Node3D { Name = "Combatants" };
                AddChild(_combatantsContainer);
            }
            
            // Initialize combat backend
            InitializeCombatContext();
            RegisterServices();
            
            // Load scenario and spawn visuals
            LoadScenario(ScenarioPath);
            SpawnCombatantVisuals();
            
            // Start combat
            StartCombat();
            
            Log("=== COMBAT ARENA READY ===");
        }

        private void InitializeCombatContext()
        {
            _combatContext = new CombatContext();
            _combatContext.Name = "CombatContext";
            AddChild(_combatContext);
            Log("CombatContext created");
        }

        private void RegisterServices()
        {
            // Core services
            _stateMachine = new CombatStateMachine();
            _turnQueue = new TurnQueueService();
            _commandService = new CommandService();
            _combatLog = new CombatLog();
            _scenarioLoader = new ScenarioLoader();

            _commandService.StateMachine = _stateMachine;
            _commandService.TurnQueue = _turnQueue;

            // Subscribe to events
            _stateMachine.OnStateChanged += OnStateChanged;
            _turnQueue.OnTurnChanged += OnTurnChanged;
            _commandService.OnCommandExecuted += OnCommandExecuted;

            _combatContext.RegisterService(_stateMachine);
            _combatContext.RegisterService(_turnQueue);
            _combatContext.RegisterService(_commandService);
            _combatContext.RegisterService(_combatLog);
            _combatContext.RegisterService(_scenarioLoader);

            // Phase B services
            _dataRegistry = new DataRegistry();
            string dataPath = ProjectSettings.GlobalizePath("res://Data");
            _dataRegistry.LoadFromDirectory(dataPath);
            _dataRegistry.ValidateOrThrow();

            _rulesEngine = new RulesEngine(42);
            
            _statusManager = new StatusManager(_rulesEngine);
            foreach (var statusDef in _dataRegistry.GetAllStatuses())
            {
                _statusManager.RegisterStatus(statusDef);
            }
            
            _effectPipeline = new EffectPipeline
            {
                Rules = _rulesEngine,
                Statuses = _statusManager,
                Rng = new Random(42)
            };
            foreach (var abilityDef in _dataRegistry.GetAllAbilities())
            {
                _effectPipeline.RegisterAbility(abilityDef);
            }
            
            _targetValidator = new TargetValidator();

            // Subscribe to status events for visual feedback
            _statusManager.OnStatusApplied += OnStatusApplied;
            _statusManager.OnStatusRemoved += OnStatusRemoved;
            _statusManager.OnStatusTick += OnStatusTick;

            _combatContext.RegisterService(_dataRegistry);
            _combatContext.RegisterService(_rulesEngine);
            _combatContext.RegisterService(_statusManager);
            _combatContext.RegisterService(_effectPipeline);
            _combatContext.RegisterService(_targetValidator);

            // AI Pipeline
            _aiPipeline = new AIDecisionPipeline(_combatContext);
            _combatContext.RegisterService(_aiPipeline);

            Log($"Services registered: {_combatContext.GetRegisteredServices().Count}");
        }

        private void LoadScenario(string path)
        {
            try
            {
                var scenario = _scenarioLoader.LoadFromFile(path);
                _combatants = _scenarioLoader.SpawnCombatants(scenario, _turnQueue);
                _rng = new Random(scenario.Seed);
                _effectPipeline.Rng = _rng;

                foreach (var c in _combatants)
                {
                    _combatContext.RegisterCombatant(c);
                }
                
                _combatLog.LogCombatStart(_combatants.Count, scenario.Seed);
                Log($"Loaded scenario: {_combatants.Count} combatants");
            }
            catch (Exception ex)
            {
                GD.PushError($"Failed to load scenario: {ex.Message}");
            }
        }

        private void SpawnCombatantVisuals()
        {
            foreach (var combatant in _combatants)
            {
                SpawnVisualForCombatant(combatant);
            }
        }

        private void SpawnVisualForCombatant(Combatant combatant)
        {
            CombatantVisual visual;
            
            if (CombatantVisualScene != null)
            {
                visual = CombatantVisualScene.Instantiate<CombatantVisual>();
            }
            else
            {
                // Create a basic visual programmatically
                visual = new CombatantVisual();
            }
            
            visual.Initialize(combatant, this);
            visual.Position = CombatantPositionToWorld(combatant.Position);
            visual.Name = $"Visual_{combatant.Id}";
            
            _combatantsContainer.AddChild(visual);
            _combatantVisuals[combatant.Id] = visual;
            
            Log($"Spawned visual for {combatant.Name} at {visual.Position}");
        }

        private Vector3 CombatantPositionToWorld(Vector3 gridPos)
        {
            // Convert grid position to world position
            return new Vector3(gridPos.X * TileSize, 0, gridPos.Z * TileSize);
        }

        private void StartCombat()
        {
            _stateMachine.TryTransition(CombatState.CombatStart, "Combat initiated");
            _turnQueue.StartCombat();
            _stateMachine.TryTransition(CombatState.TurnStart, "First turn");
            
            var firstCombatant = _turnQueue.CurrentCombatant;
            if (firstCombatant != null)
            {
                BeginTurn(firstCombatant);
            }
        }

        private void BeginTurn(Combatant combatant)
        {
            _isPlayerTurn = combatant.IsPlayerControlled;
            
            var decisionState = _isPlayerTurn 
                ? CombatState.PlayerDecision 
                : CombatState.AIDecision;
            _stateMachine.TryTransition(decisionState, $"Awaiting {combatant.Name}'s decision");

            // Process turn start effects
            _effectPipeline.ProcessTurnStart(combatant.Id);

            // Highlight active combatant
            foreach (var visual in _combatantVisuals.Values)
            {
                visual.SetActive(visual.CombatantId == combatant.Id);
            }

            if (!_isPlayerTurn)
            {
                // AI turn - execute after a short delay for visibility
                GetTree().CreateTimer(0.5).Timeout += () => ExecuteAITurn(combatant);
            }
            else
            {
                // Player turn - auto-select the active combatant
                SelectCombatant(combatant.Id);
            }

            Log($"Turn started: {combatant.Name} ({(_isPlayerTurn ? "Player" : "AI")})");
        }

        private void ExecuteAITurn(Combatant combatant)
        {
            if (_turnQueue.ShouldEndCombat())
            {
                EndCombat();
                return;
            }

            var profile = AIProfile.CreateForArchetype(AIArchetype.Aggressive, AIDifficulty.Normal);
            var decision = _aiPipeline.MakeDecision(combatant, profile);

            if (decision?.ChosenAction != null && !string.IsNullOrEmpty(decision.ChosenAction.AbilityId))
            {
                // Execute ability
                var action = decision.ChosenAction;
                var ability = _effectPipeline.GetAbility(action.AbilityId);
                if (ability != null)
                {
                    var target = _combatContext.GetCombatant(action.TargetId);
                    if (target != null)
                    {
                        ExecuteAbility(combatant.Id, action.AbilityId, action.TargetId);
                    }
                }
            }

            // End AI turn after action
            GetTree().CreateTimer(0.5).Timeout += () => EndCurrentTurn();
        }

        public void SelectCombatant(string combatantId)
        {
            // Deselect previous
            if (!string.IsNullOrEmpty(_selectedCombatantId) && _combatantVisuals.TryGetValue(_selectedCombatantId, out var prevVisual))
            {
                prevVisual.SetSelected(false);
            }

            _selectedCombatantId = combatantId;
            _selectedAbilityId = null;

            if (!string.IsNullOrEmpty(combatantId) && _combatantVisuals.TryGetValue(combatantId, out var visual))
            {
                visual.SetSelected(true);
                Log($"Selected: {combatantId}");
            }
        }

        public void SelectAbility(string abilityId)
        {
            _selectedAbilityId = abilityId;
            Log($"Ability selected: {abilityId}");
            
            // Highlight valid targets
            if (!string.IsNullOrEmpty(_selectedCombatantId))
            {
                var actor = _combatContext.GetCombatant(_selectedCombatantId);
                var ability = _effectPipeline.GetAbility(abilityId);
                if (actor != null && ability != null)
                {
                    var validTargets = _targetValidator.GetValidTargets(ability, actor, _combatants);
                    foreach (var visual in _combatantVisuals.Values)
                    {
                        bool isValidTarget = validTargets.Any(t => t.Id == visual.CombatantId);
                        visual.SetValidTarget(isValidTarget);
                    }
                }
            }
        }

        public void ClearSelection()
        {
            _selectedAbilityId = null;
            foreach (var visual in _combatantVisuals.Values)
            {
                visual.SetValidTarget(false);
            }
        }

        public void ExecuteAbility(string actorId, string abilityId, string targetId)
        {
            var actor = _combatContext.GetCombatant(actorId);
            var target = _combatContext.GetCombatant(targetId);
            
            if (actor == null || target == null)
            {
                Log($"Invalid actor or target for ability execution");
                return;
            }

            _stateMachine.TryTransition(CombatState.ActionExecution, $"{actor.Name} using {abilityId}");

            var result = _effectPipeline.ExecuteAbility(abilityId, actor, new List<Combatant> { target });
            
            if (result.Success)
            {
                // Visual feedback
                if (_combatantVisuals.TryGetValue(actorId, out var actorVisual))
                {
                    actorVisual.PlayAttackAnimation();
                }
                
                if (_combatantVisuals.TryGetValue(targetId, out var targetVisual))
                {
                    foreach (var effect in result.EffectResults)
                    {
                        if (effect.EffectType == "damage")
                        {
                            targetVisual.ShowDamage((int)effect.Value);
                        }
                        else if (effect.EffectType == "heal")
                        {
                            targetVisual.ShowHealing((int)effect.Value);
                        }
                    }
                    targetVisual.UpdateFromEntity();
                }

                Log($"{actor.Name} used {abilityId} on {target.Name}: {string.Join(", ", result.EffectResults.Select(e => $"{e.EffectType}:{e.Value}"))}");
            }
            else
            {
                Log($"Ability failed: {result.ErrorMessage}");
            }

            ClearSelection();

            // Check for combat end
            if (_turnQueue.ShouldEndCombat())
            {
                EndCombat();
            }
        }

        public void EndCurrentTurn()
        {
            var current = _turnQueue.CurrentCombatant;
            if (current == null) return;

            // Process status ticks
            _statusManager.ProcessTurnEnd(current.Id);

            // Execute end turn command
            var cmd = new EndTurnCommand(current.Id);
            _commandService.Execute(cmd);

            // Check for combat end
            if (_turnQueue.ShouldEndCombat())
            {
                EndCombat();
                return;
            }

            // Start next turn
            var next = _turnQueue.CurrentCombatant;
            if (next != null)
            {
                _stateMachine.TryTransition(CombatState.TurnStart, "Next turn");
                BeginTurn(next);
            }
        }

        private void EndCombat()
        {
            _stateMachine.TryTransition(CombatState.CombatEnd, "Combat ended");
            _statusManager.ProcessRoundEnd();
            _effectPipeline.ProcessRoundEnd();
            
            // Determine winner
            var playerAlive = _combatants.Any(c => c.Faction == Faction.Player && c.IsActive);
            var enemyAlive = _combatants.Any(c => c.Faction == Faction.Hostile && c.IsActive);
            
            string result = "Draw";
            if (playerAlive && !enemyAlive) result = "Victory!";
            else if (!playerAlive && enemyAlive) result = "Defeat!";
            
            _combatLog.LogCombatEnd(result);
            Log($"=== COMBAT ENDED: {result} ===");
        }

        // Event handlers for visual feedback
        private void OnStateChanged(StateTransitionEvent evt)
        {
            _combatLog.LogStateTransition(evt);
            Log($"[STATE] {evt.FromState} -> {evt.ToState}");
        }

        private void OnTurnChanged(TurnChangeEvent evt)
        {
            _combatLog.LogTurnChange(evt);
            Log($"[TURN] Round {evt.Round}, {evt.CurrentCombatant?.Name}");
        }

        private void OnCommandExecuted(CommandExecutedEvent evt)
        {
            _combatLog.LogCommand(evt);
        }

        private void OnStatusApplied(StatusInstance status)
        {
            if (_combatantVisuals.TryGetValue(status.TargetId, out var visual))
            {
                visual.ShowStatusApplied(status.Definition.Name);
            }
            Log($"[STATUS] {status.Definition.Name} applied to {status.TargetId}");
        }

        private void OnStatusRemoved(StatusInstance status)
        {
            if (_combatantVisuals.TryGetValue(status.TargetId, out var visual))
            {
                visual.ShowStatusRemoved(status.Definition.Name);
            }
        }

        private void OnStatusTick(StatusInstance status)
        {
            var target = _combatContext.GetCombatant(status.TargetId);
            if (target == null || !target.IsActive) return;

            foreach (var tick in status.Definition.TickEffects)
            {
                float value = tick.Value + (tick.ValuePerStack * (status.Stacks - 1));
                
                if (tick.EffectType == "damage")
                {
                    target.Resources.TakeDamage((int)value);
                    if (_combatantVisuals.TryGetValue(status.TargetId, out var visual))
                    {
                        visual.ShowDamage((int)value);
                        visual.UpdateFromEntity();
                    }
                }
                else if (tick.EffectType == "heal")
                {
                    target.Resources.Heal((int)value);
                    if (_combatantVisuals.TryGetValue(status.TargetId, out var visual))
                    {
                        visual.ShowHealing((int)value);
                        visual.UpdateFromEntity();
                    }
                }
            }
        }

        public CombatantVisual GetVisual(string combatantId)
        {
            return _combatantVisuals.TryGetValue(combatantId, out var v) ? v : null;
        }

        public IEnumerable<Combatant> GetCombatants() => _combatants;

        public List<AbilityDefinition> GetAbilitiesForCombatant(string combatantId)
        {
            // For now, return all registered abilities
            // In future, this should be based on combatant's class/equipment
            return _dataRegistry.GetAllAbilities().ToList();
        }

        private void Log(string message)
        {
            if (VerboseLogging)
            {
                GD.Print($"[CombatArena] {message}");
            }
        }
    }
}
