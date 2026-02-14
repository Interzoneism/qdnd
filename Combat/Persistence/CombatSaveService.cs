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

            foreach (var combatant in context.GetAllCombatants())
            {
                var snapshot = new CombatantSnapshot
                {
                    Id = combatant.Id,
                    Name = combatant.Name,
                    Faction = combatant.Faction.ToString(),
                    Team = string.IsNullOrEmpty(combatant.Team) || !int.TryParse(combatant.Team, out int teamNum) ? 0 : teamNum,

                    // Position
                    PositionX = combatant.Position.X,
                    PositionY = combatant.Position.Y,
                    PositionZ = combatant.Position.Z,

                    // Resources
                    CurrentHP = combatant.Resources.CurrentHP,
                    MaxHP = combatant.Resources.MaxHP,
                    TemporaryHP = combatant.Resources.TemporaryHP,
                    ResourceCurrent = combatant.ResourcePool.CurrentValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    ResourceMax = combatant.ResourcePool.MaxValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),

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
                    MaxMovement = combatant.ActionBudget?.MaxMovement ?? 30f,

                    // Stats
                    Strength = combatant.Stats?.Strength ?? 10,
                    Dexterity = combatant.Stats?.Dexterity ?? 10,
                    Constitution = combatant.Stats?.Constitution ?? 10,
                    Intelligence = combatant.Stats?.Intelligence ?? 10,
                    Wisdom = combatant.Stats?.Wisdom ?? 10,
                    Charisma = combatant.Stats?.Charisma ?? 10,
                    ArmorClass = combatant.Stats?.BaseAC ?? 10,
                    Speed = combatant.Stats?.Speed ?? 30f
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
                combatant.ResourcePool.Import(snapshot.ResourceMax, snapshot.ResourceCurrent);

                // Restore life state and death saves
                if (Enum.TryParse<CombatantLifeState>(snapshot.LifeState, out var lifeState))
                    combatant.LifeState = lifeState;
                combatant.DeathSaveSuccesses = snapshot.DeathSaveSuccesses;
                combatant.DeathSaveFailures = snapshot.DeathSaveFailures;

                // Restore stats (if Stats is null, create it)
                if (combatant.Stats == null)
                    combatant.Stats = new CombatantStats();

                combatant.Stats.Strength = snapshot.Strength;
                combatant.Stats.Dexterity = snapshot.Dexterity;
                combatant.Stats.Constitution = snapshot.Constitution;
                combatant.Stats.Intelligence = snapshot.Intelligence;
                combatant.Stats.Wisdom = snapshot.Wisdom;
                combatant.Stats.Charisma = snapshot.Charisma;
                combatant.Stats.BaseAC = snapshot.ArmorClass;
                combatant.Stats.Speed = snapshot.Speed;

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
