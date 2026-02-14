using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using QDND.Combat.Entities;
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

                // Stubs — log and skip
                case FunctorType.BreakConcentration:
                case FunctorType.Force:
                case FunctorType.SpawnSurface:
                case FunctorType.SummonInInventory:
                case FunctorType.Explode:
                case FunctorType.Teleport:
                case FunctorType.UseSpell:
                case FunctorType.UseAttack:
                case FunctorType.CreateZone:
                case FunctorType.SetStatusDuration:
                case FunctorType.FireProjectile:
                case FunctorType.Stabilize:
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

            int damage = RollDiceExpression(diceExpr);
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
            int healAmount = RollDiceExpression(diceExpr);
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
        /// Roll a dice expression string like "1d4", "2d6+3", "3d4", or a flat number like "5".
        /// </summary>
        private int RollDiceExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return 0;

            expression = expression.Trim();

            // Try flat number first
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
