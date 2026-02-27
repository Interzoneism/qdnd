using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Functors;
using QDND.Combat.Services;
using QDND.Combat.Statuses;
using QDND.Combat.Entities;
using QDND.Data.Actions;
using QDND.Data.CharacterModel;
using QDND.Data.Interrupts;
using QDND.Data.Passives;
using QDND.Data.Stats;
using QDND.Data.Statuses;

namespace QDND.Data
{
    /// <summary>
    /// Initializes all data registries and wires the BG3 functor/status pipeline.
    /// Called once during CombatArena startup to keep RegisterServices() focused on orchestration.
    /// </summary>
    public static class RegistryInitializer
    {
        /// <summary>Result of Bootstrap() — all created registries/services for CombatArena to store.</summary>
        public struct BootstrapResult
        {
            public DataRegistry DataRegistry;
            public CharacterDataRegistry CharRegistry;
            public RulesEngine RulesEngine;
            public StatusManager StatusManager;
            public MetamagicService MetamagicService;
            public ConcentrationSystem ConcentrationSystem;
            public EffectPipeline EffectPipeline;
            public ActionRegistry ActionRegistry;
            public StatsRegistry StatsRegistry;
            public StatusRegistry BG3StatusRegistry;
            public QDND.Combat.Statuses.BG3StatusIntegration BG3StatusIntegration;
            public PassiveRegistry PassiveRegistry;
            public InterruptRegistry InterruptRegistry;
            public FunctorExecutor FunctorExecutor;
        }

        /// <summary>
        /// Creates and wires all data registries needed by the combat system.
        /// Returns a BootstrapResult; CombatArena assigns each entry to its instance fields.
        /// </summary>
        /// <param name="dataPath">Absolute path to the Data directory (res://Data globalised).</param>
        /// <param name="bg3DataPath">Absolute path to the BG3_Data directory.</param>
        /// <param name="verboseLogging">Whether to emit verbose log messages.</param>
        /// <param name="scenarioLoader">Already-constructed ScenarioLoader to wire registries into.</param>
        /// <param name="combatContext">CombatContext to register services on.</param>
        /// <param name="log">Normal log delegate (maps to CombatArena.Log).</param>
        /// <param name="logError">Error log delegate (maps to GD.PrintErr).</param>
        /// <param name="resolveCombatant">Delegate to look up a Combatant by id at call-time.</param>
        /// <param name="getAllCombatantIds">Delegate that returns all current combatant ids.</param>
        /// <param name="removeSurfacesByCreator">Delegate to remove surfaces by creator id.</param>
        /// <param name="removeSurfaceById">Delegate to remove a surface by instance id.</param>
        public static BootstrapResult Bootstrap(
            string dataPath,
            string bg3DataPath,
            bool verboseLogging,
            ScenarioLoader scenarioLoader,
            ICombatContext combatContext,
            Action<string> log,
            Action<string> logError,
            Func<string, Combatant> resolveCombatant,
            Func<IEnumerable<string>> getAllCombatantIds,
            Action<string> removeSurfacesByCreator,
            Action<string> removeSurfaceById,
            Action<string> removeSummonsByOwner = null)
        {
            var r = new BootstrapResult();

            // DataRegistry
            r.DataRegistry = new DataRegistry();
            r.DataRegistry.LoadFromDirectory(dataPath);
            r.DataRegistry.ValidateOrThrow();
            scenarioLoader.SetDataRegistry(r.DataRegistry);

            // Load BG3 character data (races, classes, feats, equipment) using BG3DataLoader
            r.CharRegistry = new CharacterDataRegistry();
            BG3DataLoader.LoadAll(r.CharRegistry);
            r.CharRegistry.PrintStats();
            combatContext.RegisterService(r.CharRegistry);
            scenarioLoader.SetCharacterDataRegistry(r.CharRegistry);

            r.RulesEngine = new RulesEngine(42);

            r.StatusManager = new StatusManager(r.RulesEngine);
            r.RulesEngine.BlockDexFromACCheck = id =>
                r.StatusManager.GetStatuses(id).Any(s => s.Definition.BlockDexFromAC);

            r.MetamagicService = new MetamagicService(r.RulesEngine);
            combatContext.RegisterService(r.MetamagicService);
            foreach (var statusDef in r.DataRegistry.GetAllStatuses())
            {
                r.StatusManager.RegisterStatus(statusDef);
            }

            r.ConcentrationSystem = new ConcentrationSystem(r.StatusManager, r.RulesEngine)
            {
                ResolveCombatant = resolveCombatant
            };
            r.ConcentrationSystem.RemoveSurfacesByCreator = removeSurfacesByCreator;
            r.ConcentrationSystem.RemoveSurfaceById = removeSurfaceById;
            r.ConcentrationSystem.RemoveSummonsByOwner = removeSummonsByOwner;

            r.EffectPipeline = new EffectPipeline
            {
                Rules = r.RulesEngine,
                Statuses = r.StatusManager,
                Concentration = r.ConcentrationSystem
            };
            // OnAbilityExecuted will be wired after _actionExecutionService is created (end of RegisterServices)

            // Initialize centralized Action Registry with BG3 spells
            r.ActionRegistry = new ActionRegistry();
            var initResult = ActionRegistryInitializer.Initialize(
                r.ActionRegistry,
                bg3DataPath,
                verboseLogging: verboseLogging);

            if (!initResult.Success)
            {
                logError($"[CombatArena] Failed to initialize action registry: {initResult.ErrorMessage}");
            }
            else
            {
                log($"Action Registry initialized: {initResult.ActionsLoaded} actions loaded in {initResult.LoadTimeMs}ms");
                if (initResult.ErrorCount > 0)
                    logError($"[CombatArena] Action Registry had {initResult.ErrorCount} errors during initialization");
                if (initResult.WarningCount > 0 && verboseLogging)
                    log($"Action Registry had {initResult.WarningCount} warnings during initialization");
            }

            // Wire ActionRegistry into EffectPipeline
            r.EffectPipeline.ActionRegistry = r.ActionRegistry;
            combatContext.RegisterService(r.ActionRegistry);

            // Initialize BG3 Stats Registry (Characters, Weapons, Armor)
            r.StatsRegistry = new StatsRegistry();
            string bg3StatsPath = Path.Combine(bg3DataPath, "Stats");
            r.StatsRegistry.LoadFromDirectory(bg3StatsPath);
            log($"Stats Registry: {r.StatsRegistry.CharacterCount} characters, {r.StatsRegistry.WeaponCount} weapons, {r.StatsRegistry.ArmorCount} armors");
            combatContext.RegisterService(r.StatsRegistry);
            scenarioLoader.SetStatsRegistry(r.StatsRegistry);
            scenarioLoader.SetActionRegistry(r.ActionRegistry);

            // Initialize BG3 Status Registry with boost bridge
            r.BG3StatusRegistry = new StatusRegistry();
            r.BG3StatusIntegration = new QDND.Combat.Statuses.BG3StatusIntegration(r.StatusManager, r.BG3StatusRegistry);
            string bg3StatusPath = Path.Combine(bg3DataPath, "Statuses");
            int statusCount = r.BG3StatusIntegration.LoadBG3Statuses(bg3StatusPath);
            log($"BG3 Status Registry: {statusCount} statuses loaded and registered with StatusManager");
            combatContext.RegisterService(r.BG3StatusRegistry);

            // Initialize BG3 Passive Registry
            r.PassiveRegistry = new PassiveRegistry();
            string passiveFile = Path.Combine(bg3StatsPath, "Passive.txt");
            int passiveCount = r.PassiveRegistry.LoadPassives(passiveFile);
            log($"Passive Registry: {passiveCount} passives loaded");
            combatContext.RegisterService(r.PassiveRegistry);

            // Initialize BG3 Interrupt Registry
            r.InterruptRegistry = new InterruptRegistry();
            string interruptFile = Path.Combine(bg3StatsPath, "Interrupt.txt");
            int interruptCount = r.InterruptRegistry.LoadInterrupts(interruptFile);
            log($"Interrupt Registry: {interruptCount} interrupts loaded");
            combatContext.RegisterService(r.InterruptRegistry);

            // Wire FunctorExecutor for BG3 status/passive functor execution
            r.FunctorExecutor = new FunctorExecutor(r.RulesEngine, r.StatusManager);
            r.FunctorExecutor.ResolveCombatant = resolveCombatant;

            // Wire StatusFunctorIntegration (OnApply/OnTick/OnRemove functors for BG3 statuses)
            var statusFunctorIntegration = new StatusFunctorIntegration(r.StatusManager, r.BG3StatusRegistry, r.FunctorExecutor);
            log("StatusFunctorIntegration wired for BG3 status lifecycle functors");

            // Wire PassiveFunctorIntegration (event-driven passive effects like GWM bonus damage)
            var passiveFunctorIntegration = new PassiveFunctorIntegration(r.RulesEngine, r.PassiveRegistry, r.FunctorExecutor);
            passiveFunctorIntegration.ResolveCombatant = resolveCombatant;
            passiveFunctorIntegration.GetAllCombatantIds = getAllCombatantIds;
            log("PassiveFunctorIntegration wired for event-driven passive functors");

            // Register actions from ActionRegistry into effect pipeline
            foreach (var abilityDef in r.ActionRegistry.GetAllActions())
            {
                r.EffectPipeline.RegisterAction(abilityDef);
            }

            // Initialize Summon Template Registry
            string summonTemplatePath = Path.Combine(dataPath, "Scenarios", "summon_templates.json");
            SummonTemplateRegistry.Initialize(summonTemplatePath);
            log($"Summon Template Registry: {SummonTemplateRegistry.Count} templates loaded");

            // Phase C+: On-Hit Trigger System
            var onHitTriggerService = new OnHitTriggerService();
            OnHitTriggers.RegisterAll(onHitTriggerService, r.StatusManager, r.ConcentrationSystem);
            r.EffectPipeline.OnHitTriggerService = onHitTriggerService;

            return r;
        }
    }
}
