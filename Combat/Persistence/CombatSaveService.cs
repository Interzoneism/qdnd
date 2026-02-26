using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Services;
using QDND.Combat.Entities;
using QDND.Combat.Statuses;
using QDND.Combat.Environment;
using QDND.Combat.Reactions;
using QDND.Combat.Actions;
using QDND.Combat.Rules;
using QDND.Combat.States;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Persistence
{
    /// <summary>
    /// Service for capturing and restoring complete combat state.
    /// </summary>
    public class CombatSaveService
    {
        /// <summary>
        /// Capture a complete snapshot of the current combat state.
        /// </summary>
        public CombatSnapshot CaptureSnapshot(ICombatContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var snapshot = new CombatSnapshot
            {
                Version = 1,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // Get services
            var rulesEngine = context.GetService<RulesEngine>();
            var turnQueue = context.GetService<TurnQueueService>();
            var stateMachine = context.GetService<CombatStateMachine>();
            var statusManager = context.GetService<StatusManager>();
            var surfaceManager = context.GetService<SurfaceManager>();
            var resolutionStack = context.GetService<ResolutionStack>();
            var effectPipeline = context.GetService<EffectPipeline>();

            // Capture RNG state
            if (rulesEngine != null)
            {
                snapshot.InitialSeed = rulesEngine.Seed;
                snapshot.RollIndex = rulesEngine.RollIndex;
            }

            // Capture combat flow state
            if (turnQueue != null)
            {
                snapshot.CurrentRound = turnQueue.CurrentRound;
                snapshot.CurrentTurnIndex = turnQueue.CurrentTurnIndex;
                snapshot.TurnOrder = turnQueue.ExportTurnOrder();
            }

            if (stateMachine != null)
            {
                snapshot.CombatState = stateMachine.ExportState();
            }

            // Capture combatants
            snapshot.Combatants = CaptureCombatants(context);

            // Capture subsystem state
            if (surfaceManager != null)
            {
                snapshot.Surfaces = surfaceManager.ExportState();
            }

            if (statusManager != null)
            {
                snapshot.ActiveStatuses = statusManager.ExportState();
            }

            if (resolutionStack != null)
            {
                snapshot.ResolutionStack = resolutionStack.ExportState();
            }

            if (effectPipeline != null)
            {
                snapshot.ActionCooldowns = effectPipeline.ExportCooldowns();

                // Export concentration state
                if (effectPipeline.Concentration != null)
                {
                    snapshot.ActiveConcentrations = effectPipeline.Concentration.ExportState();
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Restore combat state from a snapshot.
        /// </summary>
        public void RestoreSnapshot(ICombatContext context, CombatSnapshot snapshot)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            // Validate version
            if (snapshot.Version != 1)
            {
                throw new InvalidOperationException($"Unsupported snapshot version: {snapshot.Version}");
            }

            // Get services
            var rulesEngine = context.GetService<RulesEngine>();
            var turnQueue = context.GetService<TurnQueueService>();
            var stateMachine = context.GetService<CombatStateMachine>();
            var statusManager = context.GetService<StatusManager>();
            var surfaceManager = context.GetService<SurfaceManager>();
            var resolutionStack = context.GetService<ResolutionStack>();
            var effectPipeline = context.GetService<EffectPipeline>();

            // Restore RNG state
            if (rulesEngine != null)
            {
                rulesEngine.SetRngState(snapshot.InitialSeed, snapshot.RollIndex);
            }

            // Restore combatants
            RestoreCombatants(context, snapshot.Combatants, turnQueue);

            // Restore combat flow state
            if (turnQueue != null && snapshot.TurnOrder != null)
            {
                turnQueue.ImportTurnOrder(snapshot.TurnOrder, snapshot.CurrentTurnIndex, snapshot.CurrentRound);
            }

            if (stateMachine != null && !string.IsNullOrEmpty(snapshot.CombatState))
            {
                stateMachine.ImportState(snapshot.CombatState);
            }

            // Restore subsystem state (silent to avoid re-triggering events)
            if (surfaceManager != null && snapshot.Surfaces != null)
            {
                surfaceManager.ImportStateSilent(snapshot.Surfaces);
            }

            if (statusManager != null && snapshot.ActiveStatuses != null)
            {
                statusManager.ImportStateSilent(snapshot.ActiveStatuses);
            }

            if (resolutionStack != null && snapshot.ResolutionStack != null)
            {
                resolutionStack.ImportState(snapshot.ResolutionStack);
            }

            if (effectPipeline != null && snapshot.ActionCooldowns != null)
            {
                effectPipeline.ImportCooldowns(snapshot.ActionCooldowns);

                // Import concentration state
                if (effectPipeline.Concentration != null && snapshot.ActiveConcentrations != null)
                {
                    effectPipeline.Concentration.ImportState(snapshot.ActiveConcentrations);
                }
            }
        }

        // Private helpers

        private List<CombatantSnapshot> CaptureCombatants(ICombatContext context)
        {
            var snapshots = new List<CombatantSnapshot>();
            var inventoryService = context.GetService<InventoryService>();

            foreach (var combatant in context.GetAllCombatants())
            {
                // Capture all resources including leveled (spell slots)
                var resourceCurrent = new Dictionary<string, int>();
                var resourceMax = new Dictionary<string, int>();
                foreach (var kvp in combatant.ActionResources.Resources)
                {
                    var res = kvp.Value;
                    if (res.IsLeveled)
                    {
                        // Save each spell slot level separately with a composite key
                        foreach (var levelKvp in res.CurrentByLevel)
                        {
                            string levelKey = $"{kvp.Key}:L{levelKvp.Key}";
                            resourceCurrent[levelKey] = levelKvp.Value;
                            resourceMax[levelKey] = res.GetMax(levelKvp.Key);
                        }
                    }
                    else
                    {
                        resourceCurrent[kvp.Key] = res.Current;
                        resourceMax[kvp.Key] = res.Max;
                    }
                }

                // Capture equipment slots
                var equipmentSlots = new Dictionary<string, string>();
                if (inventoryService != null)
                {
                    var inv = inventoryService.GetInventory(combatant.Id);
                    if (inv != null)
                    {
                        foreach (var kvp in inv.EquippedItems)
                        {
                            if (kvp.Value != null && !string.IsNullOrEmpty(kvp.Value.DefinitionId))
                                equipmentSlots[kvp.Key.ToString()] = kvp.Value.DefinitionId;
                        }
                    }
                }

                var snapshot = new CombatantSnapshot
                {
                    Id = combatant.Id,
                    DefinitionId = combatant.DefinitionId,
                    Name = combatant.Name,
                    Faction = combatant.Faction.ToString(),
                    Team = string.IsNullOrEmpty(combatant.Team) || !int.TryParse(combatant.Team, out int teamNum) ? 0 : teamNum,
                    Tags = new List<string>(combatant.Tags),

                    // Position
                    PositionX = combatant.Position.X,
                    PositionY = combatant.Position.Y,
                    PositionZ = combatant.Position.Z,

                    // Resources (including spell slots via composite keys)
                    CurrentHP = combatant.Resources.CurrentHP,
                    MaxHP = combatant.Resources.MaxHP,
                    TemporaryHP = combatant.Resources.TemporaryHP,
                    ResourceCurrent = resourceCurrent,
                    ResourceMax = resourceMax,

                    // Combat state
                    LifeState = combatant.LifeState.ToString(),
                    DeathSaveSuccesses = combatant.DeathSaveSuccesses,
                    DeathSaveFailures = combatant.DeathSaveFailures,
                    IsAlive = combatant.IsActive,
                    Initiative = combatant.Initiative,
                    InitiativeTiebreaker = combatant.InitiativeTiebreaker,

                    // Action budget
                    HasAction = combatant.ActionBudget?.HasAction ?? false,
                    HasBonusAction = combatant.ActionBudget?.HasBonusAction ?? false,
                    HasReaction = combatant.ActionBudget?.HasReaction ?? false,
                    RemainingMovement = combatant.ActionBudget?.RemainingMovement ?? 0,
                    MaxMovement = combatant.ActionBudget?.MaxMovement ?? global::QDND.Combat.Actions.ActionBudget.DefaultMaxMovement,

                    // Stats
                    Strength = combatant.GetAbilityScore(AbilityType.Strength),
                    Dexterity = combatant.GetAbilityScore(AbilityType.Dexterity),
                    Constitution = combatant.GetAbilityScore(AbilityType.Constitution),
                    Intelligence = combatant.GetAbilityScore(AbilityType.Intelligence),
                    Wisdom = combatant.GetAbilityScore(AbilityType.Wisdom),
                    Charisma = combatant.GetAbilityScore(AbilityType.Charisma),
                    ArmorClass = combatant.GetArmorClass(),
                    Speed = combatant.GetSpeed(),

                    // Actions & passive toggles
                    KnownActions = new List<string>(combatant.KnownActions),
                    PassiveToggleStates = combatant.PassiveManager?.GetToggleStates() ?? new Dictionary<string, bool>(),
                    EquipmentSlots = equipmentSlots
                };

                snapshots.Add(snapshot);
            }

            return snapshots;
        }

        private void RestoreCombatants(ICombatContext context, List<CombatantSnapshot> snapshots, TurnQueueService turnQueue)
        {
            // Clear existing combatants
            context.ClearCombatants();
            if (turnQueue != null)
            {
                turnQueue.Reset();
            }

            // Restore from snapshots
            foreach (var snapshot in snapshots)
            {
                var combatant = new Combatant(
                    snapshot.Id,
                    snapshot.Name,
                    Enum.Parse<Faction>(snapshot.Faction),
                    snapshot.MaxHP,
                    snapshot.Initiative
                )
                {
                    Team = snapshot.Team.ToString(),
                    InitiativeTiebreaker = snapshot.InitiativeTiebreaker,
                    Position = new Vector3(snapshot.PositionX, snapshot.PositionY, snapshot.PositionZ)
                };

                // Restore resources
                combatant.Resources.MaxHP = snapshot.MaxHP;
                combatant.Resources.CurrentHP = snapshot.CurrentHP;
                combatant.Resources.TemporaryHP = snapshot.TemporaryHP;
                // Restore non-leveled resources into ActionResources
                foreach (var (key, max) in snapshot.ResourceMax)
                {
                    if (!snapshot.ResourceCurrent.TryGetValue(key, out int current))
                        current = max;
                    if (combatant.ActionResources.HasResource(key))
                    {
                        var res = combatant.ActionResources.GetResource(key);
                        if (res != null && !res.IsLeveled)
                        {
                            res.Max = Math.Max(0, max);
                            res.Current = Math.Clamp(current, 0, res.Max);
                        }
                    }
                    else
                    {
                        combatant.ActionResources.RegisterSimple(key, max, refillCurrent: false);
                        var res = combatant.ActionResources.GetResource(key);
                        if (res != null)
                            res.Current = Math.Clamp(current, 0, max);
                    }
                }

                // Restore life state and death saves
                if (Enum.TryParse<CombatantLifeState>(snapshot.LifeState, out var lifeState))
                    combatant.LifeState = lifeState;
                combatant.DeathSaveSuccesses = snapshot.DeathSaveSuccesses;
                combatant.DeathSaveFailures = snapshot.DeathSaveFailures;

                // Restore stats from snapshot via AbilityScoreOverrides and CurrentAC
                combatant.AbilityScoreOverrides[AbilityType.Strength] = snapshot.Strength;
                combatant.AbilityScoreOverrides[AbilityType.Dexterity] = snapshot.Dexterity;
                combatant.AbilityScoreOverrides[AbilityType.Constitution] = snapshot.Constitution;
                combatant.AbilityScoreOverrides[AbilityType.Intelligence] = snapshot.Intelligence;
                combatant.AbilityScoreOverrides[AbilityType.Wisdom] = snapshot.Wisdom;
                combatant.AbilityScoreOverrides[AbilityType.Charisma] = snapshot.Charisma;
                combatant.CurrentAC = snapshot.ArmorClass;

                // Restore action budget using ResetForTurn and then manually consume resources
                if (combatant.ActionBudget != null)
                {
                    // Reset to full budget
                    combatant.ActionBudget.ResetForTurn();
                    combatant.ActionBudget.ResetReactionForRound();

                    // Consume resources based on snapshot
                    if (!snapshot.HasAction)
                        combatant.ActionBudget.ConsumeAction();
                    if (!snapshot.HasBonusAction)
                        combatant.ActionBudget.ConsumeBonusAction();
                    if (!snapshot.HasReaction)
                        combatant.ActionBudget.ConsumeReaction();

                    // Set remaining movement
                    float movementUsed = snapshot.MaxMovement - snapshot.RemainingMovement;
                    if (movementUsed > 0)
                        combatant.ActionBudget.ConsumeMovement(movementUsed);
                }

                // Restore DefinitionId and Tags
                combatant.DefinitionId = snapshot.DefinitionId ?? string.Empty;
                if (snapshot.Tags?.Count > 0)
                    combatant.Tags = new List<string>(snapshot.Tags);

                // Restore known actions
                if (snapshot.KnownActions?.Count > 0)
                {
                    combatant.KnownActions.Clear();
                    combatant.KnownActions.AddRange(snapshot.KnownActions);
                    combatant.NotifyKnownActionsChanged();
                }

                // Restore passive toggle states (passives must already be applied first)
                if (snapshot.PassiveToggleStates?.Count > 0)
                    combatant.PassiveManager?.RestoreToggles(snapshot.PassiveToggleStates);

                // TODO: Re-equip items from snapshot.EquipmentSlots
                // This requires InventoryService to be fully initialised before restore.
                // Tracked for future implementation.

                context.AddCombatant(combatant);

                // Assign random placeholder portrait on restore
                // TODO: Persist portrait path in save data instead
                PortraitAssigner.AssignRandomPortrait(combatant);

                if (turnQueue != null)
                {
                    turnQueue.AddCombatant(combatant);
                }
            }
        }
    }
}
