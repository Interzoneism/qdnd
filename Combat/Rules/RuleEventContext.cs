using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Abilities;
using QDND.Combat.Entities;

namespace QDND.Combat.Rules
{
    /// <summary>
    /// Mutable payload passed through rule windows.
    /// </summary>
    public class RuleEventContext
    {
        private readonly Dictionary<string, int> _maxSaveBonuses = new(StringComparer.OrdinalIgnoreCase);

        public RuleWindow Window { get; internal set; }
        public Combatant Source { get; set; }
        public Combatant Target { get; set; }
        public AbilityDefinition Ability { get; set; }
        public QueryInput QueryInput { get; set; }
        public QueryResult QueryResult { get; set; }
        public Random Random { get; set; }

        public bool Cancel { get; set; }

        // Action/attack metadata
        public bool IsMeleeWeaponAttack { get; set; }
        public bool IsRangedWeaponAttack { get; set; }
        public bool IsSpellAttack { get; set; }
        public bool IsCriticalHit { get; set; }

        // Damage metadata/mutations
        public string DamageType { get; set; }
        public string DamageDiceFormula { get; set; }
        public int DamageRollValue { get; set; }
        public int FlatDamageBonus { get; private set; }
        public float DamageMultiplier { get; private set; } = 1f;

        // Save metadata/mutations
        public int SaveBonusModifier { get; private set; }
        public int TotalSaveBonus => SaveBonusModifier + _maxSaveBonuses.Values.Sum();

        // Advantage/disadvantage sources propagated into query parameters.
        public List<string> AdvantageSources { get; } = new();
        public List<string> DisadvantageSources { get; } = new();

        public HashSet<string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, object> Data { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void AddDamageBonus(int bonus)
        {
            FlatDamageBonus += bonus;
        }

        public void MultiplyDamage(float multiplier)
        {
            DamageMultiplier *= Math.Max(0f, multiplier);
        }

        public int GetFinalDamageValue()
        {
            var baseValue = DamageRollValue + FlatDamageBonus;
            return (int)MathF.Round(baseValue * DamageMultiplier, MidpointRounding.AwayFromZero);
        }

        public void AddSaveBonus(int bonus)
        {
            SaveBonusModifier += bonus;
        }

        public void AddMaxSaveBonus(string bucket, int bonus)
        {
            if (string.IsNullOrWhiteSpace(bucket))
                return;

            if (_maxSaveBonuses.TryGetValue(bucket, out var existing))
            {
                _maxSaveBonuses[bucket] = Math.Max(existing, bonus);
                return;
            }

            _maxSaveBonuses[bucket] = bonus;
        }

        public void AddAdvantageSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source) || AdvantageSources.Contains(source))
                return;
            AdvantageSources.Add(source);
        }

        public void AddDisadvantageSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source) || DisadvantageSources.Contains(source))
                return;
            DisadvantageSources.Add(source);
        }
    }
}
