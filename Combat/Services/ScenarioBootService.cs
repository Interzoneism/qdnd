using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.AI;
using QDND.Combat.Arena;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Movement;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Boosts;
using QDND.Combat.Statuses;
using QDND.Data;
using QDND.Data.CharacterModel;
using QDND.Data.Passives;
using QDND.Tools.AutoBattler;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Seed state and dynamic-scenario configuration passed to ScenarioBootService at construction.
    /// </summary>
    public class ScenarioBootConfig
    {
        public int? ScenarioSeedOverride;
        public int? AutoBattleSeedOverride;
        public int RandomSeed;
        public int ResolvedScenarioSeed;
        public ScenarioBootService.DynamicScenarioMode DynamicMode;
        public string DynamicActionTestId;
        public List<string> DynamicActionBatchIds;
        public int DynamicCharacterLevel;
        public int DynamicTeamSize;
        public AutoBattleConfig AutoBattleConfig;
    }

    /// <summary>
    /// Godot-node references needed by ScenarioBootService when spawning combatant 3D visuals.
    /// </summary>
    public class ScenarioBootVisuals
    {
        public CombatArena Arena;
        public Node3D CombatantsContainer;
        public Dictionary<string, CombatantVisual> CombatantVisuals;
        public float TileSize;
    }

    /// <summary>
    /// Handles the one-shot boot phase of a combat encounter:
    ///   • Loading the scenario definition (from file, generator, or hard-coded default)
    ///   • Wiring combatant services (passives, inventories, reactions, LOS, …)
    ///   • Spawning the 3D visual nodes for each combatant
    ///
    /// Extracted from CombatArena so that arena code stays focused on orchestration.
    /// CombatArena keeps thin forwarding methods and reads Combatants/Rng/ResolvedScenarioSeed
    /// back via public properties after each call.
    /// </summary>
    public class ScenarioBootService
    {
        // ─── Nested types ────────────────────────────────────────────────────────

        public enum DynamicScenarioMode
        {
            None,
            ActionTest,
            ShortGameplay,
            TeamBattle,
            ActionBatch
        }

        // ─── Dependencies ────────────────────────────────────────────────────────

        private readonly ICombatContext _combatContext;
        private readonly QDND.Combat.Rules.Functors.FunctorExecutor _functorExecutor;
        private readonly ForcedMovementService _forcedMovementService;
        private readonly AutoBattleConfig _autoBattleConfig;
        private readonly CharacterDataRegistry _characterDataRegistry;
        private readonly ActionRegistry _actionRegistry;
        private readonly EffectPipeline _effectPipeline;
        private readonly AIDecisionPipeline _aiPipeline;
        private readonly ScenarioLoader _scenarioLoader;
        private readonly TurnQueueService _turnQueue;
        private readonly PassiveRegistry _passiveRegistry;
        private readonly MetamagicService _metamagicService;
        private readonly StatusManager _statusManager;
        private readonly CombatLog _combatLog;
        private readonly InventoryService _inventoryService;
        private readonly LOSService _losService;

        // Callbacks for helpers that remain in CombatArena (they depend on arena-private state).
        private readonly Action<IEnumerable<Combatant>> _applyDefaultMovement;
        private readonly Action<IEnumerable<Combatant>> _grantBaselineReactions;

        private readonly Action<string> _log;
        private readonly HashSet<string> _oneTimeLogKeys;

        // ─── Seed / dynamic-mode config ──────────────────────────────────────────

        private readonly int? _scenarioSeedOverride;
        private readonly int? _autoBattleSeedOverride;
        private readonly DynamicScenarioMode _dynamicScenarioMode;
        private readonly string _dynamicActionTestId;
        private readonly List<string> _dynamicActionBatchIds;
        private readonly int _dynamicCharacterLevel;
        private readonly int _dynamicTeamSize;

        // ─── Visual-spawning refs ────────────────────────────────────────────────

        private readonly CombatArena _arena;
        private readonly Node3D _combatantsContainer;
        private readonly Dictionary<string, CombatantVisual> _combatantVisuals;
        private readonly float _tileSize;

        // ─── Output state (read by CombatArena after each Load call) ─────────────

        /// <summary>Combatant list produced by the most recent Load call.</summary>
        public List<Combatant> Combatants { get; private set; }

        /// <summary>Seeded RNG produced by the most recent Load call.</summary>
        public Random Rng { get; private set; }

        /// <summary>Scenario seed resolved during the most recent Load call.</summary>
        public int ResolvedScenarioSeed { get; private set; }

        /// <summary>
        /// Value that should be written back to CombatArena.RandomSeed after a Load call.
        /// Some load paths change the seed (e.g. random/dynamic scenarios).
        /// </summary>
        public int ResolvedRandomSeed { get; private set; }

        // ─── Constructor ─────────────────────────────────────────────────────────

        public ScenarioBootService(
            ICombatContext combatContext,
            QDND.Combat.Rules.Functors.FunctorExecutor functorExecutor,
            ForcedMovementService forcedMovementService,
            CharacterDataRegistry characterDataRegistry,
            ActionRegistry actionRegistry,
            EffectPipeline effectPipeline,
            AIDecisionPipeline aiPipeline,
            ScenarioLoader scenarioLoader,
            TurnQueueService turnQueue,
            PassiveRegistry passiveRegistry,
            MetamagicService metamagicService,
            StatusManager statusManager,
            CombatLog combatLog,
            InventoryService inventoryService,
            LOSService losService,
            Action<IEnumerable<Combatant>> applyDefaultMovement,
            Action<IEnumerable<Combatant>> grantBaselineReactions,
            Action<string> log,
            HashSet<string> oneTimeLogKeys,
            ScenarioBootConfig config,
            ScenarioBootVisuals visuals)
        {
            _combatContext = combatContext;
            _functorExecutor = functorExecutor;
            _forcedMovementService = forcedMovementService;
            _autoBattleConfig = config.AutoBattleConfig;
            _characterDataRegistry = characterDataRegistry;
            _actionRegistry = actionRegistry;
            _effectPipeline = effectPipeline;
            _aiPipeline = aiPipeline;
            _scenarioLoader = scenarioLoader;
            _turnQueue = turnQueue;
            _passiveRegistry = passiveRegistry;
            _metamagicService = metamagicService;
            _statusManager = statusManager;
            _combatLog = combatLog;
            _inventoryService = inventoryService;
            _losService = losService;
            _applyDefaultMovement = applyDefaultMovement;
            _grantBaselineReactions = grantBaselineReactions;
            _log = log;
            _oneTimeLogKeys = oneTimeLogKeys;

            _scenarioSeedOverride = config.ScenarioSeedOverride;
            _autoBattleSeedOverride = config.AutoBattleSeedOverride;
            ResolvedRandomSeed = config.RandomSeed;
            ResolvedScenarioSeed = config.ResolvedScenarioSeed;

            _dynamicScenarioMode = config.DynamicMode;
            _dynamicActionTestId = config.DynamicActionTestId;
            _dynamicActionBatchIds = config.DynamicActionBatchIds;
            _dynamicCharacterLevel = config.DynamicCharacterLevel;
            _dynamicTeamSize = config.DynamicTeamSize;

            _arena = visuals.Arena;
            _combatantsContainer = visuals.CombatantsContainer;
            _combatantVisuals = visuals.CombatantVisuals;
            _tileSize = visuals.TileSize;
        }

        // ─── Scenario loading ─────────────────────────────────────────────────────

        /// <summary>
        /// Loads a randomly generated 2v2 scenario using ScenarioGenerator.
        /// </summary>
        public void LoadRandomScenario()
        {
            int seed = _scenarioSeedOverride ?? _autoBattleSeedOverride ?? (ResolvedRandomSeed != 0 ? ResolvedRandomSeed : GenerateRuntimeSeed());
            ResolvedRandomSeed = seed;

            var scenarioGenerator = new ScenarioGenerator(_characterDataRegistry, seed);
            var scenario = scenarioGenerator.GenerateRandomScenario(2, 2);
            // Random 2v2 should use the baseline starter kit for fairness.
            LoadScenarioDefinition(scenario, "random scenario", useBaseStarterKit: true);
        }

        /// <summary>
        /// Returns true if the given action ID is a known valid ability.
        /// </summary>
        public bool IsKnownAbilityId(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return false;

            string normalized = actionId.Trim();
            if (_actionRegistry?.GetAction(normalized) != null)
                return true;

            return normalized.Equals("main_hand_attack", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("ranged_attack", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("dodge_action", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Target_MainHandAttack", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Projectile_MainHandAttack", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Shout_Dodge", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Loads one of the dynamic full-fidelity scenarios based on the configured DynamicScenarioMode.
        /// </summary>
        public void LoadDynamicScenario()
        {
            if (_characterDataRegistry == null)
                throw new InvalidOperationException("CharacterDataRegistry service is unavailable.");

            int scenarioSeed = _scenarioSeedOverride ?? (ResolvedScenarioSeed != 0 ? ResolvedScenarioSeed : GenerateRuntimeSeed());
            ResolvedScenarioSeed = scenarioSeed;
            ResolvedRandomSeed = scenarioSeed;

            var scenarioGenerator = new ScenarioGenerator(_characterDataRegistry, scenarioSeed);
            ScenarioDefinition scenario = _dynamicScenarioMode switch
            {
                DynamicScenarioMode.ActionTest => BuildActionTestScenario(scenarioGenerator),
                DynamicScenarioMode.ActionBatch => BuildActionBatchScenario(scenarioGenerator),
                DynamicScenarioMode.ShortGameplay => scenarioGenerator.GenerateShortGameplayScenario(_dynamicCharacterLevel),
                DynamicScenarioMode.TeamBattle => scenarioGenerator.GenerateRandomScenario(_dynamicTeamSize, _dynamicTeamSize, _dynamicCharacterLevel),
                _ => throw new InvalidOperationException("Dynamic scenario mode was not set.")
            };

            LoadScenarioDefinition(scenario, $"dynamic {_dynamicScenarioMode}");

            // Activate tag-based test policy for action-testing modes so that
            // ability_test_actor-tagged combatants bypass requirement/resource checks.
            bool isTestMode = _dynamicScenarioMode == DynamicScenarioMode.ActionTest
                           || _dynamicScenarioMode == DynamicScenarioMode.ActionBatch;
            if (isTestMode)
            {
                var testPolicy = new TagBasedAbilityTestPolicy();
                var effectPipeline = _effectPipeline;
                var aiPipeline = _aiPipeline;
                if (effectPipeline == null || aiPipeline == null)
                    GD.PushWarning("[ScenarioBootService] TestPolicy not set: one or both pipelines unavailable.");
                if (effectPipeline != null) effectPipeline.TestPolicy = testPolicy;
                if (aiPipeline != null) aiPipeline.TestPolicy = testPolicy;
            }
        }

        private ScenarioDefinition BuildActionTestScenario(ScenarioGenerator scenarioGenerator)
        {
            if (string.IsNullOrWhiteSpace(_dynamicActionTestId))
            {
                throw new InvalidOperationException("Action test mode requires --ff-action-test <action_id>.");
            }

            if (!IsKnownAbilityId(_dynamicActionTestId))
            {
                throw new InvalidOperationException(
                    $"Action '{_dynamicActionTestId}' was not found in loaded actions. " +
                    "Use a valid action id from Data/Actions.");
            }

            return scenarioGenerator.GenerateActionTestScenario(_dynamicActionTestId, _dynamicCharacterLevel, _actionRegistry);
        }

        private ScenarioDefinition BuildActionBatchScenario(ScenarioGenerator scenarioGenerator)
        {
#if !DEBUG
            GD.PushError("[ScenarioBootService] Action batch mode requires a DEBUG build (test_dummy assets not available in Release).");
            return null;
#endif
            if (_dynamicActionBatchIds == null || _dynamicActionBatchIds.Count == 0)
            {
                throw new InvalidOperationException("Action batch mode requires --ff-action-batch id1,id2,...");
            }

            // Validate all action IDs
            var invalidIds = _dynamicActionBatchIds.Where(id => !IsKnownAbilityId(id)).ToList();
            if (invalidIds.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Unknown action IDs in batch: {string.Join(", ", invalidIds)}. " +
                    "Use valid action ids from Data/Actions.");
            }

            return scenarioGenerator.GenerateMultiActionTestScenario(_dynamicActionBatchIds, _dynamicCharacterLevel, _actionRegistry);
        }

        /// <summary>
        /// Loads a scenario from a JSON file on disk.
        /// </summary>
        public void LoadScenario(string path)
        {
            var scenario = _scenarioLoader.LoadFromFile(path);
            if (_scenarioSeedOverride.HasValue)
            {
                scenario.Seed = _scenarioSeedOverride.Value;
            }
            LoadScenarioDefinition(scenario, $"scenario file {path}");
        }

        /// <summary>
        /// Core boot routine: wires all combatant services and resolves seeds.
        /// Sets Combatants, Rng, ResolvedScenarioSeed.
        /// </summary>
        public void LoadScenarioDefinition(ScenarioDefinition scenario, string sourceLabel, bool useBaseStarterKit = false)
        {
            if (scenario == null)
            {
                throw new InvalidOperationException("Scenario definition is null.");
            }

            if (scenario.Units == null || scenario.Units.Count == 0)
            {
                throw new InvalidOperationException($"Scenario '{scenario.Id ?? sourceLabel}' has no units.");
            }

            var scenarioLoader = _scenarioLoader;
            var turnQueue = _turnQueue;
            var passiveRegistry = _passiveRegistry;
            var metamagicService = _metamagicService;
            var statusManager = _statusManager;
            var effectPipeline = _effectPipeline;
            var aiPipeline = _aiPipeline;
            var combatLog = _combatLog;

            _oneTimeLogKeys.Clear();
            Combatants = scenarioLoader.SpawnCombatants(scenario, turnQueue);
            // Assign random placeholder portraits to all combatants
            // TODO: Replace with proper character-specific portraits
            PortraitAssigner.AssignRandomPortraits(Combatants, scenario.Seed);
            _applyDefaultMovement(Combatants);
            _grantBaselineReactions(Combatants);

            // Grant BG3 passives to all combatants (applies their boosts)
            if (passiveRegistry != null)
            {
                int totalPassivesGranted = 0;
                foreach (var c in Combatants)
                {
                    // Wire FunctorExecutor to PassiveManager for toggle functor execution
                    if (_functorExecutor != null)
                    {
                        c.PassiveManager.SetFunctorExecutor(_functorExecutor);
                    }

                    foreach (var passiveId in c.PassiveIds)
                    {
                        if (string.Equals(passiveId, "aura_of_protection", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (c.PassiveManager.GrantPassive(passiveRegistry, passiveId))
                            totalPassivesGranted++;
                    }
                }
                if (totalPassivesGranted > 0)
                    _log($"Granted {totalPassivesGranted} BG3 passives across {Combatants.Count} combatants");
            }

            // Paladin Aura of Protection: register per-paladin aura status definitions at combat start.
            if (statusManager != null)
            {
                foreach (var c in Combatants)
                {
                    bool hasAuraOfProtection = c.PassiveIds?.Any(p =>
                        string.Equals(p, "aura_of_protection", StringComparison.OrdinalIgnoreCase)) == true;
                    if (!hasAuraOfProtection)
                        continue;

                    int chaMod = Math.Max(1, c.GetAbilityModifier(AbilityType.Charisma));
                    int paladinLevel = c.ResolvedCharacter?.Sheet?.ClassLevels?.Count(cl =>
                        string.Equals(cl.ClassId, "paladin", StringComparison.OrdinalIgnoreCase)) ?? 0;
                    float auraRadius = paladinLevel >= 18 ? 9f : 3f;

                    string buffId = $"aura_of_protection_buff_{c.Id}";
                    string auraId = $"aura_of_protection_{c.Id}";

                    var buffDef = new StatusDefinition
                    {
                        Id = buffId,
                        Name = "Aura of Protection",
                        Description = $"Saving throw bonus +{chaMod} from Paladin's Aura of Protection.",
                        IsBuff = true,
                        DefaultDuration = 1,
                        DurationType = DurationType.Turns,
                        Stacking = StackingBehavior.Refresh,
                    };
                    buffDef.Modifiers.Add(new StatusModifier
                    {
                        Target = ModifierTarget.SavingThrow,
                        Type = ModifierType.Flat,
                        Value = chaMod
                    });

                    var auraDef = new StatusDefinition
                    {
                        Id = auraId,
                        Name = "Aura of Protection (Source)",
                        IsBuff = true,
                        DefaultDuration = 0,
                        DurationType = DurationType.Permanent,
                        Stacking = StackingBehavior.Unique,
                        AuraRadius = auraRadius,
                        AuraStatusId = buffId,
                        AuraAffectsEnemiesOnly = false,
                        AuraAffectsAlliesOnly = true,
                    };

                    statusManager.RegisterStatus(buffDef);
                    statusManager.RegisterStatus(auraDef);
                    statusManager.ApplyStatus(auraId, c.Id, c.Id, duration: 0);

                    // Apply self-buff so the paladin benefits immediately.
                    statusManager.ApplyStatus(buffId, c.Id, c.Id, duration: 0);
                }
            }

            // Grant metamagic options from passive IDs to sorcerer combatants
            if (metamagicService != null)
            {
                foreach (var c in Combatants)
                {
                    foreach (var passiveId in c.PassiveIds)
                    {
                        metamagicService.GrantFromPassiveId(c.Id, passiveId);
                    }
                }
            }

            // Ki-Empowered Strikes: Monks L6+ get all_magical status (attacks count as magical)
            if (statusManager != null)
            {
                foreach (var c in Combatants)
                {
                    bool hasKiEmpowered = c.ResolvedCharacter?.Features?.Any(f =>
                        string.Equals(f.Id, "ki_empowered_strikes", StringComparison.OrdinalIgnoreCase)) == true;
                    if (hasKiEmpowered)
                    {
                        statusManager.ApplyStatus("all_magical", c.Id, c.Id, duration: 0, stacks: 1);
                    }
                }
            }

            // War Caster: advantage on CON saves to maintain concentration
            foreach (var c in Combatants)
            {
                bool hasWarCaster = c.ResolvedCharacter?.Sheet?.FeatIds?.Any(f =>
                    string.Equals(f, "war_caster", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(f, "warcaster", StringComparison.OrdinalIgnoreCase)) == true;
                if (hasWarCaster)
                {
                    BoostApplicator.ApplyBoosts(c, "Advantage(Concentration)", "Feat", "war_caster");
                }

                // Shield Master: +2 to DEX saving throws when wielding a shield
                bool hasShieldMaster = c.ResolvedCharacter?.Sheet?.FeatIds?.Any(f =>
                    string.Equals(f, "shield_master", StringComparison.OrdinalIgnoreCase)) == true;
                if (hasShieldMaster && c.HasShield)
                {
                    BoostApplicator.ApplyBoosts(c, "Bonus(SavingThrow,Dexterity,2)", "Feat", "shield_master");
                }

                // Heavy Armor Master: DR 3 vs non-magical Slashing/Piercing/Bludgeoning in heavy armor
                bool hasHAM = c.ResolvedCharacter?.Sheet?.FeatIds?.Any(f =>
                    string.Equals(f, "heavy_armour_master", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(f, "heavy_armor_master", StringComparison.OrdinalIgnoreCase)) == true;
                if (hasHAM && c.EquippedArmor?.Category == ArmorCategory.Heavy)
                {
                    BoostApplicator.ApplyBoosts(c,
                        "DamageReduction(Slashing,3);DamageReduction(Piercing,3);DamageReduction(Bludgeoning,3)",
                        "Feat", "heavy_armor_master");
                }
            }

            // Initialize inventories for all combatants
            var inventoryService = _inventoryService;
            if (inventoryService != null)
            {
                foreach (var c in Combatants)
                    inventoryService.InitializeFromCombatant(c, includeExtendedStarterGear: !useBaseStarterKit);
                _log($"Initialized inventories for {Combatants.Count} combatants");
            }

            int scenarioSeed = scenario.Seed;
            int aiSeed = _autoBattleSeedOverride ?? scenarioSeed;
            ResolvedScenarioSeed = scenarioSeed;

            Rng = new Random(scenarioSeed);
            effectPipeline.Rng = Rng;
            aiPipeline?.SetRandomSeed(aiSeed);
            if (_autoBattleConfig != null)
            {
                _autoBattleConfig.Seed = aiSeed;
            }

            var losService = _losService;
            _combatContext.Combatants.ReplaceAll(Combatants);
            losService?.ClearCombatants();
            _forcedMovementService?.ClearCombatants();
            foreach (var c in Combatants)
            {
                losService?.RegisterCombatant(c);
                _forcedMovementService?.RegisterCombatant(c);
            }

            combatLog.LogCombatStart(Combatants.Count, scenarioSeed);
            _log($"Loaded {sourceLabel}: {Combatants.Count} combatants (scenario seed {scenarioSeed}, AI seed {aiSeed})");
        }

        // ─── Visual spawning ──────────────────────────────────────────────────────

        /// <summary>
        /// Spawns 3D visuals for every combatant in Combatants.
        /// Call after a successful Load method.
        /// </summary>
        public void SpawnCombatantVisuals()
        {
            foreach (var combatant in Combatants)
            {
                SpawnVisualForCombatant(combatant);
            }
        }

        public void SpawnVisualForCombatant(Combatant combatant)
        {
            if (_combatantsContainer == null || _arena == null)
            {
                GD.PushError($"[ScenarioBootService] Cannot spawn visual for {combatant.Name}: not initialized");
                return;
            }

            // Check if visual already exists
            if (_combatantVisuals.ContainsKey(combatant.Id))
            {
                RuntimeSafety.Log($"[ScenarioBootService] Visual already exists for {combatant.Id}");
                return;
            }

            CombatantVisual visual;

            if (_arena.CombatantVisualScene != null)
            {
                _log($"Instantiating visual from scene for {combatant.Name}");
                visual = _arena.CombatantVisualScene.Instantiate<CombatantVisual>();
            }
            else
            {
                _log($"Creating visual programmatically for {combatant.Name}");
                // Create a basic visual programmatically
                visual = new CombatantVisual();
            }

            visual.Initialize(combatant, _arena);
            visual.Position = CombatantPositionToWorld(combatant.Position);
            visual.Name = $"Visual_{combatant.Id}";

            _combatantsContainer.AddChild(visual);
            _combatantVisuals[combatant.Id] = visual;

            _log($"Spawned visual for {combatant.Name} at {visual.Position}, Layer: {visual.CollisionLayer}, InTree: {visual.IsInsideTree()}");
        }

        /// <summary>
        /// Removes and frees the 3D visual node for the given combatant.
        /// Called when a summon is unsummoned or dies.
        /// </summary>
        public void RemoveVisualForCombatant(string combatantId)
        {
            if (_combatantVisuals.TryGetValue(combatantId, out var visual))
            {
                _combatantVisuals.Remove(combatantId);
                visual.QueueFree();
                _log($"[ScenarioBootService] Removed visual for {combatantId}");
            }
        }

        private Vector3 CombatantPositionToWorld(Vector3 gridPos)
        {
            // Convert grid position to world position (identity with TileSize=1)
            return new Vector3(gridPos.X * _tileSize, gridPos.Y, gridPos.Z * _tileSize);
        }

        // ─── Utilities ────────────────────────────────────────────────────────────

        private static int GenerateRuntimeSeed()
        {
            return unchecked(System.Environment.TickCount ^ Guid.NewGuid().GetHashCode());
        }
    }
}
