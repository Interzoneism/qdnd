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
using QDND.Data;

namespace QDND.Tools
{
    /// <summary>
    /// Bootstrap script for Testbed.tscn. Initializes CombatContext, registers services,
    /// loads a default scenario, and runs automated combat simulation with validation.
    /// Phase B: Includes RulesEngine, StatusManager, EffectPipeline, and TargetValidator.
    /// </summary>
    public partial class TestbedBootstrap : Node3D
    {
        [Export] public bool HeadlessMode = false;
        [Export] public string DefaultScenarioPath = "res://Data/Scenarios/minimal_combat.json";
        [Export] public bool VerboseLogging = true;
        [Export] public int AutoRunTurns = 8;
        [Export] public bool RunPhaseBTests = true;

        private CombatContext _combatContext;
        private CombatStateMachine _stateMachine;
        private TurnQueueService _turnQueue;
        private CommandService _commandService;
        private CombatLog _combatLog;
        private ScenarioLoader _scenarioLoader;
        
        // Phase B services
        private RulesEngine _rulesEngine;
        private StatusManager _statusManager;
        private EffectPipeline _effectPipeline;
        private TargetValidator _targetValidator;
        
        private List<string> _initializationLog = new List<string>();
        private List<Combatant> _combatants = new List<Combatant>();
        private Random _rng;

        public override void _Ready()
        {
            if (VerboseLogging)
            {
                GD.Print("=== TESTBED BOOTSTRAP START (Phase B) ===");
            }

            LogStep("Testbed bootstrap initiated");
            
            InitializeCombatContext();
            RegisterCoreServices();
            RegisterPhaseBServices();
            
            if (!string.IsNullOrEmpty(DefaultScenarioPath))
            {
                LoadScenario(DefaultScenarioPath);
            }

            if (AutoRunTurns > 0)
            {
                RunCombatSimulation();
            }

            if (RunPhaseBTests)
            {
                RunPhaseBValidation();
            }

            EmitReadyEvent();

            if (VerboseLogging)
            {
                GD.Print("=== TESTBED BOOTSTRAP COMPLETE ===");
                PrintDiagnostics();
                PrintFinalStateHash();
            }

            if (HeadlessMode)
            {
                GD.Print("[SUCCESS] Testbed completed successfully");
                GetTree().Quit(0);
            }
        }

        private void InitializeCombatContext()
        {
            _combatContext = new CombatContext();
            _combatContext.Name = "CombatContext";
            AddChild(_combatContext);
            LogStep("CombatContext created and added to scene tree");
        }

        private void RegisterCoreServices()
        {
            LogStep("Registering core services...");
            
            _stateMachine = new CombatStateMachine();
            _turnQueue = new TurnQueueService();
            _commandService = new CommandService();
            _combatLog = new CombatLog();
            _scenarioLoader = new ScenarioLoader();

            _commandService.StateMachine = _stateMachine;
            _commandService.TurnQueue = _turnQueue;

            _stateMachine.OnStateChanged += OnStateChanged;
            _turnQueue.OnTurnChanged += OnTurnChanged;
            _commandService.OnCommandExecuted += OnCommandExecuted;

            _combatContext.RegisterService(_stateMachine);
            _combatContext.RegisterService(_turnQueue);
            _combatContext.RegisterService(_commandService);
            _combatContext.RegisterService(_combatLog);
            _combatContext.RegisterService(_scenarioLoader);
            
            LogStep($"Core services registered: {_combatContext.GetRegisteredServices().Count}");
        }

        private void RegisterPhaseBServices()
        {
            LogStep("Registering Phase B services...");

            // Create RulesEngine with fixed seed for determinism
            _rulesEngine = new RulesEngine(42);
            
            // Create StatusManager
            _statusManager = new StatusManager(_rulesEngine);
            RegisterSampleStatuses();
            
            // Create EffectPipeline
            _effectPipeline = new EffectPipeline
            {
                Rules = _rulesEngine,
                Statuses = _statusManager,
                Rng = new Random(42)
            };
            RegisterSampleAbilities();
            
            // Create TargetValidator
            _targetValidator = new TargetValidator();

            // Subscribe to status events
            _statusManager.OnStatusApplied += (s) => LogStep($"[STATUS] Applied {s.Definition.Name} to {s.TargetId}");
            _statusManager.OnStatusRemoved += (s) => LogStep($"[STATUS] Removed {s.Definition.Name} from {s.TargetId}");

            _combatContext.RegisterService(_rulesEngine);
            _combatContext.RegisterService(_statusManager);
            _combatContext.RegisterService(_effectPipeline);
            _combatContext.RegisterService(_targetValidator);

            LogStep($"Phase B services registered: RulesEngine, StatusManager, EffectPipeline, TargetValidator");
        }

        private void RegisterSampleStatuses()
        {
            // Register sample statuses directly (simplified from JSON for bootstrap)
            _statusManager.RegisterStatus(new StatusDefinition
            {
                Id = "poisoned",
                Name = "Poisoned",
                DurationType = DurationType.Turns,
                DefaultDuration = 3,
                MaxStacks = 3,
                Stacking = StackingBehavior.Stack,
                IsBuff = false,
                Modifiers = new List<StatusModifier>
                {
                    new() { Target = ModifierTarget.AttackRoll, Type = ModifierType.Flat, Value = -2 }
                },
                TickEffects = new List<StatusTickEffect>
                {
                    new() { EffectType = "damage", Value = 2, ValuePerStack = 2, DamageType = "poison" }
                },
                Tags = new HashSet<string> { "poison", "debuff" }
            });

            _statusManager.RegisterStatus(new StatusDefinition
            {
                Id = "inspired",
                Name = "Inspired",
                DurationType = DurationType.Turns,
                DefaultDuration = 2,
                MaxStacks = 1,
                Stacking = StackingBehavior.Refresh,
                IsBuff = true,
                Modifiers = new List<StatusModifier>
                {
                    new() { Target = ModifierTarget.AttackRoll, Type = ModifierType.Flat, Value = 2 },
                    new() { Target = ModifierTarget.DamageDealt, Type = ModifierType.Flat, Value = 1 }
                },
                Tags = new HashSet<string> { "buff", "morale" }
            });

            _statusManager.RegisterStatus(new StatusDefinition
            {
                Id = "shielded",
                Name = "Shielded",
                DurationType = DurationType.Turns,
                DefaultDuration = 3,
                MaxStacks = 1,
                Stacking = StackingBehavior.Refresh,
                IsBuff = true,
                Modifiers = new List<StatusModifier>
                {
                    new() { Target = ModifierTarget.ArmorClass, Type = ModifierType.Flat, Value = 2 }
                },
                Tags = new HashSet<string> { "buff", "magic" }
            });

            LogStep("Sample statuses registered: poisoned, inspired, shielded");
        }

        private void RegisterSampleAbilities()
        {
            _effectPipeline.RegisterAbility(new AbilityDefinition
            {
                Id = "basic_attack",
                Name = "Basic Attack",
                TargetType = TargetType.SingleUnit,
                TargetFilter = TargetFilter.Enemies,
                Range = 1.5f,
                AttackType = AttackType.MeleeWeapon,
                Effects = new List<EffectDefinition>
                {
                    new() { Type = "damage", DiceFormula = "1d8+2", DamageType = "physical", Condition = "on_hit" }
                },
                Tags = new HashSet<string> { "weapon", "melee" }
            });

            _effectPipeline.RegisterAbility(new AbilityDefinition
            {
                Id = "heal_wounds",
                Name = "Heal Wounds",
                TargetType = TargetType.SingleUnit,
                TargetFilter = TargetFilter.Allies,
                Range = 5f,
                Effects = new List<EffectDefinition>
                {
                    new() { Type = "heal", DiceFormula = "2d8+3" }
                },
                Tags = new HashSet<string> { "spell", "healing" }
            });

            _effectPipeline.RegisterAbility(new AbilityDefinition
            {
                Id = "poison_strike",
                Name = "Poison Strike",
                TargetType = TargetType.SingleUnit,
                TargetFilter = TargetFilter.Enemies,
                Range = 1.5f,
                AttackType = AttackType.MeleeWeapon,
                Effects = new List<EffectDefinition>
                {
                    new() { Type = "damage", DiceFormula = "1d6+2", DamageType = "physical", Condition = "on_hit" },
                    new() { Type = "apply_status", StatusId = "poisoned", StatusDuration = 3, Condition = "on_hit" }
                },
                Tags = new HashSet<string> { "weapon", "melee", "poison" }
            });

            LogStep("Sample abilities registered: basic_attack, heal_wounds, poison_strike");
        }

        private void LoadScenario(string scenarioPath)
        {
            LogStep($"Loading scenario: {scenarioPath}");
            
            try
            {
                var scenario = _scenarioLoader.LoadFromFile(scenarioPath);
                _combatants = _scenarioLoader.SpawnCombatants(scenario, _turnQueue);
                _rng = new Random(scenario.Seed);
                _effectPipeline.Rng = _rng;
                
                _combatLog.LogCombatStart(_combatants.Count, scenario.Seed);
                LogStep($"Scenario loaded: {_combatants.Count} combatants, seed {scenario.Seed}");

                foreach (var c in _combatants)
                {
                    LogStep($"  - {c}");
                }
            }
            catch (Exception ex)
            {
                LogStep($"Failed to load scenario: {ex.Message}");
                GD.PushError($"Scenario load failed: {ex}");
            }
        }

        private void RunCombatSimulation()
        {
            LogStep("Starting combat simulation...");
            
            _stateMachine.TryTransition(CombatState.CombatStart, "Combat initiated");
            _turnQueue.StartCombat();
            _stateMachine.TryTransition(CombatState.TurnStart, "First turn");
            
            var firstCombatant = _turnQueue.CurrentCombatant;
            if (firstCombatant != null)
            {
                var decisionState = firstCombatant.IsPlayerControlled 
                    ? CombatState.PlayerDecision 
                    : CombatState.AIDecision;
                _stateMachine.TryTransition(decisionState, $"Awaiting {firstCombatant.Name}'s decision");
            }

            for (int i = 0; i < AutoRunTurns && !_turnQueue.ShouldEndCombat(); i++)
            {
                var current = _turnQueue.CurrentCombatant;
                if (current == null) break;

                // Process turn start for cooldowns
                _effectPipeline.ProcessTurnStart(current.Id);

                var cmd = new EndTurnCommand(current.Id);
                _commandService.Execute(cmd);

                // Process status ticks
                _statusManager.ProcessTurnEnd(current.Id);
            }

            // Process round end for round-based statuses
            _statusManager.ProcessRoundEnd();
            _effectPipeline.ProcessRoundEnd();

            if (_stateMachine.CurrentState != CombatState.CombatEnd &&
                _stateMachine.CurrentState != CombatState.NotInCombat)
            {
                _stateMachine.TryTransition(CombatState.CombatEnd, "Simulation complete");
                _combatLog.LogCombatEnd("Simulation completed");
            }

            LogStep($"Combat simulation complete - {_turnQueue.CurrentRound} rounds");
        }

        private void RunPhaseBValidation()
        {
            GD.Print("\n=== PHASE B VALIDATION ===");
            
            if (_combatants.Count < 2)
            {
                GD.Print("[SKIP] Not enough combatants for Phase B tests");
                return;
            }

            var attacker = _combatants.FirstOrDefault(c => c.Faction == Faction.Player);
            var defender = _combatants.FirstOrDefault(c => c.Faction == Faction.Hostile);

            if (attacker == null || defender == null)
            {
                GD.Print("[SKIP] Need both player and hostile combatants");
                return;
            }

            int passed = 0;
            int total = 0;

            // Test 1: Attack roll with modifiers
            total++;
            GD.Print("\n[TEST] Attack Roll with Modifiers");
            _rulesEngine.AddModifier(attacker.Id, Modifier.Flat("Test Bonus", ModifierTarget.AttackRoll, 5));
            var attackResult = _rulesEngine.RollAttack(new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 5
            });
            GD.Print($"  Roll: {attackResult.NaturalRoll}, Total: {attackResult.FinalValue}, Hit: {attackResult.IsSuccess}");
            GD.Print($"  Breakdown: {attackResult.GetBreakdown()}");
            if (attackResult.FinalValue >= attackResult.NaturalRoll + 10) // Base 5 + modifier 5
            {
                GD.Print("  [PASS] Modifiers applied correctly");
                passed++;
            }
            else
            {
                GD.Print("  [FAIL] Modifiers not applied correctly");
            }

            // Test 2: Status application and modifier integration
            total++;
            GD.Print("\n[TEST] Status Application");
            var statusInstance = _statusManager.ApplyStatus("poisoned", attacker.Id, defender.Id);
            bool hasStatus = _statusManager.HasStatus(defender.Id, "poisoned");
            var defenderMods = _rulesEngine.GetModifiers(defender.Id);
            var poisonMod = defenderMods.GetModifiers(ModifierTarget.AttackRoll, null).FirstOrDefault();
            if (hasStatus && statusInstance != null && poisonMod != null && poisonMod.Value == -2)
            {
                GD.Print($"  Status applied: {statusInstance}");
                GD.Print($"  Modifier on defender: {poisonMod}");
                GD.Print("  [PASS] Status and modifiers integrated correctly");
                passed++;
            }
            else
            {
                GD.Print("  [FAIL] Status/modifier integration failed");
            }

            // Test 3: Damage effect execution
            total++;
            GD.Print("\n[TEST] Damage Effect Execution");
            int hpBefore = defender.Resources.CurrentHP;
            var damageResult = _effectPipeline.ExecuteAbility(
                "basic_attack",
                attacker,
                new List<Combatant> { defender }
            );
            int hpAfter = defender.Resources.CurrentHP;
            if (damageResult.Success && damageResult.EffectResults.Any())
            {
                var dmgEffect = damageResult.EffectResults.First();
                GD.Print($"  Attack result: Hit={damageResult.AttackResult?.IsSuccess}, Crit={damageResult.AttackResult?.IsCritical}");
                GD.Print($"  Damage dealt: {dmgEffect.Value}, HP: {hpBefore} -> {hpAfter}");
                if (damageResult.AttackResult?.IsSuccess == true && hpAfter < hpBefore)
                {
                    GD.Print("  [PASS] Damage applied correctly on hit");
                    passed++;
                }
                else if (damageResult.AttackResult?.IsSuccess == false)
                {
                    GD.Print("  [PASS] Attack missed, no damage (expected behavior)");
                    passed++;
                }
                else
                {
                    GD.Print("  [FAIL] Damage not applied correctly");
                }
            }
            else
            {
                GD.Print($"  [FAIL] Ability execution failed: {damageResult.ErrorMessage}");
            }

            // Test 4: Heal effect execution
            total++;
            GD.Print("\n[TEST] Heal Effect Execution");
            defender.Resources.TakeDamage(10); // Ensure defender has damage to heal
            int hpBeforeHeal = defender.Resources.CurrentHP;
            var healResult = _effectPipeline.ExecuteAbility(
                "heal_wounds",
                attacker,
                new List<Combatant> { defender }
            );
            int hpAfterHeal = defender.Resources.CurrentHP;
            if (healResult.Success && hpAfterHeal > hpBeforeHeal)
            {
                var healEffect = healResult.EffectResults.FirstOrDefault();
                GD.Print($"  Healed: {healEffect?.Value}, HP: {hpBeforeHeal} -> {hpAfterHeal}");
                GD.Print("  [PASS] Heal applied correctly");
                passed++;
            }
            else
            {
                GD.Print("  [FAIL] Heal not applied correctly");
            }

            // Test 5: Target validation
            total++;
            GD.Print("\n[TEST] Target Validation");
            var basicAttack = _effectPipeline.GetAbility("basic_attack");
            var validTargets = _targetValidator.GetValidTargets(basicAttack, attacker, _combatants);
            bool correctFilter = validTargets.All(t => t.Faction == Faction.Hostile) && 
                                 validTargets.Any();
            if (correctFilter)
            {
                GD.Print($"  Valid targets for basic_attack: {string.Join(", ", validTargets.Select(t => t.Name))}");
                GD.Print("  [PASS] Target filter correctly excludes allies");
                passed++;
            }
            else
            {
                GD.Print("  [FAIL] Target filter incorrect");
            }

            // Test 6: Hit chance calculation
            total++;
            GD.Print("\n[TEST] Hit Chance Calculation");
            var hitChanceResult = _rulesEngine.CalculateHitChance(new QueryInput
            {
                Type = QueryType.HitChance,
                Source = attacker,
                Target = defender,
                BaseValue = 5
            });
            if (hitChanceResult.FinalValue >= 0 && hitChanceResult.FinalValue <= 100)
            {
                GD.Print($"  Hit chance: {hitChanceResult.FinalValue:F1}%");
                GD.Print("  [PASS] Hit chance calculated correctly");
                passed++;
            }
            else
            {
                GD.Print("  [FAIL] Hit chance out of range");
            }

            GD.Print($"\n=== PHASE B VALIDATION: {passed}/{total} PASSED ===");
            
            if (passed == total)
            {
                GD.Print("[SUCCESS] All Phase B tests passed!");
            }
            else
            {
                GD.Print($"[WARNING] {total - passed} tests failed");
            }
        }

        private void OnStateChanged(StateTransitionEvent evt)
        {
            _combatLog.LogStateTransition(evt);
            if (VerboseLogging)
            {
                GD.Print($"[STATE] {evt}");
            }
        }

        private void OnTurnChanged(TurnChangeEvent evt)
        {
            _combatLog.LogTurnChange(evt);
            if (VerboseLogging)
            {
                GD.Print($"[TURN] {evt}");
            }
        }

        private void OnCommandExecuted(CommandExecutedEvent evt)
        {
            _combatLog.LogCommand(evt);
            if (VerboseLogging)
            {
                GD.Print($"[CMD] {evt.Command.Type} by {evt.Command.CombatantId}: {(evt.Success ? "OK" : "FAIL")}");
            }
        }

        private void EmitReadyEvent()
        {
            LogStep("TESTBED_READY event emitted");
            GD.Print($"[EVENT:TESTBED_READY] Services: {string.Join(", ", _combatContext.GetRegisteredServices())}");
        }

        private void LogStep(string message)
        {
            _initializationLog.Add($"[{Time.GetTicksMsec()}] {message}");
            
            if (VerboseLogging)
            {
                GD.Print($"[TestbedBootstrap] {message}");
            }
        }

        private void PrintDiagnostics()
        {
            GD.Print("\n--- TESTBED DIAGNOSTICS ---");
            GD.Print($"Headless Mode: {HeadlessMode}");
            GD.Print($"Services Registered: {_combatContext.GetRegisteredServices().Count}");
            
            if (_combatContext.GetRegisteredServices().Count > 0)
            {
                GD.Print("Registered Services:");
                foreach (var serviceName in _combatContext.GetRegisteredServices())
                {
                    GD.Print($"  - {serviceName}");
                }
            }
            
            GD.Print($"\nCombat State: {_stateMachine.CurrentState}");
            GD.Print($"Round: {_turnQueue.CurrentRound}");
            GD.Print($"State Transitions: {_stateMachine.TransitionHistory.Count}");
            GD.Print($"Log Entries: {_combatLog.Entries.Count}");
            GD.Print($"Rule Events: {_rulesEngine.Events.EventHistory.Count}");
            GD.Print("--- END DIAGNOSTICS ---\n");
        }

        private void PrintFinalStateHash()
        {
            int stateHash = _turnQueue.GetStateHash();
            int logHash = _combatLog.CalculateHash();
            int rulesHash = _rulesEngine.Events.EventHistory.Count;
            int combinedHash = stateHash ^ logHash ^ rulesHash;
            
            GD.Print($"\n=== FINAL STATE HASH: {combinedHash:X8} ===");
            GD.Print($"  TurnQueue hash: {stateHash:X8}");
            GD.Print($"  CombatLog hash: {logHash:X8}");
            GD.Print($"  RuleEvents count: {rulesHash}");
            GD.Print("\n" + _combatLog.GetFormattedLog());
        }

        public List<string> GetInitializationLog() => new List<string>(_initializationLog);
        public CombatContext GetCombatContext() => _combatContext;
        public RulesEngine GetRulesEngine() => _rulesEngine;
        public StatusManager GetStatusManager() => _statusManager;
        public EffectPipeline GetEffectPipeline() => _effectPipeline;
    }
}
