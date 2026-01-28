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
        Custom
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

        public DiceRoller(int seed)
        {
            _rng = new Random(seed);
        }

        public DiceRoller()
        {
            _rng = new Random();
        }

        public void SetSeed(int seed)
        {
            _rng = new Random(seed);
        }

        /// <summary>
        /// Roll a d20.
        /// </summary>
        public int RollD20() => _rng.Next(1, 21);

        /// <summary>
        /// Roll arbitrary dice (e.g., 2d6+3).
        /// </summary>
        public int Roll(int count, int sides, int bonus = 0)
        {
            int total = bonus;
            for (int i = 0; i < count; i++)
            {
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

        public RulesEngine(int? seed = null)
        {
            _dice = seed.HasValue ? new DiceRoller(seed.Value) : new DiceRoller();
        }

        public void SetSeed(int seed)
        {
            _dice.SetSeed(seed);
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

            // Get combined modifiers
            var attackerMods = input.Source != null ? GetModifiers(input.Source.Id) : new ModifierStack();
            int advState = attackerMods.GetAdvantageState(ModifierTarget.AttackRoll, context)
                         + _globalModifiers.GetAdvantageState(ModifierTarget.AttackRoll, context);

            // Roll with advantage/disadvantage
            int naturalRoll;
            int[] rollValues = null;

            if (advState > 0)
            {
                var (result, r1, r2) = _dice.RollWithAdvantage();
                naturalRoll = result;
                rollValues = new[] { r1, r2 };
            }
            else if (advState < 0)
            {
                var (result, r1, r2) = _dice.RollWithDisadvantage();
                naturalRoll = result;
                rollValues = new[] { r1, r2 };
            }
            else
            {
                naturalRoll = _dice.RollD20();
            }

            // Apply modifiers
            float baseValue = naturalRoll + input.BaseValue;
            var (finalValue, appliedMods) = attackerMods.Apply(baseValue, ModifierTarget.AttackRoll, context);
            var (finalValueGlobal, globalMods) = _globalModifiers.Apply(finalValue, ModifierTarget.AttackRoll, context);

            // Check target AC
            float targetAC = input.Target != null ? GetArmorClass(input.Target) : input.DC;
            bool isHit = finalValueGlobal >= targetAC;
            bool isCrit = naturalRoll == 20;
            bool isCritFail = naturalRoll == 1;

            // Critical always hits, crit fail always misses
            if (isCrit) isHit = true;
            if (isCritFail) isHit = false;

            return new QueryResult
            {
                Input = input,
                BaseValue = input.BaseValue,
                NaturalRoll = naturalRoll,
                FinalValue = finalValueGlobal,
                AppliedModifiers = appliedMods.Concat(globalMods).ToList(),
                IsSuccess = isHit,
                IsCritical = isCrit,
                IsCriticalFailure = isCritFail,
                AdvantageState = Math.Sign(advState),
                RollValues = rollValues
            };
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
            int advState = targetMods.GetAdvantageState(ModifierTarget.SavingThrow, context);

            int naturalRoll;
            int[] rollValues = null;

            if (advState > 0)
            {
                var (result, r1, r2) = _dice.RollWithAdvantage();
                naturalRoll = result;
                rollValues = new[] { r1, r2 };
            }
            else if (advState < 0)
            {
                var (result, r1, r2) = _dice.RollWithDisadvantage();
                naturalRoll = result;
                rollValues = new[] { r1, r2 };
            }
            else
            {
                naturalRoll = _dice.RollD20();
            }

            float baseValue = naturalRoll + input.BaseValue;
            var (finalValue, appliedMods) = targetMods.Apply(baseValue, ModifierTarget.SavingThrow, context);

            bool success = finalValue >= input.DC;

            return new QueryResult
            {
                Input = input,
                BaseValue = input.BaseValue,
                NaturalRoll = naturalRoll,
                FinalValue = finalValue,
                AppliedModifiers = appliedMods,
                IsSuccess = success,
                IsCritical = naturalRoll == 20,
                IsCriticalFailure = naturalRoll == 1,
                AdvantageState = Math.Sign(advState),
                RollValues = rollValues
            };
        }

        /// <summary>
        /// Roll damage with modifiers.
        /// </summary>
        public QueryResult RollDamage(QueryInput input)
        {
            var context = new ModifierContext
            {
                AttackerId = input.Source?.Id,
                DefenderId = input.Target?.Id,
                Tags = input.Tags
            };

            float baseDamage = input.BaseValue;
            var attackerMods = input.Source != null ? GetModifiers(input.Source.Id) : new ModifierStack();
            var (modifiedDamage, attackMods) = attackerMods.Apply(baseDamage, ModifierTarget.DamageDealt, context);

            // Apply target's damage reduction
            var targetMods = input.Target != null ? GetModifiers(input.Target.Id) : new ModifierStack();
            var (finalDamage, defenseMods) = targetMods.Apply(modifiedDamage, ModifierTarget.DamageTaken, context);

            // Floor at 0
            finalDamage = Math.Max(0, finalDamage);

            return new QueryResult
            {
                Input = input,
                BaseValue = baseDamage,
                FinalValue = finalDamage,
                AppliedModifiers = attackMods.Concat(defenseMods).ToList(),
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
            // Base AC (could be expanded with armor/dex)
            float baseAC = 10;
            
            var context = new ModifierContext { DefenderId = combatant.Id };
            var mods = GetModifiers(combatant.Id);
            var (finalAC, _) = mods.Apply(baseAC, ModifierTarget.ArmorClass, context);
            
            return finalAC;
        }

        /// <summary>
        /// Clean up modifiers for a removed combatant.
        /// </summary>
        public void RemoveCombatant(string combatantId)
        {
            _combatantModifiers.Remove(combatantId);
            Events.UnsubscribeByOwner(combatantId);
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
        }
    }
}
