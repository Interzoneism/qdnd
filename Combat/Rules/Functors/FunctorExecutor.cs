using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Environment;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Services;
using QDND.Combat.Statuses;

namespace QDND.Combat.Rules.Functors
{
    /// <summary>
    /// Executes parsed <see cref="FunctorDefinition"/> objects against live combatants.
    ///
    /// Delegates to existing subsystems:
    /// - <see cref="RulesEngine.RollDamage"/> / <see cref="ResourceComponent.TakeDamage"/> for DealDamage
    /// - <see cref="StatusManager.ApplyStatus(string,string,string,int?,int)"/> for ApplyStatus
    /// - <see cref="StatusManager.RemoveStatus(string,string)"/> for RemoveStatus
    /// - <see cref="ResourceComponent.Heal"/> for RegainHitPoints
    /// - <see cref="Services.ResourcePool.Restore"/> for RestoreResource
    ///
    /// Unimplemented functor types emit a warning and are skipped.
    /// </summary>
    public class FunctorExecutor
    {
        private readonly RulesEngine _rulesEngine;
        private readonly StatusManager _statusManager;

        /// <summary>
        /// Optional resolver to map combatant IDs to runtime <see cref="Combatant"/> instances.
        /// Required for DealDamage, RegainHitPoints, and RestoreResource.
        /// </summary>
        public Func<string, Combatant> ResolveCombatant { get; set; }

        /// <summary>
        /// Optional callback for breaking concentration. Parameters: (combatantId, reason).
        /// Wire to <see cref="ConcentrationSystem.BreakConcentration(string, string)"/>.
        /// </summary>
        public Action<string, string> BreakConcentrationAction { get; set; }

        /// <summary>
        /// Optional callback for triggering an extra attack. Parameters: (sourceId, targetId).
        /// Wire to the combat system's attack execution path.
        /// </summary>
        public Action<string, string> UseAttackAction { get; set; }

        /// <summary>
        /// Optional forced movement service for push/pull effects.
        /// </summary>
        public Movement.ForcedMovementService ForcedMovement { get; set; }

        /// <summary>
        /// Optional effect pipeline for sub-spell/projectile execution.
        /// </summary>
        public EffectPipeline EffectPipeline { get; set; }

        /// <summary>
        /// Optional surface manager for surface/zone/douse functors.
        /// </summary>
        public SurfaceManager SurfaceManager { get; set; }

        /// <summary>
        /// Optional inventory service for SummonInInventory functors.
        /// </summary>
        public InventoryService InventoryService { get; set; }

        /// <summary>
        /// Optional counterspell callback for reaction systems.
        /// Parameters: (sourceId, targetId).
        /// </summary>
        public Action<string, string> CounterspellAction { get; set; }

        [ThreadStatic] private static int _subspellRecursionDepth;
        private const int MaxSubspellRecursionDepth = 3;

        /// <summary>
        /// Create a new FunctorExecutor wired to the given rules engine and status manager.
        /// </summary>
        /// <param name="rulesEngine">Rules engine for dice rolls and event dispatch.</param>
        /// <param name="statusManager">Status manager for applying/removing statuses.</param>
        public FunctorExecutor(RulesEngine rulesEngine, StatusManager statusManager)
        {
            _rulesEngine = rulesEngine ?? throw new ArgumentNullException(nameof(rulesEngine));
            _statusManager = statusManager ?? throw new ArgumentNullException(nameof(statusManager));
        }

        /// <summary>
        /// Execute a list of functors in order.
        /// </summary>
        /// <param name="functors">The functor definitions to execute.</param>
        /// <param name="context">The event context that triggered execution.</param>
        /// <param name="sourceId">Combatant ID of the effect source (caster, status applier).</param>
        /// <param name="targetId">Combatant ID of the default target.</param>
        public void Execute(
            IReadOnlyList<FunctorDefinition> functors,
            FunctorContext context,
            string sourceId,
            string targetId)
        {
            if (functors == null || functors.Count == 0)
                return;

            foreach (var functor in functors)
            {
                try
                {
                    ExecuteSingle(functor, context, sourceId, targetId);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"[FunctorExecutor] Error executing {functor}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Execute a single functor definition.
        /// </summary>
        private void ExecuteSingle(
            FunctorDefinition functor,
            FunctorContext context,
            string sourceId,
            string targetId)
        {
            // Resolve effective target based on SELF/TARGET override
            string effectiveTarget = ResolveEffectiveTarget(functor.TargetOverride, sourceId, targetId);

            switch (functor.Type)
            {
                case FunctorType.DealDamage:
                    ExecuteDealDamage(functor, sourceId, effectiveTarget);
                    break;

                case FunctorType.ApplyStatus:
                    ExecuteApplyStatus(functor, sourceId, effectiveTarget);
                    break;

                case FunctorType.RemoveStatus:
                    ExecuteRemoveStatus(functor, effectiveTarget);
                    break;

                case FunctorType.RegainHitPoints:
                    ExecuteRegainHitPoints(functor, sourceId, effectiveTarget);
                    break;

                case FunctorType.RestoreResource:
                    ExecuteRestoreResource(functor, effectiveTarget);
                    break;

                case FunctorType.UseActionResource:
                    ExecuteUseActionResource(functor, effectiveTarget);
                    break;

                case FunctorType.BreakConcentration:
                    ExecuteBreakConcentration(functor, effectiveTarget);
                    break;

                case FunctorType.Stabilize:
                    ExecuteStabilize(functor, effectiveTarget);
                    break;

                case FunctorType.Force:
                    ExecuteForce(functor, sourceId, effectiveTarget);
                    break;

                case FunctorType.SetStatusDuration:
                    ExecuteSetStatusDuration(functor, effectiveTarget);
                    break;

                case FunctorType.UseAttack:
                    ExecuteUseAttack(functor, sourceId, effectiveTarget);
                    break;

                case FunctorType.SpawnSurface:
                    ExecuteSpawnSurface(functor, sourceId, effectiveTarget);
                    break;

                case FunctorType.SummonInInventory:
                    ExecuteSummonInInventory(functor, sourceId, effectiveTarget);
                    break;

                case FunctorType.Explode:
                    ExecuteExplode(functor, sourceId, effectiveTarget);
                    break;

                case FunctorType.Teleport:
                    ExecuteTeleport(functor, sourceId, effectiveTarget);
                    break;

                case FunctorType.UseSpell:
                    ExecuteUseSpell(functor, sourceId, effectiveTarget);
                    break;

                case FunctorType.CreateZone:
                    ExecuteCreateZone(functor, sourceId, effectiveTarget);
                    break;

                case FunctorType.FireProjectile:
                    ExecuteFireProjectile(functor, sourceId, effectiveTarget);
                    break;

                case FunctorType.Resurrect:
                    ExecuteResurrect(functor, sourceId, effectiveTarget);
                    break;

                case FunctorType.Douse:
                    ExecuteDouse(functor, sourceId, effectiveTarget);
                    break;

                case FunctorType.Counterspell:
                    ExecuteCounterspell(functor, sourceId, effectiveTarget);
                    break;

                case FunctorType.Unknown:
                    LogStub(functor, context, sourceId, effectiveTarget);
                    break;

                default:
                    LogStub(functor, context, sourceId, effectiveTarget);
                    break;
            }
        }

        // ─── Implemented functor handlers ────────────────────────────────

        /// <summary>
        /// Handle DealDamage(diceExpr, damageType).
        /// Rolls dice via <see cref="DiceRoller"/> and applies damage to the target.
        /// </summary>
        private void ExecuteDealDamage(FunctorDefinition functor, string sourceId, string targetId)
        {
            if (functor.Parameters.Length < 1)
            {
                Console.Error.WriteLine($"[FunctorExecutor] DealDamage missing dice parameter: {functor.RawString}");
                return;
            }

            string diceExpr = functor.Parameters[0];
            string damageType = functor.Parameters.Length >= 2 ? functor.Parameters[1] : "Untyped";

            int sourceLevel = ResolveCombatant?.Invoke(sourceId)?.ResolvedCharacter?.Sheet?.TotalLevel ?? 1;

            // For LevelMapValue: use class-specific level for multiclass characters
            int levelForDice = sourceLevel;
            var lvlMapMatch = Regex.Match(diceExpr, @"^LevelMapValue\((\w+)\)$", RegexOptions.IgnoreCase);
            if (lvlMapMatch.Success)
            {
                string mapClassName = LevelMapResolver.GetClassForMap(lvlMapMatch.Groups[1].Value);
                if (mapClassName != null)
                    levelForDice = ResolveCombatant?.Invoke(sourceId)?.ResolvedCharacter?.Sheet?.GetClassLevel(mapClassName) ?? 1;
            }

            int damage = RollDiceExpression(diceExpr, levelForDice);
            if (damage <= 0)
                return;

            var target = ResolveCombatant?.Invoke(targetId);
            if (target == null)
            {
                Console.Error.WriteLine($"[FunctorExecutor] DealDamage: cannot resolve target '{targetId}'");
                return;
            }

            int dealt = target.Resources.TakeDamage(damage);

            Console.WriteLine(
                $"[FunctorExecutor] DealDamage: {sourceId} → {targetId} for {dealt} {damageType} damage (rolled {damage} from {diceExpr})");

            // Dispatch damage event
            _rulesEngine.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.DamageTaken,
                SourceId = sourceId,
                TargetId = targetId,
                Value = dealt,
                Data = new Dictionary<string, object>
                {
                    { "damageType", damageType },
                    { "source", "functor" },
                    { "rawFunctor", functor.RawString ?? "" }
                },
                Tags = new HashSet<string> { DamageTypes.ToTag(damageType) }
            });
        }

        /// <summary>
        /// Handle ApplyStatus(statusId, chance, duration).
        /// Chance is a percentage (0-100); duration is in turns.
        /// </summary>
        private void ExecuteApplyStatus(FunctorDefinition functor, string sourceId, string targetId)
        {
            if (functor.Parameters.Length < 1)
            {
                Console.Error.WriteLine($"[FunctorExecutor] ApplyStatus missing statusId: {functor.RawString}");
                return;
            }

            string statusId = functor.Parameters[0];
            int chance = 100;
            int? duration = null;

            if (functor.Parameters.Length >= 2 && int.TryParse(functor.Parameters[1], out int parsedChance))
                chance = parsedChance;

            if (functor.Parameters.Length >= 3 && int.TryParse(functor.Parameters[2], out int parsedDuration))
                duration = parsedDuration == -1 ? (int?)null : parsedDuration; // -1 means permanent in BG3

            // Roll chance
            if (chance < 100)
            {
                int roll = _rulesEngine.Dice.Roll(1, 100);
                if (roll > chance)
                {
                    Console.WriteLine(
                        $"[FunctorExecutor] ApplyStatus({statusId}): chance roll {roll} > {chance}, skipped");
                    return;
                }
            }

            var instance = _statusManager.ApplyStatus(statusId, sourceId, targetId, duration);
            if (instance != null)
            {
                Console.WriteLine(
                    $"[FunctorExecutor] ApplyStatus: applied {statusId} to {targetId} (source={sourceId}, dur={duration?.ToString() ?? "default"})");
            }
        }

        /// <summary>
        /// Handle RemoveStatus(statusId).
        /// </summary>
        private void ExecuteRemoveStatus(FunctorDefinition functor, string targetId)
        {
            if (functor.Parameters.Length < 1)
            {
                Console.Error.WriteLine($"[FunctorExecutor] RemoveStatus missing statusId: {functor.RawString}");
                return;
            }

            string statusId = functor.Parameters[0];
            bool removed = _statusManager.RemoveStatus(targetId, statusId);

            Console.WriteLine(
                $"[FunctorExecutor] RemoveStatus({statusId}) on {targetId}: {(removed ? "success" : "not found")}");
        }

        /// <summary>
        /// Handle RegainHitPoints(diceExpr [, healType]).
        /// Rolls dice and heals the target.
        /// </summary>
        private void ExecuteRegainHitPoints(FunctorDefinition functor, string sourceId, string targetId)
        {
            if (functor.Parameters.Length < 1)
            {
                Console.Error.WriteLine($"[FunctorExecutor] RegainHitPoints missing dice parameter: {functor.RawString}");
                return;
            }

            string diceExpr = functor.Parameters[0];
            int healSourceLevel = ResolveCombatant?.Invoke(sourceId)?.ResolvedCharacter?.Sheet?.TotalLevel ?? 1;
            int healAmount = RollDiceExpression(diceExpr, healSourceLevel);
            if (healAmount <= 0)
                return;

            var target = ResolveCombatant?.Invoke(targetId);
            if (target == null)
            {
                Console.Error.WriteLine($"[FunctorExecutor] RegainHitPoints: cannot resolve target '{targetId}'");
                return;
            }

            int healed = target.Resources.Heal(healAmount);

            Console.WriteLine(
                $"[FunctorExecutor] RegainHitPoints: healed {targetId} for {healed} HP (rolled {healAmount} from {diceExpr})");

            // Dispatch healing event
            _rulesEngine.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.HealingReceived,
                SourceId = sourceId,
                TargetId = targetId,
                Value = healed,
                Data = new Dictionary<string, object>
                {
                    { "source", "functor" },
                    { "rawFunctor", functor.RawString ?? "" }
                }
            });
        }

        /// <summary>
        /// Handle RestoreResource(resourceName, amount [, level]).
        /// </summary>
        private void ExecuteRestoreResource(FunctorDefinition functor, string targetId)
        {
            if (functor.Parameters.Length < 2)
            {
                Console.Error.WriteLine($"[FunctorExecutor] RestoreResource missing parameters: {functor.RawString}");
                return;
            }

            string resourceName = functor.Parameters[0];
            if (!int.TryParse(functor.Parameters[1], out int amount))
            {
                Console.Error.WriteLine(
                    $"[FunctorExecutor] RestoreResource: cannot parse amount '{functor.Parameters[1]}'");
                return;
            }

            int level = 0;
            if (functor.Parameters.Length >= 3 && int.TryParse(functor.Parameters[2], out int parsedLevel))
                level = parsedLevel;

            var target = ResolveCombatant?.Invoke(targetId);
            if (target == null)
            {
                Console.Error.WriteLine($"[FunctorExecutor] RestoreResource: cannot resolve target '{targetId}'");
                return;
            }

            if (!target.ActionResources.HasResource(resourceName))
            {
                Console.WriteLine(
                    $"[FunctorExecutor] RestoreResource: target '{targetId}' has no resource '{resourceName}'");
                return;
            }

            target.ActionResources.Restore(resourceName, amount, level);

            Console.WriteLine(
                $"[FunctorExecutor] RestoreResource: restored {amount} {resourceName} (level {level}) on {targetId}");
        }

        /// <summary>
        /// Handle UseActionResource(resourceName, amount [, level] [, clamp]).
        /// Supports Movement as flat or percent of MaxMovement.
        /// </summary>
        private void ExecuteUseActionResource(FunctorDefinition functor, string targetId)
        {
            if (functor.Parameters.Length < 2)
            {
                Console.Error.WriteLine($"[FunctorExecutor] UseActionResource missing parameters: {functor.RawString}");
                return;
            }

            string resourceName = functor.Parameters[0];
            string amountToken = functor.Parameters[1];

            int level = 0;
            bool clamp = false;

            if (functor.Parameters.Length >= 3)
            {
                if (int.TryParse(functor.Parameters[2], out int parsedLevel))
                    level = parsedLevel;
                else if (bool.TryParse(functor.Parameters[2], out bool parsedClamp))
                    clamp = parsedClamp;
            }

            if (functor.Parameters.Length >= 4 && bool.TryParse(functor.Parameters[3], out bool parsedClamp4))
                clamp = parsedClamp4;

            var target = ResolveCombatant?.Invoke(targetId);
            if (target == null)
            {
                Console.Error.WriteLine($"[FunctorExecutor] UseActionResource: cannot resolve target '{targetId}'");
                return;
            }

            if (resourceName.Equals("Movement", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseResourceAmount(amountToken, target.ActionBudget.MaxMovement, out float requested))
                {
                    Console.Error.WriteLine(
                        $"[FunctorExecutor] UseActionResource: cannot parse amount '{amountToken}' for Movement");
                    return;
                }

                if (requested < 0f)
                {
                    Console.Error.WriteLine(
                        $"[FunctorExecutor] UseActionResource: negative amount '{amountToken}' is invalid");
                    return;
                }

                float before = target.ActionBudget.RemainingMovement;
                float toConsume = clamp ? Math.Min(requested, before) : requested;
                bool success = target.ActionBudget.ConsumeMovement(toConsume);

                if (!success)
                {
                    Console.WriteLine(
                        $"[FunctorExecutor] UseActionResource: failed to consume {toConsume:F2} Movement on {targetId} " +
                        $"(remaining={before:F2}, clamp={clamp})");
                    return;
                }

                Console.WriteLine(
                    $"[FunctorExecutor] UseActionResource: consumed {toConsume:F2} Movement on {targetId} " +
                    $"(remaining={target.ActionBudget.RemainingMovement:F2}, clamp={clamp})");
                return;
            }

            if (!TryParseResourceAmount(amountToken, 1f, out float requestedAmount))
            {
                Console.Error.WriteLine(
                    $"[FunctorExecutor] UseActionResource: cannot parse amount '{amountToken}' for {resourceName}");
                return;
            }

            if (requestedAmount < 0f)
            {
                Console.Error.WriteLine(
                    $"[FunctorExecutor] UseActionResource: negative amount '{amountToken}' is invalid");
                return;
            }

            int discreteAmount = ToDiscreteResourceAmount(requestedAmount);
            switch (resourceName.ToLowerInvariant())
            {
                case "actionpoint":
                {
                    int available = target.ActionBudget.ActionCharges;
                    int toConsume = clamp ? Math.Min(discreteAmount, available) : discreteAmount;
                    if (!clamp && toConsume > available)
                    {
                        Console.WriteLine(
                            $"[FunctorExecutor] UseActionResource: failed consuming ActionPoint x{toConsume} on {targetId} " +
                            $"(remaining={available}, clamp={clamp})");
                        return;
                    }

                    int consumed = 0;
                    for (int i = 0; i < toConsume; i++)
                    {
                        if (!target.ActionBudget.ConsumeAction())
                            break;

                        consumed++;
                    }

                    Console.WriteLine(
                        $"[FunctorExecutor] UseActionResource: consumed ActionPoint x{consumed} on {targetId} " +
                        $"(remaining={target.ActionBudget.ActionCharges}, clamp={clamp})");
                    return;
                }

                case "bonusactionpoint":
                {
                    int available = target.ActionBudget.BonusActionCharges;
                    int toConsume = clamp ? Math.Min(discreteAmount, available) : discreteAmount;
                    if (!clamp && toConsume > available)
                    {
                        Console.WriteLine(
                            $"[FunctorExecutor] UseActionResource: failed consuming BonusActionPoint x{toConsume} on {targetId} " +
                            $"(remaining={available}, clamp={clamp})");
                        return;
                    }

                    int consumed = 0;
                    for (int i = 0; i < toConsume; i++)
                    {
                        if (!target.ActionBudget.ConsumeBonusAction())
                            break;

                        consumed++;
                    }

                    Console.WriteLine(
                        $"[FunctorExecutor] UseActionResource: consumed BonusActionPoint x{consumed} on {targetId} " +
                        $"(remaining={target.ActionBudget.BonusActionCharges}, clamp={clamp})");
                    return;
                }

                case "reactionactionpoint":
                {
                    int available = target.ActionBudget.ReactionCharges;
                    int toConsume = clamp ? Math.Min(discreteAmount, available) : discreteAmount;
                    if (!clamp && toConsume > available)
                    {
                        Console.WriteLine(
                            $"[FunctorExecutor] UseActionResource: failed consuming ReactionActionPoint x{toConsume} on {targetId} " +
                            $"(remaining={available}, clamp={clamp})");
                        return;
                    }

                    int consumed = 0;
                    for (int i = 0; i < toConsume; i++)
                    {
                        if (!target.ActionBudget.ConsumeReaction())
                            break;

                        consumed++;
                    }

                    Console.WriteLine(
                        $"[FunctorExecutor] UseActionResource: consumed ReactionActionPoint x{consumed} on {targetId} " +
                        $"(remaining={target.ActionBudget.ReactionCharges}, clamp={clamp})");
                    return;
                }

                default:
                {
                    if (!target.ActionResources.HasResource(resourceName))
                    {
                        Console.WriteLine(
                            $"[FunctorExecutor] UseActionResource: target '{targetId}' has no resource '{resourceName}'");
                        return;
                    }

                    int maxForPercent = target.ActionResources.GetMax(resourceName, level);
                    if (amountToken.TrimEnd().EndsWith("%", StringComparison.Ordinal) &&
                        !TryParseResourceAmount(amountToken, maxForPercent, out requestedAmount))
                    {
                        Console.Error.WriteLine(
                            $"[FunctorExecutor] UseActionResource: cannot parse percent amount '{amountToken}' for {resourceName}");
                        return;
                    }

                    int requestedUnits = ToDiscreteResourceAmount(requestedAmount);
                    int available = target.ActionResources.GetCurrent(resourceName, level);
                    int toConsume = clamp ? Math.Min(requestedUnits, available) : requestedUnits;

                    bool success = target.ActionResources.Consume(resourceName, toConsume, level);
                    if (!success)
                    {
                        Console.WriteLine(
                            $"[FunctorExecutor] UseActionResource: failed consuming {resourceName} x{toConsume} on {targetId} " +
                            $"(level={level}, remaining={available}, clamp={clamp})");
                        return;
                    }

                    Console.WriteLine(
                        $"[FunctorExecutor] UseActionResource: consumed {resourceName} x{toConsume} on {targetId} " +
                        $"(level={level}, remaining={target.ActionResources.GetCurrent(resourceName, level)}, clamp={clamp})");
                    return;
                }
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Break concentration on the target combatant.
        /// BG3 usage: BreakConcentration() — typically no parameters.
        /// </summary>
        private void ExecuteBreakConcentration(FunctorDefinition functor, string targetId)
        {
            if (BreakConcentrationAction != null)
            {
                string reason = functor.Parameters.Length >= 1 ? functor.Parameters[0] : "Functor";
                BreakConcentrationAction(targetId, reason);
                Console.WriteLine($"[FunctorExecutor] BreakConcentration on {targetId} (reason: {reason})");
            }
            else
            {
                Console.WriteLine($"[FunctorExecutor] BreakConcentration: no ConcentrationSystem wired for {targetId}");
            }
        }

        /// <summary>
        /// Stabilize a downed combatant (stops death saves, sets to Unconscious at 0 HP).
        /// BG3 usage: IF(IsDowned()):Stabilize()
        /// </summary>
        private void ExecuteStabilize(FunctorDefinition functor, string targetId)
        {
            var target = ResolveCombatant?.Invoke(targetId);
            if (target == null)
            {
                Console.Error.WriteLine($"[FunctorExecutor] Stabilize: cannot resolve target '{targetId}'");
                return;
            }

            if (target.LifeState == CombatantLifeState.Downed)
            {
                target.Resources.CurrentHP = 0;
                target.LifeState = CombatantLifeState.Unconscious;
                target.ResetDeathSaves();
                Console.WriteLine($"[FunctorExecutor] Stabilize: stabilized {targetId} (now Unconscious at 0 HP)");
            }
            else
            {
                Console.WriteLine($"[FunctorExecutor] Stabilize: {targetId} is not Downed (state={target.LifeState}), skipped");
            }
        }

        /// <summary>
        /// Apply forced movement (push/pull) to a target.
        /// BG3 usage: Force(distance [, origin_type [, ...]])
        /// Origin types: TargetToEntity (pull toward caster), OriginToEntity (push from origin).
        /// Negative distance means pull.
        /// </summary>
        private void ExecuteForce(FunctorDefinition functor, string sourceId, string targetId)
        {
            if (functor.Parameters.Length < 1 || !float.TryParse(functor.Parameters[0], out float distance))
            {
                Console.Error.WriteLine($"[FunctorExecutor] Force: missing or invalid distance parameter: {functor.RawString}");
                return;
            }

            var source = ResolveCombatant?.Invoke(sourceId);
            var target = ResolveCombatant?.Invoke(targetId);
            if (target == null)
            {
                Console.Error.WriteLine($"[FunctorExecutor] Force: cannot resolve target '{targetId}'");
                return;
            }

            // Determine push vs pull from distance sign and origin type parameter
            string originType = functor.Parameters.Length >= 2 ? functor.Parameters[1] : "";
            bool isPull = distance < 0 || string.Equals(originType, "TargetToEntity", StringComparison.OrdinalIgnoreCase);
            float absDistance = Math.Abs(distance);

            if (ForcedMovement != null && source != null)
            {
                Movement.ForcedMovementResult result;
                if (isPull)
                {
                    result = ForcedMovement.Pull(target, source.Position, absDistance);
                }
                else
                {
                    result = ForcedMovement.Push(target, source.Position, absDistance);
                }

                Console.WriteLine(
                    $"[FunctorExecutor] Force: {(isPull ? "pulled" : "pushed")} {targetId} {result.DistanceMoved:F1}m " +
                    $"(intended {absDistance:F1}m){(result.WasBlocked ? $" blocked by {result.BlockedBy}" : "")}");
            }
            else
            {
                Console.WriteLine(
                    $"[FunctorExecutor] Force: would {(isPull ? "pull" : "push")} {targetId} {absDistance:F1}m " +
                    $"(no ForcedMovementService wired)");
            }
        }

        /// <summary>
        /// Modify the duration of an existing status on the target.
        /// BG3 usage: SetStatusDuration(statusId, delta, mode)
        /// Modes: "Add" extends duration, otherwise sets absolute.
        /// </summary>
        private void ExecuteSetStatusDuration(FunctorDefinition functor, string targetId)
        {
            if (functor.Parameters.Length < 2)
            {
                Console.Error.WriteLine($"[FunctorExecutor] SetStatusDuration missing parameters: {functor.RawString}");
                return;
            }

            string statusId = functor.Parameters[0];
            if (!int.TryParse(functor.Parameters[1], out int durationValue))
            {
                Console.Error.WriteLine(
                    $"[FunctorExecutor] SetStatusDuration: cannot parse duration '{functor.Parameters[1]}'");
                return;
            }

            string mode = functor.Parameters.Length >= 3 ? functor.Parameters[2] : "Set";

            var statuses = _statusManager.GetStatuses(targetId);
            var instance = statuses.FirstOrDefault(s =>
                string.Equals(s.Definition.Id, statusId, StringComparison.OrdinalIgnoreCase));

            if (instance == null)
            {
                Console.WriteLine(
                    $"[FunctorExecutor] SetStatusDuration: status '{statusId}' not found on '{targetId}'");
                return;
            }

            if (string.Equals(mode, "Add", StringComparison.OrdinalIgnoreCase))
            {
                instance.ExtendDuration(durationValue);
                Console.WriteLine(
                    $"[FunctorExecutor] SetStatusDuration: extended {statusId} on {targetId} by {durationValue} " +
                    $"(remaining={instance.RemainingDuration})");
            }
            else
            {
                // -1 means permanent/infinite in BG3
                instance.RemainingDuration = durationValue;
                Console.WriteLine(
                    $"[FunctorExecutor] SetStatusDuration: set {statusId} on {targetId} to {durationValue}");
            }
        }

        /// <summary>
        /// Trigger an extra attack from source against target.
        /// BG3 usage: UseAttack() or UseAttack(SWAP)
        /// Used by passives/interrupts that grant bonus attacks (e.g., Sentinel, Giant Killer).
        /// </summary>
        private void ExecuteUseAttack(FunctorDefinition functor, string sourceId, string targetId)
        {
            if (UseAttackAction != null)
            {
                UseAttackAction(sourceId, targetId);
                Console.WriteLine($"[FunctorExecutor] UseAttack: {sourceId} attacks {targetId}");
            }
            else
            {
                Console.WriteLine(
                    $"[FunctorExecutor] UseAttack: would trigger attack from {sourceId} → {targetId} " +
                    $"(no UseAttackAction wired)");
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Resolve the effective target ID based on the functor's target override.
        /// </summary>
        private static string ResolveEffectiveTarget(FunctorTarget targetOverride, string sourceId, string targetId)
        {
            return targetOverride switch
            {
                FunctorTarget.Self => sourceId,
                FunctorTarget.Target => targetId,
                _ => targetId // Default: the natural target
            };
        }

        /// <summary>
        /// Roll a dice expression string like "1d4", "2d6+3", "3d4", a flat number like "5",
        /// or a BG3 LevelMapValue expression like "LevelMapValue(RageDamage)".
        /// </summary>
        private int RollDiceExpression(string expression, int sourceLevel = 1)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return 0;

            expression = expression.Trim();

            // Handle LevelMapValue(mapName) expressions
            var lvlMatch = Regex.Match(expression, @"^LevelMapValue\((\w+)\)$", RegexOptions.IgnoreCase);
            if (lvlMatch.Success)
            {
                string resolved = LevelMapResolver.Resolve(lvlMatch.Groups[1].Value, sourceLevel);
                expression = resolved;
            }

            // Try flat number first (also handles plain integers returned by LevelMapResolver)
            if (int.TryParse(expression, out int flat))
                return Math.Max(0, flat);

            // Parse XdY or XdY+Z or XdY-Z
            var match = Regex.Match(expression, @"^(\d+)d(\d+)([+-]\d+)?$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                Console.Error.WriteLine($"[FunctorExecutor] Cannot parse dice expression: {expression}");
                return 0;
            }

            int count = int.Parse(match.Groups[1].Value);
            int sides = int.Parse(match.Groups[2].Value);
            int bonus = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

            return Math.Max(0, _rulesEngine.Dice.Roll(count, sides, bonus));
        }

        private static bool TryParseResourceAmount(string amountText, float maxForPercent, out float amount)
        {
            amount = 0f;
            if (string.IsNullOrWhiteSpace(amountText))
                return false;

            string trimmed = amountText.Trim();
            if (trimmed.EndsWith("%", StringComparison.Ordinal))
            {
                string numberPart = trimmed[..^1].Trim();
                if (!float.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out float percent))
                    return false;

                amount = maxForPercent * (percent / 100f);
                if (percent > 0f && maxForPercent > 0f && amount > 0f && amount < 1f)
                    amount = 1f;

                return true;
            }

            return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out amount);
        }

        private static int ToDiscreteResourceAmount(float amount)
        {
            if (amount <= 0f)
                return 0;

            int floored = (int)Math.Floor(amount);
            return Math.Max(1, floored);
        }

        /// <summary>
        /// Log a warning for an unimplemented (stub) functor type.
        /// </summary>
        private static void LogStub(
            FunctorDefinition functor,
            FunctorContext context,
            string sourceId,
            string targetId)
        {
            Console.WriteLine(
                $"[FunctorExecutor] STUB functor {functor.Type} not implemented " +
                $"(context={context}, source={sourceId}, target={targetId}, raw={functor.RawString})");
        }

        private void ExecuteUseSpell(FunctorDefinition functor, string sourceId, string targetId)
        {
            ExecuteSubspellFunctor(functor, sourceId, targetId, "UseSpell");
        }

        private void ExecuteFireProjectile(FunctorDefinition functor, string sourceId, string targetId)
        {
            ExecuteSubspellFunctor(functor, sourceId, targetId, "FireProjectile");
        }

        private void ExecuteExplode(FunctorDefinition functor, string sourceId, string targetId)
        {
            ExecuteSubspellFunctor(functor, sourceId, targetId, "Explode");
        }

        private void ExecuteSubspellFunctor(FunctorDefinition functor, string sourceId, string targetId, string label)
        {
            if (EffectPipeline == null)
            {
                Console.WriteLine($"[FunctorExecutor] {label}: no EffectPipeline wired");
                return;
            }

            var source = ResolveCombatant?.Invoke(sourceId);
            var target = ResolveCombatant?.Invoke(targetId);
            if (source == null)
            {
                Console.Error.WriteLine($"[FunctorExecutor] {label}: cannot resolve source '{sourceId}'");
                return;
            }

            if (functor.Parameters.Length == 0)
            {
                Console.Error.WriteLine($"[FunctorExecutor] {label}: missing spell/projectile id: {functor.RawString}");
                return;
            }

            int spellIndex = 0;
            string qualifier = NormalizeToken(functor.Parameters[0]).ToUpperInvariant();
            if (LooksLikeSourceQualifier(qualifier) && functor.Parameters.Length > 1)
            {
                spellIndex = 1;
            }

            string spellId = NormalizeToken(functor.Parameters[spellIndex]);
            if (string.IsNullOrWhiteSpace(spellId))
            {
                Console.Error.WriteLine($"[FunctorExecutor] {label}: empty spell/projectile id: {functor.RawString}");
                return;
            }

            var castSource = source;
            var targets = new List<Combatant>();

            if (spellIndex == 1)
            {
                switch (qualifier)
                {
                    case "SWAP":
                        castSource = target ?? source;
                        if (source != null)
                            targets.Add(source);
                        break;
                    case "SELF":
                    case "OBSERVER_SOURCE":
                        targets.Add(source);
                        break;
                    case "TARGET":
                    case "OBSERVER_TARGET":
                        if (target != null)
                            targets.Add(target);
                        break;
                    default:
                        if (target != null)
                            targets.Add(target);
                        break;
                }
            }

            if (targets.Count == 0)
            {
                if (target != null)
                    targets.Add(target);
                else
                    targets.Add(castSource);
            }

            if (_subspellRecursionDepth >= MaxSubspellRecursionDepth)
            {
                Console.WriteLine($"[FunctorExecutor] {label}: recursion depth limit reached for '{spellId}'");
                return;
            }

            _subspellRecursionDepth++;
            try
            {
                var options = new ActionExecutionOptions
                {
                    SkipCostValidation = true,
                    TargetPosition = targets[0]?.Position
                };

                var result = EffectPipeline.ExecuteAction(spellId, castSource, targets, options);
                if (!result.Success)
                {
                    Console.WriteLine($"[FunctorExecutor] {label}: sub-action '{spellId}' failed: {result.ErrorMessage}");
                }
            }
            finally
            {
                _subspellRecursionDepth--;
            }
        }

        private static bool LooksLikeSourceQualifier(string token)
        {
            return token is "SWAP" or "SELF" or "TARGET" or "OBSERVER_SOURCE" or "OBSERVER_TARGET" or "OBSERVER_OBSERVER";
        }

        private static string NormalizeToken(string token)
        {
            return token?.Trim().Trim('\'', '"') ?? string.Empty;
        }

        private void ExecuteSpawnSurface(FunctorDefinition functor, string sourceId, string targetId)
        {
            ExecuteSurfaceFunctor(functor, sourceId, targetId, defaultSurfaceType: "fire", defaultRadius: 2.5f, defaultDuration: 2, label: "SpawnSurface");
        }

        private void ExecuteCreateZone(FunctorDefinition functor, string sourceId, string targetId)
        {
            ExecuteSurfaceFunctor(functor, sourceId, targetId, defaultSurfaceType: "fog", defaultRadius: 3.0f, defaultDuration: 3, label: "CreateZone");
        }

        private void ExecuteSurfaceFunctor(
            FunctorDefinition functor,
            string sourceId,
            string targetId,
            string defaultSurfaceType,
            float defaultRadius,
            int defaultDuration,
            string label)
        {
            if (SurfaceManager == null)
            {
                Console.WriteLine($"[FunctorExecutor] {label}: no SurfaceManager wired");
                return;
            }

            string surfaceType = defaultSurfaceType;
            float radius = defaultRadius;
            int duration = defaultDuration;

            bool hasRadius = false;
            bool hasDuration = false;

            foreach (var param in functor.Parameters)
            {
                string token = NormalizeToken(param);
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                if (!hasRadius && float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedRadius))
                {
                    radius = Math.Max(0.5f, parsedRadius);
                    hasRadius = true;
                    continue;
                }

                if (!hasDuration && int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedDuration))
                {
                    duration = parsedDuration;
                    hasDuration = true;
                    continue;
                }

                if (token.Any(char.IsLetter))
                {
                    surfaceType = token.ToLowerInvariant();
                }
            }

            var target = ResolveCombatant?.Invoke(targetId);
            var source = ResolveCombatant?.Invoke(sourceId);
            Vector3 position = target?.Position ?? source?.Position ?? Vector3.Zero;

            var created = SurfaceManager.CreateSurface(surfaceType, position, radius, sourceId, duration);
            if (created == null)
            {
                Console.WriteLine($"[FunctorExecutor] {label}: failed to create surface '{surfaceType}'");
                return;
            }

            Console.WriteLine($"[FunctorExecutor] {label}: created {surfaceType} at {position} r={radius:F1} d={duration}");
        }

        private void ExecuteTeleport(FunctorDefinition functor, string sourceId, string targetId)
        {
            var target = ResolveCombatant?.Invoke(targetId);
            if (target == null)
            {
                Console.Error.WriteLine($"[FunctorExecutor] Teleport: cannot resolve target '{targetId}'");
                return;
            }

            var source = ResolveCombatant?.Invoke(sourceId);
            Vector3 destination = target.Position;

            if (functor.Parameters.Length >= 3 &&
                float.TryParse(NormalizeToken(functor.Parameters[0]), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(NormalizeToken(functor.Parameters[1]), NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(NormalizeToken(functor.Parameters[2]), NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                destination = new Vector3(x, y, z);
            }
            else if (functor.Parameters.Length >= 1 &&
                     float.TryParse(NormalizeToken(functor.Parameters[0]), NumberStyles.Float, CultureInfo.InvariantCulture, out float distance))
            {
                Vector3 origin = source?.Position ?? target.Position;
                Vector3 direction = (target.Position - origin).Length() > 0.01f
                    ? (target.Position - origin).Normalized()
                    : Vector3.Forward;
                destination = target.Position + direction * Math.Abs(distance);
            }
            else if (source != null)
            {
                destination = source.Position;
            }

            if (ForcedMovement != null)
            {
                ForcedMovement.Teleport(target, destination);
            }
            else
            {
                target.Position = destination;
            }

            Console.WriteLine($"[FunctorExecutor] Teleport: moved {targetId} to {destination}");
        }

        private void ExecuteResurrect(FunctorDefinition functor, string sourceId, string targetId)
        {
            var target = ResolveCombatant?.Invoke(targetId);
            if (target == null)
            {
                Console.Error.WriteLine($"[FunctorExecutor] Resurrect: cannot resolve target '{targetId}'");
                return;
            }

            int hpPercent = 1;
            if (functor.Parameters.Length >= 2 && int.TryParse(NormalizeToken(functor.Parameters[1]), out int p2))
            {
                hpPercent = p2;
            }
            else if (functor.Parameters.Length >= 1 && int.TryParse(NormalizeToken(functor.Parameters[0]), out int p1) && p1 > 0 && p1 <= 100)
            {
                hpPercent = p1;
            }

            hpPercent = Math.Clamp(hpPercent, 1, 100);
            int restoredHp = Math.Max(1, (int)Math.Round(target.Resources.MaxHP * (hpPercent / 100.0f)));

            target.Resources.CurrentHP = restoredHp;
            target.LifeState = CombatantLifeState.Alive;
            target.ResetDeathSaves();

            Console.WriteLine($"[FunctorExecutor] Resurrect: revived {targetId} at {restoredHp} HP ({hpPercent}% max)");
        }

        private void ExecuteDouse(FunctorDefinition functor, string sourceId, string targetId)
        {
            var target = ResolveCombatant?.Invoke(targetId);
            if (target != null)
            {
                _statusManager.RemoveStatus(target.Id, "BURNING");
                _statusManager.RemoveStatus(target.Id, "burning");
            }

            if (SurfaceManager != null)
            {
                float radius = 2.5f;
                if (functor.Parameters.Length >= 1 &&
                    float.TryParse(NormalizeToken(functor.Parameters[0]), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedRadius))
                {
                    radius = Math.Max(0.5f, parsedRadius);
                }

                var source = ResolveCombatant?.Invoke(sourceId);
                Vector3 position = target?.Position ?? source?.Position ?? Vector3.Zero;
                int affected = SurfaceManager.ApplySurfaceEvent("douse", position, radius, sourceId);
                Console.WriteLine($"[FunctorExecutor] Douse: affected {affected} surface tiles around {position}");
            }
        }

        private void ExecuteSummonInInventory(FunctorDefinition functor, string sourceId, string targetId)
        {
            if (InventoryService == null)
            {
                Console.WriteLine("[FunctorExecutor] SummonInInventory: no InventoryService wired");
                return;
            }

            if (functor.Parameters.Length == 0)
            {
                Console.Error.WriteLine($"[FunctorExecutor] SummonInInventory: missing item id: {functor.RawString}");
                return;
            }

            string itemId = NormalizeToken(functor.Parameters[0]);
            int quantity = 1;
            if (functor.Parameters.Length >= 3 && int.TryParse(NormalizeToken(functor.Parameters[2]), out int q3))
                quantity = Math.Max(1, q3);
            else if (functor.Parameters.Length >= 2 && int.TryParse(NormalizeToken(functor.Parameters[1]), out int q2))
                quantity = Math.Max(1, q2);

            var recipient = ResolveCombatant?.Invoke(targetId) ?? ResolveCombatant?.Invoke(sourceId);
            if (recipient == null)
            {
                Console.Error.WriteLine($"[FunctorExecutor] SummonInInventory: cannot resolve recipient '{targetId}'");
                return;
            }

            var item = new InventoryItem
            {
                DefinitionId = itemId,
                Name = itemId,
                Category = ItemCategory.Misc,
                Quantity = quantity
            };

            bool added = InventoryService.AddItemToBag(recipient, item);
            Console.WriteLine($"[FunctorExecutor] SummonInInventory: {(added ? "added" : "failed")} {quantity}x {itemId} for {recipient.Id}");
        }

        private void ExecuteCounterspell(FunctorDefinition functor, string sourceId, string targetId)
        {
            if (CounterspellAction != null)
            {
                CounterspellAction(sourceId, targetId);
                return;
            }

            if (BreakConcentrationAction != null)
            {
                BreakConcentrationAction(targetId, "Counterspell");
                Console.WriteLine($"[FunctorExecutor] Counterspell: broke concentration on {targetId}");
                return;
            }

            Console.WriteLine("[FunctorExecutor] Counterspell: no callback wired");
        }

    }
}
