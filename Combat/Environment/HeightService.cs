using System;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Rules;

namespace QDND.Combat.Environment
{
    /// <summary>
    /// Height advantage type.
    /// </summary>
    public enum HeightAdvantage
    {
        Higher,     // Attacker is higher
        Level,      // Same height
        Lower       // Attacker is lower
    }

    /// <summary>
    /// Result of fall damage calculation.
    /// </summary>
    public class FallDamageResult
    {
        public float FallDistance { get; set; }
        public int Damage { get; set; }
        public bool IsLethal { get; set; }
        public bool IsProne { get; set; } // Knocked prone on landing
    }

    /// <summary>
    /// Service for height-based combat modifiers and fall damage.
    /// </summary>
    public class HeightService
    {
        private readonly RuleEventBus _events;
        
        // Configuration
        public float AdvantageThreshold { get; set; } = 3f;  // Height diff for advantage
        public float DamagePerUnit { get; set; } = 1f;       // Damage per foot of fall (D&D: 1d6 per 10ft)
        public float SafeFallDistance { get; set; } = 10f;   // Fall distance before damage
        public float LethalFallDistance { get; set; } = 200f; // Instant death threshold
        public float ProneThreshold { get; set; } = 10f;     // Fall causes prone

        public HeightService(RuleEventBus events = null)
        {
            _events = events;
        }

        /// <summary>
        /// Get the height difference between two combatants.
        /// </summary>
        public float GetHeightDifference(Combatant from, Combatant to)
        {
            return from.Position.Y - to.Position.Y;
        }

        /// <summary>
        /// Determine advantage based on height.
        /// </summary>
        public HeightAdvantage GetHeightAdvantage(Combatant attacker, Combatant target)
        {
            float diff = GetHeightDifference(attacker, target);
            
            if (diff >= AdvantageThreshold)
                return HeightAdvantage.Higher;
            else if (diff <= -AdvantageThreshold)
                return HeightAdvantage.Lower;
            else
                return HeightAdvantage.Level;
        }

        /// <summary>
        /// Check if attacker has advantage from height.
        /// </summary>
        public bool HasHeightAdvantage(Combatant attacker, Combatant target)
        {
            return GetHeightAdvantage(attacker, target) == HeightAdvantage.Higher;
        }

        /// <summary>
        /// Check if attacker has disadvantage from height.
        /// </summary>
        public bool HasHeightDisadvantage(Combatant attacker, Combatant target)
        {
            return GetHeightAdvantage(attacker, target) == HeightAdvantage.Lower;
        }

        /// <summary>
        /// Get attack modifier based on height.
        /// </summary>
        public int GetAttackModifier(Combatant attacker, Combatant target)
        {
            var advantage = GetHeightAdvantage(attacker, target);
            return advantage switch
            {
                HeightAdvantage.Higher => 2,  // +2 for high ground (BG3 style)
                HeightAdvantage.Lower => -2,  // -2 for low ground
                _ => 0
            };
        }

        /// <summary>
        /// Get damage modifier based on height (for ranged attacks).
        /// </summary>
        public float GetDamageModifier(Combatant attacker, Combatant target, bool isRanged)
        {
            if (!isRanged)
                return 1f;
                
            var advantage = GetHeightAdvantage(attacker, target);
            return advantage switch
            {
                HeightAdvantage.Higher => 1.15f,  // +15% damage from high ground
                HeightAdvantage.Lower => 0.9f,    // -10% damage from low ground
                _ => 1f
            };
        }

        /// <summary>
        /// Calculate fall damage.
        /// </summary>
        public FallDamageResult CalculateFallDamage(float fallDistance)
        {
            var result = new FallDamageResult
            {
                FallDistance = fallDistance
            };

            // Safe fall
            if (fallDistance <= SafeFallDistance)
            {
                result.Damage = 0;
                result.IsProne = false;
                result.IsLethal = false;
                return result;
            }

            // Lethal fall
            if (fallDistance >= LethalFallDistance)
            {
                result.IsLethal = true;
                result.Damage = 9999;
                result.IsProne = true;
                return result;
            }

            // Calculate damage (D&D style: 1d6 per 10ft, we use average 3.5)
            float damageFeet = fallDistance - SafeFallDistance;
            int d6Count = (int)(damageFeet / 10f);
            result.Damage = (int)(d6Count * 3.5f * DamagePerUnit);

            // Prone if significant fall
            result.IsProne = fallDistance >= ProneThreshold;

            return result;
        }

        /// <summary>
        /// Apply fall damage to a combatant.
        /// </summary>
        public FallDamageResult ApplyFallDamage(Combatant combatant, float fallDistance)
        {
            var result = CalculateFallDamage(fallDistance);

            if (result.Damage > 0)
            {
                combatant.Resources.TakeDamage(result.Damage);
                
                _events?.Dispatch(new RuleEvent
                {
                    Type = RuleEventType.DamageTaken,
                    TargetId = combatant.Id,
                    Value = result.Damage,
                    Data = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "source", "fall" },
                        { "fallDistance", fallDistance },
                        { "damageType", "bludgeoning" }
                    }
                });
            }

            if (result.IsLethal)
            {
                _events?.Dispatch(new RuleEvent
                {
                    Type = RuleEventType.CombatantDied,
                    TargetId = combatant.Id,
                    Data = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "cause", "lethal_fall" }
                    }
                });
            }

            return result;
        }

        /// <summary>
        /// Process position change for fall damage.
        /// </summary>
        public FallDamageResult ProcessPositionChange(Combatant combatant, Vector3 oldPosition, Vector3 newPosition, bool isJump = false)
        {
            float heightChange = oldPosition.Y - newPosition.Y;
            
            // Only apply if falling down (not jumping up or moving laterally)
            if (heightChange <= 0 || isJump)
                return null;

            return ApplyFallDamage(combatant, heightChange);
        }

        /// <summary>
        /// Get safe landing spots from a position.
        /// </summary>
        public float GetMaxSafeFallHeight()
        {
            return SafeFallDistance;
        }

        /// <summary>
        /// Check if a jump is safe (no fall damage on landing).
        /// </summary>
        public bool IsJumpSafe(Vector3 from, Vector3 to, float jumpHeight = 0)
        {
            float landingHeight = from.Y + jumpHeight - to.Y;
            return landingHeight <= SafeFallDistance;
        }
    }
}
