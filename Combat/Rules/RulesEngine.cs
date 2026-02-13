using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;

namespace QDND.Combat.Rules
{
    /// <summary>
    /// Types of queries that can be made to the rules engine.
    /// </summary>
    public enum QueryType
    {
        AttackRoll,
        DamageRoll,
        SavingThrow,
        SkillCheck,
        HitChance,
        CriticalChance,
        ArmorClass,
        Initiative,
        MovementSpeed,
        Contest,
        Custom
    }

    /// <summary>
    /// Indicates the winner of a contested check.
    /// </summary>
    public enum ContestWinner
    {
        Attacker,
        Defender,
        Tie
    }

    /// <summary>
    /// Policy for resolving ties in contested checks.
    /// </summary>
    public enum TiePolicy
    {
        /// <summary>Defender wins on tie (default D&amp;D 5e rule).</summary>
        DefenderWins,
        /// <summary>Attacker wins on tie.</summary>
        AttackerWins,
        /// <summary>Result is a true tie (no winner).</summary>
        NoWinner
    }

    /// <summary>
    /// Result of a contested check (e.g., shove, grapple).
    /// </summary>
    public class ContestResult
    {
        /// <summary>Attacker's total roll (natural + modifiers).</summary>
        public int RollA { get; set; }

        /// <summary>Defender's total roll (natural + modifiers).</summary>
        public int RollB { get; set; }

        /// <summary>Attacker's natural d20 roll.</summary>
        public int NaturalRollA { get; set; }

        /// <summary>Defender's natural d20 roll.</summary>
        public int NaturalRollB { get; set; }

        /// <summary>Winner of the contest.</summary>
        public ContestWinner Winner { get; set; }

        /// <summary>Breakdown string for attacker's roll.</summary>
        public string BreakdownA { get; set; }

        /// <summary>Breakdown string for defender's roll.</summary>
        public string BreakdownB { get; set; }

        /// <summary>Difference between RollA and RollB (positive = attacker advantage).</summary>
        public int Margin { get; set; }

        /// <summary>Whether the attacker won the contest.</summary>
        public bool AttackerWon => Winner == ContestWinner.Attacker;

        /// <summary>Whether the defender won the contest.</summary>
        public bool DefenderWon => Winner == ContestWinner.Defender;
    }

    /// <summary>
    /// Input for a rules query.
    /// </summary>
    public class QueryInput
    {
        public QueryType Type { get; set; }
        public string CustomType { get; set; }

        /// <summary>
        /// Source combatant (attacker, caster, etc).
        /// </summary>
        public Combatant Source { get; set; }

        /// <summary>
        /// Target combatant (defender, target, etc).
        /// </summary>
        public Combatant Target { get; set; }

        /// <summary>
        /// Base value before modifiers.
        /// </summary>
        public float BaseValue { get; set; }

        /// <summary>
        /// Difficulty class (for saves/checks).
        /// </summary>
        public int DC { get; set; }

        /// <summary>
        /// Tags for modifier filtering.
        /// </summary>
        public HashSet<string> Tags { get; set; } = new();

        /// <summary>
        /// Additional query parameters.
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Result of a rules query with full breakdown.
    /// </summary>
    public class QueryResult
    {
        /// <summary>
        /// The query that produced this result.
        /// </summary>
        public QueryInput Input { get; set; }

        /// <summary>
        /// Original base value.
        /// </summary>
        public float BaseValue { get; set; }

        /// <summary>
        /// Final calculated value.
        /// </summary>
        public float FinalValue { get; set; }

        /// <summary>
        /// Natural roll value (for d20 rolls).
        /// </summary>
        public int NaturalRoll { get; set; }

        /// <summary>
        /// Modifiers that were applied.
        /// </summary>
        public List<Modifier> AppliedModifiers { get; set; } = new();

        /// <summary>
        /// Whether the result is a success (for saves/checks).
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Whether this was a critical hit/success.
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Whether this was a critical failure.
        /// </summary>
        public bool IsCriticalFailure { get; set; }

        /// <summary>
        /// Advantage state: 1 = advantage, -1 = disadvantage, 0 = normal.
        /// </summary>
        public int AdvantageState { get; set; }

        /// <summary>
        /// If advantage/disadvantage, both roll values.
        /// </summary>
        public int[] RollValues { get; set; }

        /// <summary>
        /// Structured breakdown for UI tooltips.
        /// </summary>
        public RollBreakdown Breakdown { get; set; }

        /// <summary>
        /// Get a formatted breakdown string for display.
        /// </summary>
        public string GetBreakdown()
        {
            var parts = new List<string>();

            if (NaturalRoll > 0)
                parts.Add($"Roll: {NaturalRoll}");
            else
                parts.Add($"Base: {BaseValue}");

            foreach (var mod in AppliedModifiers)
            {
                parts.Add(mod.ToString());
            }

            parts.Add($"= {FinalValue}");

            if (IsCritical) parts.Add("(CRITICAL)");
            if (IsCriticalFailure) parts.Add("(CRITICAL FAIL)");

            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Export breakdown as structured data.
        /// </summary>
        public Dictionary<string, object> ToBreakdownData()
        {
            return new Dictionary<string, object>
            {
                { "baseValue", BaseValue },
                { "finalValue", FinalValue },
                { "naturalRoll", NaturalRoll },
                { "isSuccess", IsSuccess },
                { "isCritical", IsCritical },
                { "isCriticalFailure", IsCriticalFailure },
                { "advantageState", AdvantageState },
                { "modifiers", AppliedModifiers.Select(m => m.ToString()).ToList() }
            };
        }
    }

    /// <summary>
    /// Computes dice roll results with seeded RNG.
    /// </summary>
    public class DiceRoller
    {
        private Random _rng;
        private int _seed;
        private int _rollIndex = 0;

        /// <summary>Current seed value.</summary>
        public int Seed => _seed;

        /// <summary>Number of rolls made since initialization/reset.</summary>
        public int RollIndex => _rollIndex;

        public DiceRoller(int seed)
        {
            _seed = seed;
            _rng = new Random(seed);
            _rollIndex = 0;
        }

        public DiceRoller()
        {
            _seed = System.Environment.TickCount;
            _rng = new Random(_seed);
            _rollIndex = 0;
        }

        public void SetSeed(int seed)
        {
            _seed = seed;
            _rng = new Random(seed);
            _rollIndex = 0;
        }

        /// <summary>
        /// Restore RNG state for deterministic replay.
        /// Fast-forwards to the specified roll index.
        /// </summary>
        public void SetState(int seed, int rollIndex)
        {
            if (rollIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(rollIndex), "Roll index cannot be negative");

            _seed = seed;
            _rng = new Random(seed);
            // Fast-forward to rollIndex by consuming random values
            for (int i = 0; i < rollIndex; i++)
            {
                _rng.Next();
            }
            _rollIndex = rollIndex;
        }

        /// <summary>
        /// Roll a d20.
        /// </summary>
        public int RollD20()
        {
            _rollIndex++;
            return _rng.Next(1, 21);
        }

        /// <summary>
        /// Roll arbitrary dice (e.g., 2d6+3).
        /// </summary>
        public int Roll(int count, int sides, int bonus = 0)
        {
            int total = bonus;
            for (int i = 0; i < count; i++)
            {
                _rollIndex++;
                total += _rng.Next(1, sides + 1);
            }
            return total;
        }

        /// <summary>
        /// Roll with advantage (take higher of two rolls).
        /// </summary>
        public (int Result, int Roll1, int Roll2) RollWithAdvantage()
        {
            int r1 = RollD20();
            int r2 = RollD20();
            return (Math.Max(r1, r2), r1, r2);
        }

        /// <summary>
        /// Roll with disadvantage (take lower of two rolls).
        /// </summary>
        public (int Result, int Roll1, int Roll2) RollWithDisadvantage()
        {
            int r1 = RollD20();
            int r2 = RollD20();
            return (Math.Min(r1, r2), r1, r2);
        }
    }

    /// <summary>
    /// Central rules engine for computing combat outcomes.
    /// </summary>
    public class RulesEngine
    {
        private readonly ModifierStack _globalModifiers = new();
        private readonly Dictionary<string, ModifierStack> _combatantModifiers = new();
        private DiceRoller _dice;

        /// <summary>
        /// The event bus for rule events.
        /// </summary>
        public RuleEventBus Events { get; } = new();
        public RuleWindowBus RuleWindows { get; } = new();

        /// <summary>Current RNG seed.</summary>
        public int Seed => _dice.Seed;

        /// <summary>Current roll index for save/load.</summary>
        public int RollIndex => _dice.RollIndex;

        /// <summary>Public dice roller access for subsystems that need simple rolls.</summary>
        public DiceRoller Dice => _dice;

        public RulesEngine(int? seed = null)
        {
            _dice = seed.HasValue ? new DiceRoller(seed.Value) : new DiceRoller();
        }

        public void SetSeed(int seed)
        {
            _dice.SetSeed(seed);
        }

        /// <summary>Restore RNG state for deterministic replay.</summary>
        public void SetRngState(int seed, int rollIndex)
        {
            _dice.SetState(seed, rollIndex);
        }

        /// <summary>
        /// Get or create modifier stack for a combatant.
        /// </summary>
        public ModifierStack GetModifiers(string combatantId)
        {
            if (!_combatantModifiers.TryGetValue(combatantId, out var stack))
            {
                stack = new ModifierStack();
                _combatantModifiers[combatantId] = stack;
            }
            return stack;
        }

        /// <summary>
        /// Add a global modifier that applies to all combatants.
        /// </summary>
        public void AddGlobalModifier(Modifier modifier)
        {
            _globalModifiers.Add(modifier);
        }

        /// <summary>
        /// Add a modifier to a specific combatant.
        /// </summary>
        public void AddModifier(string combatantId, Modifier modifier)
        {
            GetModifiers(combatantId).Add(modifier);
        }

        /// <summary>
        /// Execute an attack roll query.
        /// </summary>
        public QueryResult RollAttack(QueryInput input)
        {
            var context = new ModifierContext
            {
                AttackerId = input.Source?.Id,
                DefenderId = input.Target?.Id,
                Tags = input.Tags
            };

            // Resolve advantage/disadvantage with sources
            var attackerMods = input.Source != null ? GetModifiers(input.Source.Id) : new ModifierStack();
            var attackerResolution = attackerMods.ResolveAdvantage(ModifierTarget.AttackRoll, context);
            var globalResolution = _globalModifiers.ResolveAdvantage(ModifierTarget.AttackRoll, context);

            // Combine resolutions: if either has adv and either has dis, result is normal
            AdvantageState combinedState;
            var allAdvSources = attackerResolution.AdvantageSources.Concat(globalResolution.AdvantageSources).ToList();
            var allDisSources = attackerResolution.DisadvantageSources.Concat(globalResolution.DisadvantageSources).ToList();
            allAdvSources.AddRange(GetStringListParameter(input.Parameters, "statusAdvantageSources"));
            allDisSources.AddRange(GetStringListParameter(input.Parameters, "statusDisadvantageSources"));

            if (allAdvSources.Count > 0 && allDisSources.Count > 0)
            {
                combinedState = AdvantageState.Normal;
            }
            else if (allAdvSources.Count > 0)
            {
                combinedState = AdvantageState.Advantage;
            }
            else if (allDisSources.Count > 0)
            {
                combinedState = AdvantageState.Disadvantage;
            }
            else
            {
                combinedState = AdvantageState.Normal;
            }

            // Roll with advantage/disadvantage
            int naturalRoll;
            int[] rollValues = null;

            if (combinedState == AdvantageState.Advantage)
            {
                var (rollResult, r1, r2) = _dice.RollWithAdvantage();
                naturalRoll = rollResult;
                rollValues = new[] { r1, r2 };
            }
            else if (combinedState == AdvantageState.Disadvantage)
            {
                var (rollResult, r1, r2) = _dice.RollWithDisadvantage();
                naturalRoll = rollResult;
                rollValues = new[] { r1, r2 };
            }
            else
            {
                naturalRoll = _dice.RollD20();
            }

            // Halfling Lucky: reroll 1s on attack rolls
            if (naturalRoll == 1 && input.Source?.Tags != null && input.Source.Tags.Contains("lucky_reroll"))
            {
                int reroll = _dice.RollD20();
                naturalRoll = reroll; // Must use the new roll
            }

            // Apply modifiers
            float baseValue = naturalRoll + input.BaseValue;
            var (finalValue, appliedMods) = attackerMods.Apply(baseValue, ModifierTarget.AttackRoll, context, _dice);
            var (finalValueGlobal, globalMods) = _globalModifiers.Apply(finalValue, ModifierTarget.AttackRoll, context, _dice);

            // Check target AC
            float targetAC = input.Target != null ? GetArmorClass(input.Target) : input.DC;

            // Apply cover AC bonus if provided
            int coverACBonus = 0;
            if (input.Parameters.TryGetValue("coverACBonus", out var coverObj) && coverObj is int coverVal)
            {
                coverACBonus = coverVal;
                targetAC += coverACBonus;
            }

            int criticalThreshold = 20;
            if (input.Parameters.TryGetValue("criticalThreshold", out var thresholdObj))
            {
                if (thresholdObj is int intThreshold)
                    criticalThreshold = Math.Clamp(intThreshold, 2, 20);
                else if (int.TryParse(thresholdObj?.ToString(), out var parsedThreshold))
                    criticalThreshold = Math.Clamp(parsedThreshold, 2, 20);
            }

            bool isHit = finalValueGlobal >= targetAC;
            bool isCrit = naturalRoll >= criticalThreshold;
            bool isCritFail = naturalRoll == 1;

            // Critical always hits, crit fail always misses
            if (isCrit) isHit = true;
            if (isCritFail) isHit = false;

            // Some control states force critical on hit (for example paralyzed targets hit in melee).
            if (GetBoolParameter(input.Parameters, "autoCritOnHit") && isHit)
            {
                isCrit = true;
            }

            // Build modifier list including height and cover for breakdown
            var allModifiers = appliedMods.Concat(globalMods).ToList();

            // Add height modifier to breakdown if present
            if (input.Parameters.TryGetValue("heightModifier", out var heightObj) && heightObj is int heightMod && heightMod != 0)
            {
                var heightName = heightMod > 0 ? "High Ground" : "Low Ground";
                allModifiers.Insert(0, Modifier.Flat(
                    heightName,
                    ModifierTarget.AttackRoll,
                    heightMod,
                    heightName
                ));
            }

            // Add cover AC bonus to breakdown if present
            if (coverACBonus != 0)
            {
                var coverName = coverACBonus >= 5 ? "Three-Quarters Cover" : "Half Cover";
                allModifiers.Add(Modifier.Flat(
                    coverName,
                    ModifierTarget.ArmorClass,
                    coverACBonus,
                    coverName
                ));
            }

            var result = new QueryResult
            {
                Input = input,
                BaseValue = input.BaseValue,
                NaturalRoll = naturalRoll,
                FinalValue = finalValueGlobal,
                AppliedModifiers = allModifiers,
                IsSuccess = isHit,
                IsCritical = isCrit,
                IsCriticalFailure = isCritFail,
                AdvantageState = (int)combinedState,
                RollValues = rollValues
            };

            // Populate structured breakdown
            result.Breakdown = BuildRollBreakdown(naturalRoll, (int)finalValueGlobal, allModifiers,
                combinedState, rollValues, allAdvSources, allDisSources);

            return result;
        }

        private static List<string> GetStringListParameter(Dictionary<string, object> parameters, string key)
        {
            var values = new List<string>();
            if (parameters == null || !parameters.TryGetValue(key, out var parameterValue) || parameterValue == null)
                return values;

            switch (parameterValue)
            {
                case string single:
                    if (!string.IsNullOrWhiteSpace(single))
                        values.Add(single);
                    break;
                case IEnumerable<string> list:
                    values.AddRange(list.Where(v => !string.IsNullOrWhiteSpace(v)));
                    break;
                case System.Collections.IEnumerable enumerable:
                    foreach (var entry in enumerable)
                    {
                        var text = entry?.ToString();
                        if (!string.IsNullOrWhiteSpace(text))
                            values.Add(text);
                    }
                    break;
            }

            return values;
        }

        private static bool GetBoolParameter(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var parameterValue) || parameterValue == null)
                return false;

            if (parameterValue is bool boolValue)
                return boolValue;

            return bool.TryParse(parameterValue.ToString(), out var parsed) && parsed;
        }

        /// <summary>
        /// Execute a saving throw query.
        /// </summary>
        public QueryResult RollSave(QueryInput input)
        {
            var context = new ModifierContext
            {
                DefenderId = input.Target?.Id,
                Tags = input.Tags
            };

            var targetMods = input.Target != null ? GetModifiers(input.Target.Id) : new ModifierStack();
            var resolution = targetMods.ResolveAdvantage(ModifierTarget.SavingThrow, context);
            var globalResolution = _globalModifiers.ResolveAdvantage(ModifierTarget.SavingThrow, context);

            // Combine resolutions
            AdvantageState combinedState;
            var allAdvSources = resolution.AdvantageSources.Concat(globalResolution.AdvantageSources).ToList();
            var allDisSources = resolution.DisadvantageSources.Concat(globalResolution.DisadvantageSources).ToList();

            if (allAdvSources.Count > 0 && allDisSources.Count > 0)
            {
                combinedState = AdvantageState.Normal;
            }
            else if (allAdvSources.Count > 0)
            {
                combinedState = AdvantageState.Advantage;
            }
            else if (allDisSources.Count > 0)
            {
                combinedState = AdvantageState.Disadvantage;
            }
            else
            {
                combinedState = AdvantageState.Normal;
            }

            int naturalRoll;
            int[] rollValues = null;

            if (combinedState == AdvantageState.Advantage)
            {
                var (rollResult, r1, r2) = _dice.RollWithAdvantage();
                naturalRoll = rollResult;
                rollValues = new[] { r1, r2 };
            }
            else if (combinedState == AdvantageState.Disadvantage)
            {
                var (rollResult, r1, r2) = _dice.RollWithDisadvantage();
                naturalRoll = rollResult;
                rollValues = new[] { r1, r2 };
            }
            else
            {
                naturalRoll = _dice.RollD20();
            }

            // Halfling Lucky: reroll 1s on saving throws
            if (naturalRoll == 1 && input.Target?.Tags != null && input.Target.Tags.Contains("lucky_reroll"))
            {
                int reroll = _dice.RollD20();
                naturalRoll = reroll; // Must use the new roll
            }

            float baseValue = naturalRoll + input.BaseValue;
            var (finalValue, appliedMods) = targetMods.Apply(baseValue, ModifierTarget.SavingThrow, context, _dice);

            bool success = finalValue >= input.DC;

            var result = new QueryResult
            {
                Input = input,
                BaseValue = input.BaseValue,
                NaturalRoll = naturalRoll,
                FinalValue = finalValue,
                AppliedModifiers = appliedMods,
                IsSuccess = success,
                IsCritical = naturalRoll == 20,
                IsCriticalFailure = naturalRoll == 1,
                AdvantageState = (int)combinedState,
                RollValues = rollValues
            };

            // Populate structured breakdown
            result.Breakdown = BuildRollBreakdown(naturalRoll, (int)finalValue, appliedMods,
                combinedState, rollValues, allAdvSources, allDisSources);

            return result;
        }

        /// <summary>
        /// Execute a contested check (e.g., shove, grapple escape).
        /// Both combatants roll and compare results.
        /// </summary>
        /// <param name="attacker">The initiating combatant.</param>
        /// <param name="defender">The defending combatant.</param>
        /// <param name="attackerMod">Attacker's base modifier (e.g., Athletics bonus).</param>
        /// <param name="defenderMod">Defender's base modifier (e.g., Athletics or Acrobatics bonus).</param>
        /// <param name="attackerSkill">Name of attacker's skill/ability for breakdown.</param>
        /// <param name="defenderSkill">Name of defender's skill/ability for breakdown.</param>
        /// <param name="tiePolicy">How to resolve ties (default: defender wins).</param>
        /// <returns>ContestResult with full breakdown of both rolls.</returns>
        public ContestResult Contest(
            Combatant attacker,
            Combatant defender,
            int attackerMod,
            int defenderMod,
            string attackerSkill = "Check",
            string defenderSkill = "Check",
            TiePolicy tiePolicy = TiePolicy.DefenderWins)
        {
            // Roll for attacker
            var attackerContext = new ModifierContext
            {
                AttackerId = attacker?.Id,
                DefenderId = defender?.Id,
                Tags = new HashSet<string> { "contest", attackerSkill.ToLowerInvariant() }
            };

            var attackerModStack = attacker != null ? GetModifiers(attacker.Id) : new ModifierStack();
            var attackerResolution = attackerModStack.ResolveAdvantage(ModifierTarget.SkillCheck, attackerContext);
            var attackerGlobalResolution = _globalModifiers.ResolveAdvantage(ModifierTarget.SkillCheck, attackerContext);

            // Combine attacker resolutions
            AdvantageState attackerState;
            var attackerAdvSources = attackerResolution.AdvantageSources.Concat(attackerGlobalResolution.AdvantageSources).ToList();
            var attackerDisSources = attackerResolution.DisadvantageSources.Concat(attackerGlobalResolution.DisadvantageSources).ToList();

            if (attackerAdvSources.Count > 0 && attackerDisSources.Count > 0)
            {
                attackerState = AdvantageState.Normal;
            }
            else if (attackerAdvSources.Count > 0)
            {
                attackerState = AdvantageState.Advantage;
            }
            else if (attackerDisSources.Count > 0)
            {
                attackerState = AdvantageState.Disadvantage;
            }
            else
            {
                attackerState = AdvantageState.Normal;
            }

            int naturalRollA;
            if (attackerState == AdvantageState.Advantage)
            {
                var (rollResult, _, _) = _dice.RollWithAdvantage();
                naturalRollA = rollResult;
            }
            else if (attackerState == AdvantageState.Disadvantage)
            {
                var (rollResult, _, _) = _dice.RollWithDisadvantage();
                naturalRollA = rollResult;
            }
            else
            {
                naturalRollA = _dice.RollD20();
            }

            float attackerBase = naturalRollA + attackerMod;
            var (attackerFinal, attackerAppliedMods) = attackerModStack.Apply(attackerBase, ModifierTarget.SkillCheck, attackerContext, _dice);
            var (attackerFinalGlobal, attackerGlobalMods) = _globalModifiers.Apply(attackerFinal, ModifierTarget.SkillCheck, attackerContext, _dice);
            int rollA = (int)attackerFinalGlobal;

            // Roll for defender
            var defenderContext = new ModifierContext
            {
                AttackerId = attacker?.Id,
                DefenderId = defender?.Id,
                Tags = new HashSet<string> { "contest", defenderSkill.ToLowerInvariant() }
            };

            var defenderModStack = defender != null ? GetModifiers(defender.Id) : new ModifierStack();
            var defenderResolution = defenderModStack.ResolveAdvantage(ModifierTarget.SkillCheck, defenderContext);
            var defenderGlobalResolution = _globalModifiers.ResolveAdvantage(ModifierTarget.SkillCheck, defenderContext);

            // Combine defender resolutions
            AdvantageState defenderState;
            var defenderAdvSources = defenderResolution.AdvantageSources.Concat(defenderGlobalResolution.AdvantageSources).ToList();
            var defenderDisSources = defenderResolution.DisadvantageSources.Concat(defenderGlobalResolution.DisadvantageSources).ToList();

            if (defenderAdvSources.Count > 0 && defenderDisSources.Count > 0)
            {
                defenderState = AdvantageState.Normal;
            }
            else if (defenderAdvSources.Count > 0)
            {
                defenderState = AdvantageState.Advantage;
            }
            else if (defenderDisSources.Count > 0)
            {
                defenderState = AdvantageState.Disadvantage;
            }
            else
            {
                defenderState = AdvantageState.Normal;
            }

            int naturalRollB;
            if (defenderState == AdvantageState.Advantage)
            {
                var (rollResult, _, _) = _dice.RollWithAdvantage();
                naturalRollB = rollResult;
            }
            else if (defenderState == AdvantageState.Disadvantage)
            {
                var (rollResult, _, _) = _dice.RollWithDisadvantage();
                naturalRollB = rollResult;
            }
            else
            {
                naturalRollB = _dice.RollD20();
            }

            float defenderBase = naturalRollB + defenderMod;
            var (defenderFinal, defenderAppliedMods) = defenderModStack.Apply(defenderBase, ModifierTarget.SkillCheck, defenderContext, _dice);
            var (defenderFinalGlobal, defenderGlobalMods) = _globalModifiers.Apply(defenderFinal, ModifierTarget.SkillCheck, defenderContext, _dice);
            int rollB = (int)defenderFinalGlobal;

            // Determine winner
            int margin = rollA - rollB;
            ContestWinner winner;

            if (margin > 0)
            {
                winner = ContestWinner.Attacker;
            }
            else if (margin < 0)
            {
                winner = ContestWinner.Defender;
            }
            else
            {
                // Tie - apply policy
                winner = tiePolicy switch
                {
                    TiePolicy.AttackerWins => ContestWinner.Attacker,
                    TiePolicy.NoWinner => ContestWinner.Tie,
                    _ => ContestWinner.Defender
                };
            }

            // Build breakdowns
            var allAttackerMods = attackerAppliedMods.Concat(attackerGlobalMods).ToList();
            var allDefenderMods = defenderAppliedMods.Concat(defenderGlobalMods).ToList();

            string breakdownA = BuildContestBreakdown(attackerSkill, naturalRollA, attackerMod, allAttackerMods, rollA, attackerState);
            string breakdownB = BuildContestBreakdown(defenderSkill, naturalRollB, defenderMod, allDefenderMods, rollB, defenderState);

            return new ContestResult
            {
                RollA = rollA,
                RollB = rollB,
                NaturalRollA = naturalRollA,
                NaturalRollB = naturalRollB,
                Winner = winner,
                BreakdownA = breakdownA,
                BreakdownB = breakdownB,
                Margin = margin
            };
        }

        /// <summary>
        /// Helper to build a breakdown string for contest rolls.
        /// </summary>
        private string BuildContestBreakdown(string skillName, int naturalRoll, int baseMod, List<Modifier> appliedMods, int total, AdvantageState advState)
        {
            var parts = new List<string> { $"{skillName}: {naturalRoll}" };

            if (baseMod != 0)
            {
                parts.Add($"{(baseMod >= 0 ? "+" : "")}{baseMod}");
            }

            foreach (var mod in appliedMods)
            {
                parts.Add(mod.ToString());
            }

            parts.Add($"= {total}");

            if (advState == AdvantageState.Advantage) parts.Add("(ADV)");
            if (advState == AdvantageState.Disadvantage) parts.Add("(DIS)");

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Build a structured RollBreakdown from roll components.
        /// </summary>
        private RollBreakdown BuildRollBreakdown(int naturalRoll, int total, List<Modifier> appliedMods,
            AdvantageState advState, int[] rollValues, List<string> advSources, List<string> disSources)
        {
            var breakdown = new RollBreakdown
            {
                NaturalRoll = naturalRoll,
                Total = total,
                HasAdvantage = advState == AdvantageState.Advantage,
                HasDisadvantage = advState == AdvantageState.Disadvantage,
                AdvantageSources = advSources ?? new List<string>(),
                DisadvantageSources = disSources ?? new List<string>()
            };

            // Set advantage rolls if available
            if (rollValues != null && rollValues.Length == 2)
            {
                int discarded = rollValues[0] == naturalRoll ? rollValues[1] : rollValues[0];
                breakdown.AdvantageRolls = (naturalRoll, discarded);
            }

            // Convert applied modifiers with category detection
            foreach (var mod in appliedMods)
            {
                var category = CategorizeModifier(mod);
                breakdown.AddModifier(mod.Source ?? mod.Name ?? "Unknown", (int)mod.Value, category);
            }

            return breakdown;
        }

        /// <summary>
        /// Categorize a Modifier based on its properties for UI display.
        /// </summary>
        private static BreakdownCategory CategorizeModifier(Modifier mod)
        {
            var source = (mod.Source ?? mod.Name ?? "").ToLowerInvariant();

            if (source.Contains("proficiency"))
                return BreakdownCategory.Proficiency;

            if (source.Contains("strength") || source.Contains("dexterity") ||
                source.Contains("constitution") || source.Contains("intelligence") ||
                source.Contains("wisdom") || source.Contains("charisma") ||
                source.Contains("str") || source.Contains("dex") ||
                source.Contains("con") || source.Contains("int") ||
                source.Contains("wis") || source.Contains("cha"))
                return BreakdownCategory.Ability;

            if (source.Contains("weapon") || source.Contains("armor") ||
                source.Contains("shield") || source.Contains("equipment") ||
                source.Contains("magic item") || source.Contains("ring") ||
                source.Contains("amulet"))
                return BreakdownCategory.Equipment;

            if (source.Contains("cover") || source.Contains("high ground") ||
                source.Contains("low ground") || source.Contains("height") ||
                source.Contains("flanking") || source.Contains("prone"))
                return BreakdownCategory.Situational;

            if (source.Contains("bless") || source.Contains("bane") ||
                source.Contains("curse") || source.Contains("buff") ||
                source.Contains("debuff") || source.Contains("status") ||
                source.Contains("poisoned") || source.Contains("frightened"))
                return BreakdownCategory.Status;

            if (mod.Type == ModifierType.Advantage || mod.Type == ModifierType.Disadvantage)
                return BreakdownCategory.Advantage;

            return BreakdownCategory.Base;
        }

        /// <summary>
        /// Roll damage with modifiers using the damage pipeline.
        /// </summary>
        public QueryResult RollDamage(QueryInput input)
        {
            var context = new ModifierContext
            {
                AttackerId = input.Source?.Id,
                DefenderId = input.Target?.Id,
                Tags = input.Tags
            };

            // Collect all modifiers from source and target
            var attackerMods = input.Source != null ? GetModifiers(input.Source.Id) : new ModifierStack();
            var targetMods = input.Target != null ? GetModifiers(input.Target.Id) : new ModifierStack();

            var allModifiers = new List<Modifier>();
            allModifiers.AddRange(attackerMods.GetModifiers(ModifierTarget.DamageDealt, context));
            allModifiers.AddRange(targetMods.GetModifiers(ModifierTarget.DamageTaken, context));

            // Get target's current resources for layer calculation
            int targetTempHP = input.Target?.Resources.TemporaryHP ?? 0;
            int targetCurrentHP = input.Target?.Resources.CurrentHP ?? 100;

            // Execute damage pipeline
            var pipelineResult = DamagePipeline.Calculate(
                baseDamage: (int)input.BaseValue,
                modifiers: allModifiers,
                targetTempHP: targetTempHP,
                targetCurrentHP: targetCurrentHP,
                targetBarrier: 0 // Barrier system not yet implemented
            );

            // Convert to QueryResult for backward compatibility
            return new QueryResult
            {
                Input = input,
                BaseValue = pipelineResult.BaseDamage,
                FinalValue = pipelineResult.FinalDamage,
                AppliedModifiers = allModifiers,
                IsSuccess = true
            };
        }

        /// <summary>
        /// Roll healing with modifiers.
        /// Uses ModifierTarget.HealingReceived to allow statuses/effects to reduce or prevent healing.
        /// </summary>
        public QueryResult RollHealing(QueryInput input)
        {
            var context = new ModifierContext
            {
                AttackerId = input.Source?.Id,
                DefenderId = input.Target?.Id,
                Tags = input.Tags
            };

            float baseHealing = input.BaseValue;

            // Apply target's healing received modifiers
            var targetMods = input.Target != null ? GetModifiers(input.Target.Id) : new ModifierStack();
            var (finalHealing, appliedMods) = targetMods.Apply(baseHealing, ModifierTarget.HealingReceived, context, _dice);

            // Floor at 0 - negative modifiers cannot turn healing into damage
            finalHealing = Math.Max(0, finalHealing);

            return new QueryResult
            {
                Input = input,
                BaseValue = baseHealing,
                FinalValue = finalHealing,
                AppliedModifiers = appliedMods,
                IsSuccess = true
            };
        }

        /// <summary>
        /// Calculate hit chance percentage.
        /// </summary>
        public QueryResult CalculateHitChance(QueryInput input)
        {
            float targetAC = input.Target != null ? GetArmorClass(input.Target) : input.DC;

            // Base hit chance with d20
            float neededRoll = targetAC - input.BaseValue;
            float hitChance = Math.Clamp((21 - neededRoll) / 20f * 100f, 5f, 95f);

            // Adjust for advantage/disadvantage
            var context = new ModifierContext
            {
                AttackerId = input.Source?.Id,
                DefenderId = input.Target?.Id,
                Tags = input.Tags
            };

            var attackerMods = input.Source != null ? GetModifiers(input.Source.Id) : new ModifierStack();
            int advState = attackerMods.GetAdvantageState(ModifierTarget.AttackRoll, context);

            if (advState > 0)
            {
                // Advantage roughly: 1 - (1-p)^2
                float p = hitChance / 100f;
                hitChance = (1 - (1 - p) * (1 - p)) * 100f;
            }
            else if (advState < 0)
            {
                // Disadvantage roughly: p^2
                float p = hitChance / 100f;
                hitChance = p * p * 100f;
            }

            return new QueryResult
            {
                Input = input,
                BaseValue = 0,
                FinalValue = hitChance,
                AdvantageState = Math.Sign(advState),
                IsSuccess = true
            };
        }

        /// <summary>
        /// Get a combatant's armor class with modifiers.
        /// </summary>
        public float GetArmorClass(Combatant combatant)
        {
            // Use combatant's computed BaseAC from character build, or default 10
            float baseAC = combatant?.Stats?.BaseAC ?? 10;

            var context = new ModifierContext { DefenderId = combatant.Id };
            var mods = GetModifiers(combatant.Id);
            var (finalAC, _) = mods.Apply(baseAC, ModifierTarget.ArmorClass, context, _dice);

            return finalAC;
        }

        /// <summary>
        /// Clean up modifiers for a removed combatant.
        /// </summary>
        public void RemoveCombatant(string combatantId)
        {
            _combatantModifiers.Remove(combatantId);
            Events.UnsubscribeByOwner(combatantId);
            RuleWindows.UnregisterByOwner(combatantId);
        }

        /// <summary>
        /// Reset engine state.
        /// </summary>
        public void Reset()
        {
            _globalModifiers.Clear();
            _combatantModifiers.Clear();
            Events.ClearHistory();
            Events.ClearSubscriptions();
            RuleWindows.Clear();
        }
    }
}
