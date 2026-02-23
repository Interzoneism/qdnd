using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
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

                // Stubs — log and skip
                case FunctorType.SpawnSurface:
                case FunctorType.SummonInInventory:
                case FunctorType.Explode:
                case FunctorType.Teleport:
                case FunctorType.UseSpell:
                case FunctorType.CreateZone:
                case FunctorType.FireProjectile:
                case FunctorType.Resurrect:
                case FunctorType.Douse:
                case FunctorType.Counterspell:
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
    }
}
