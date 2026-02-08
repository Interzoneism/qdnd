using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Tracks combat state and adjusts AI behavior dynamically.
    /// Implements adaptive difficulty and tactical adaptation.
    /// </summary>
    public class AdaptiveBehavior
    {
        /// <summary>
        /// Behavioral modifier applied based on combat conditions.
        /// </summary>
        public class BehaviorModifier
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public Dictionary<string, float> WeightMultipliers { get; set; } = new();
            public float RandomFactorOverride { get; set; } = -1f; // -1 = no override
            public bool ForceFocusFire { get; set; }
            public bool ForceDisengage { get; set; }
        }

        // Thresholds
        private const float LOW_HP_THRESHOLD = 0.3f;
        private const float OUTNUMBERED_RATIO = 0.6f;
        private const float DOMINANT_RATIO = 1.5f;
        private const float LOSING_HP_RATIO = 0.5f;

        /// <summary>
        /// Evaluate combat conditions and return applicable behavior modifiers.
        /// </summary>
        public List<BehaviorModifier> EvaluateConditions(
            Combatant actor,
            AIProfile profile,
            IEnumerable<Combatant> allCombatants)
        {
            var modifiers = new List<BehaviorModifier>();
            var combatantList = allCombatants?.ToList() ?? new List<Combatant>();
            
            var allies = combatantList.Where(c => c.Faction == actor.Faction && c.IsActive && c.Id != actor.Id).ToList();
            var enemies = combatantList.Where(c => c.Faction != actor.Faction && c.IsActive && c.Resources?.CurrentHP > 0).ToList();
            
            if (enemies.Count == 0) return modifiers;

            // 1. Self-preservation when low HP
            float hpPercent = (float)actor.Resources.CurrentHP / Math.Max(1, actor.Resources.MaxHP);
            if (hpPercent < LOW_HP_THRESHOLD)
            {
                modifiers.Add(new BehaviorModifier
                {
                    Name = "desperate_survival",
                    Description = "Low HP - prioritizing survival",
                    WeightMultipliers = new Dictionary<string, float>
                    {
                        { "self_preservation", 2.5f },
                        { "healing", 2.0f },
                        { "damage", 0.6f },
                        { "positioning", 1.5f }
                    },
                    ForceDisengage = profile.Archetype != AIArchetype.Berserker
                });
            }

            // 2. Outnumbered — become more defensive and coordinated
            float numRatio = (allies.Count + 1f) / Math.Max(1f, enemies.Count);
            if (numRatio < OUTNUMBERED_RATIO)
            {
                modifiers.Add(new BehaviorModifier
                {
                    Name = "outnumbered",
                    Description = "Outnumbered - tightening formation",
                    WeightMultipliers = new Dictionary<string, float>
                    {
                        { "self_preservation", 1.5f },
                        { "positioning", 1.5f },
                        { "kill_potential", 1.3f } // Focus on eliminating threats
                    },
                    ForceFocusFire = true
                });
            }

            // 3. Dominant position — become more aggressive
            if (numRatio > DOMINANT_RATIO)
            {
                modifiers.Add(new BehaviorModifier
                {
                    Name = "dominant",
                    Description = "Numerical advantage - pressing attack",
                    WeightMultipliers = new Dictionary<string, float>
                    {
                        { "damage", 1.3f },
                        { "kill_potential", 1.5f },
                        { "self_preservation", 0.7f }
                    },
                    ForceFocusFire = true
                });
            }

            // 4. Losing badly — all allies low HP
            float teamHpRatio = (allies.Sum(a => (float)a.Resources.CurrentHP / Math.Max(1, a.Resources.MaxHP)) + hpPercent) / (allies.Count + 1);
            if (teamHpRatio < LOSING_HP_RATIO)
            {
                modifiers.Add(new BehaviorModifier
                {
                    Name = "losing_badly",
                    Description = "Team HP critical - desperate measures",
                    WeightMultipliers = new Dictionary<string, float>
                    {
                        { "healing", 2.5f },
                        { "kill_potential", 2.0f },
                        { "self_preservation", 0.5f }
                    },
                    RandomFactorOverride = 0f // No randomness when desperate
                });
            }

            // 5. Last stand — actor is the last ally standing
            if (allies.Count == 0)
            {
                modifiers.Add(new BehaviorModifier
                {
                    Name = "last_stand",
                    Description = "Last combatant standing",
                    WeightMultipliers = new Dictionary<string, float>
                    {
                        { "damage", 1.5f },
                        { "self_preservation", 2.0f },
                        { "kill_potential", 2.0f }
                    },
                    RandomFactorOverride = 0f
                });
            }

            // 6. Bloodied enemy — someone is close to death, press the attack
            var bloodiedEnemy = enemies.FirstOrDefault(e => 
                (float)e.Resources.CurrentHP / Math.Max(1, e.Resources.MaxHP) < 0.25f);
            if (bloodiedEnemy != null)
            {
                modifiers.Add(new BehaviorModifier
                {
                    Name = "finish_them",
                    Description = $"Enemy {bloodiedEnemy.Id} is bloodied - going for the kill",
                    WeightMultipliers = new Dictionary<string, float>
                    {
                        { "kill_potential", 2.0f },
                        { "damage", 1.5f }
                    },
                    ForceFocusFire = true
                });
            }

            return modifiers;
        }

        /// <summary>
        /// Apply behavior modifiers to a profile's weights temporarily.
        /// Returns adjusted weights dictionary.
        /// </summary>
        public Dictionary<string, float> ApplyModifiers(
            AIProfile baseProfile,
            List<BehaviorModifier> modifiers)
        {
            // Start with base weights
            var adjustedWeights = new Dictionary<string, float>(baseProfile.Weights);

            foreach (var modifier in modifiers)
            {
                foreach (var (key, multiplier) in modifier.WeightMultipliers)
                {
                    if (adjustedWeights.TryGetValue(key, out var current))
                    {
                        adjustedWeights[key] = current * multiplier;
                    }
                }
            }

            return adjustedWeights;
        }
    }
}
