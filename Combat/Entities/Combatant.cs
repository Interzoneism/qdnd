using System;
using Godot;
using QDND.Combat.Actions;

namespace QDND.Combat.Entities
{
    /// <summary>
    /// Faction allegiances for combatants.
    /// </summary>
    public enum Faction
    {
        Player,
        Hostile,
        Neutral,
        Ally
    }

    /// <summary>
    /// Resource component for HP and related values.
    /// </summary>
    public class ResourceComponent
    {
        public int MaxHP { get; set; }
        public int CurrentHP { get; set; }
        public int TemporaryHP { get; set; }

        public bool IsAlive => CurrentHP > 0;
        public bool IsDowned => CurrentHP <= 0;

        public ResourceComponent(int maxHP)
        {
            MaxHP = maxHP;
            CurrentHP = maxHP;
            TemporaryHP = 0;
        }

        /// <summary>
        /// Apply damage, consuming temp HP first.
        /// </summary>
        public int TakeDamage(int amount)
        {
            if (amount <= 0) return 0;

            int damageDealt = 0;

            // Consume temporary HP first
            if (TemporaryHP > 0)
            {
                int tempHPAbsorbed = Math.Min(TemporaryHP, amount);
                TemporaryHP -= tempHPAbsorbed;
                amount -= tempHPAbsorbed;
                damageDealt += tempHPAbsorbed;
            }

            // Apply remaining damage to current HP
            if (amount > 0)
            {
                int actualDamage = Math.Min(CurrentHP, amount);
                CurrentHP -= actualDamage;
                damageDealt += actualDamage;
            }

            return damageDealt;
        }

        /// <summary>
        /// Heal the combatant (cannot exceed max HP).
        /// </summary>
        public int Heal(int amount)
        {
            if (amount <= 0) return 0;
            
            int healAmount = Math.Min(amount, MaxHP - CurrentHP);
            CurrentHP += healAmount;
            return healAmount;
        }

        /// <summary>
        /// Add temporary HP (does not stack, takes higher).
        /// </summary>
        public void AddTemporaryHP(int amount)
        {
            TemporaryHP = Math.Max(TemporaryHP, amount);
        }

        public override string ToString()
        {
            string temp = TemporaryHP > 0 ? $"+{TemporaryHP}" : "";
            return $"{CurrentHP}/{MaxHP}{temp}";
        }
    }

    /// <summary>
    /// Base combatant entity representing any battle participant.
    /// Minimal Phase A implementation - full component model comes in Phase B.
    /// </summary>
    public class Combatant
    {
        /// <summary>
        /// Unique identifier for this combatant.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Which side is this combatant on.
        /// </summary>
        public Faction Faction { get; set; }

        /// <summary>
        /// Initiative value for turn order.
        /// </summary>
        public int Initiative { get; set; }

        /// <summary>
        /// Initiative tie-breaker (higher wins ties).
        /// </summary>
        public int InitiativeTiebreaker { get; set; }

        /// <summary>
        /// HP and resource tracking.
        /// </summary>
        public ResourceComponent Resources { get; }

        /// <summary>
        /// Whether this combatant is controlled by player or AI.
        /// </summary>
        public bool IsPlayerControlled => Faction == Faction.Player || Faction == Faction.Ally;

        /// <summary>
        /// Action economy budget for this combatant.
        /// </summary>
        public ActionBudget ActionBudget { get; private set; }

        /// <summary>
        /// World position of this combatant.
        /// </summary>
        public Vector3 Position { get; set; } = Vector3.Zero;

        /// <summary>
        /// Ability scores and derived stats.
        /// </summary>
        public CombatantStats Stats { get; set; }

        /// <summary>
        /// Create a new combatant.
        /// </summary>
        public Combatant(string id, string name, Faction faction, int maxHP, int initiative)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? id;
            Faction = faction;
            Initiative = initiative;
            InitiativeTiebreaker = 0;
            Resources = new ResourceComponent(maxHP);
            ActionBudget = new ActionBudget(30f); // Default 30 movement
        }

        /// <summary>
        /// Check if combatant is active in combat.
        /// </summary>
        public bool IsActive => Resources.IsAlive;

        /// <summary>
        /// Get a hash representing current state for deterministic comparison.
        /// </summary>
        public int GetStateHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Id.GetHashCode();
                hash = hash * 31 + Faction.GetHashCode();
                hash = hash * 31 + Resources.CurrentHP;
                hash = hash * 31 + Resources.MaxHP;
                hash = hash * 31 + Resources.TemporaryHP;
                hash = hash * 31 + Initiative;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{Name}[{Id}] {Faction} HP:{Resources} Init:{Initiative}";
        }
    }
}
