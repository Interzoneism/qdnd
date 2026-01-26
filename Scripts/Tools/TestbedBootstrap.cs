using Godot;
using System;
using System.Collections.Generic;
using QDND.Combat.Services;

namespace QDND.Tools
{
    /// <summary>
    /// Bootstrap script for Testbed.tscn. Initializes CombatContext, registers services,
    /// loads a default scenario, and emits a ready event with diagnostic information.
    /// This is the entry point for all combat system testing and validation.
    /// </summary>
    public partial class TestbedBootstrap : Node3D
    {
        [Export] public bool HeadlessMode = false;
        [Export] public string DefaultScenarioPath = "";
        [Export] public bool VerboseLogging = true;

        private CombatContext _combatContext;
        private List<string> _initializationLog = new List<string>();

        public override void _Ready()
        {
            if (VerboseLogging)
            {
                GD.Print("=== TESTBED BOOTSTRAP START ===");
            }

            LogStep("Testbed bootstrap initiated");
            
            // Create and register CombatContext
            InitializeCombatContext();
            
            // Register core services (Phase A will add more)
            RegisterCoreServices();
            
            // Load default scenario if specified
            if (!string.IsNullOrEmpty(DefaultScenarioPath))
            {
                LoadScenario(DefaultScenarioPath);
            }
            else
            {
                LogStep("No default scenario specified, Testbed ready in empty state");
            }

            // Emit ready event with diagnostic information
            EmitReadyEvent();

            if (VerboseLogging)
            {
                GD.Print("=== TESTBED BOOTSTRAP COMPLETE ===");
                PrintDiagnostics();
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
            
            // Phase A services will be added here as they are implemented
            // Example:
            // var combatStateMachine = new CombatStateMachine();
            // _combatContext.RegisterService(combatStateMachine);
            
            LogStep($"Core services registered: {_combatContext.GetRegisteredServices().Count}");
        }

        private void LoadScenario(string scenarioPath)
        {
            LogStep($"Loading scenario: {scenarioPath}");
            
            // Scenario loading will be implemented in Phase A
            // For now, just log the intent
            LogStep("Scenario loading system not yet implemented (Phase A pending)");
        }

        private void EmitReadyEvent()
        {
            var eventData = new Dictionary<string, object>
            {
                { "timestamp", Time.GetTicksMsec() },
                { "headless_mode", HeadlessMode },
                { "registered_services", _combatContext.GetRegisteredServices() },
                { "initialization_log", new List<string>(_initializationLog) }
            };

            LogStep("TESTBED_READY event emitted");
            
            // Emit structured event (can be captured by tests)
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
            
            GD.Print("\nInitialization Steps:");
            foreach (var step in _initializationLog)
            {
                GD.Print($"  {step}");
            }
            GD.Print("--- END DIAGNOSTICS ---\n");
        }

        /// <summary>
        /// Get the initialization log for testing purposes.
        /// </summary>
        public List<string> GetInitializationLog()
        {
            return new List<string>(_initializationLog);
        }

        /// <summary>
        /// Get the combat context instance for testing.
        /// </summary>
        public CombatContext GetCombatContext()
        {
            return _combatContext;
        }
    }
}
