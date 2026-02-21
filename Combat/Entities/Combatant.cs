using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Rules.Boosts;
using QDND.Data.CharacterModel;

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
        /// Team identifier for AI targeting (simplifies ally/enemy checks).
        /// </summary>
        public string Team { get; set; }

        /// <summary>
        /// Tags for role identification (healer, tank, damage, etc).
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Owner ID for summons/pets (null if not owned).
        /// </summary>
        public string OwnerId { get; set; }

        /// <summary>
        /// Life/vitality state of this combatant.
        /// </summary>
        public CombatantLifeState LifeState { get; set; } = CombatantLifeState.Alive;

        /// <summary>
        /// Participation state in the current encounter.
        /// </summary>
        public CombatantParticipationState ParticipationState { get; set; } = CombatantParticipationState.InFight;

        /// <summary>
        /// Number of successful death saving throws (0-3).
        /// </summary>
        public int DeathSaveSuccesses { get; set; } = 0;

        /// <summary>
        /// Number of failed death saving throws (0-3).
        /// </summary>
        public int DeathSaveFailures { get; set; } = 0;

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
        /// Non-HP resources (spell slots, class charges, etc.).
        /// DEPRECATED: Use ActionResources instead for BG3-style resource management.
        /// </summary>
        public CombatantResourcePool ResourcePool { get; } = new CombatantResourcePool();

        /// <summary>
        /// BG3-style action resources (spell slots, rage, ki, etc.) with full definition support.
        /// This is the primary resource system - ResourcePool is legacy.
        /// </summary>
        public QDND.Combat.Services.ResourcePool ActionResources { get; } = new QDND.Combat.Services.ResourcePool();

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
        /// Resolved character build data (null if old-style unit).
        /// </summary>
        public QDND.Data.CharacterModel.ResolvedCharacter ResolvedCharacter { get; set; }

        /// <summary>
        /// Proficiency bonus from character level.
        /// </summary>
        public int ProficiencyBonus { get; set; } = 2;

        /// <summary>
        /// List of ability IDs this combatant knows and can use.
        /// </summary>
        public List<string> KnownActions { get; set; } = new List<string>();

        /// <summary>
        /// Number of extra attacks this combatant gets with weapon attacks.
        /// 0 = 1 attack total, 1 = 2 attacks total (Extra Attack), 2 = 3 attacks total (Improved Extra Attack)
        /// </summary>
        public int ExtraAttacks { get; set; } = 0;

        /// <summary>
        /// Equipment loadout for this combatant.
        /// </summary>
        public EquipmentLoadout Equipment { get; set; }

        /// <summary>
        /// Resolved main-hand weapon definition (null = unarmed).
        /// </summary>
        public WeaponDefinition MainHandWeapon { get; set; }

        /// <summary>
        /// Resolved off-hand weapon definition (null = empty/shield).
        /// </summary>
        public WeaponDefinition OffHandWeapon { get; set; }

        /// <summary>
        /// Resolved armor definition (null = unarmored).
        /// </summary>
        public ArmorDefinition EquippedArmor { get; set; }

        /// <summary>
        /// Whether combatant has a shield equipped.
        /// </summary>
        public bool HasShield { get; set; } = false;

        /// <summary>The creature's size category (default Medium).</summary>
        public CreatureSize CreatureSize { get; set; } = CreatureSize.Medium;

        /// <summary>
        /// Container for active boosts currently affecting this combatant.
        /// Boosts are stat modifiers granted by statuses, passives, equipment, etc.
        /// Use BoostApplicator to add/remove boosts and BoostEvaluator to query their effects.
        /// </summary>
        public BoostContainer Boosts { get; private set; } = new BoostContainer();

        /// <summary>
        /// List of passive ability IDs this combatant has.
        /// Passives grant permanent boosts and event-driven effects.
        /// Populated from race, class, feats, etc.
        /// </summary>
        public List<string> PassiveIds { get; set; } = new List<string>();

        /// <summary>
        /// Manages passive abilities on this combatant.
        /// Handles granting/revoking passives and applying their boosts.
        /// </summary>
        public QDND.Combat.Passives.PassiveManager PassiveManager { get; private set; }

        /// <summary>
        /// Path to the portrait texture for this combatant.
        /// Currently assigned randomly from prototype placeholder portraits.
        /// </summary>
        // TODO: Replace random portrait assignment with proper character-specific portraits
        public string PortraitPath { get; set; }

        /// <summary>
        /// Optional scene path for spawning a character-specific 3D visual.
        /// </summary>
        public string ScenePath { get; set; }

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
            ActionBudget = new ActionBudget(global::QDND.Combat.Actions.ActionBudget.DefaultMaxMovement);
            
            // Initialize passive manager and set owner
            PassiveManager = new QDND.Combat.Passives.PassiveManager { Owner = this };
        }

        /// <summary>
        /// Check if combatant can act (take turns, use abilities).
        /// Requires being alive and participating in the fight.
        /// </summary>
        public bool CanAct => LifeState == CombatantLifeState.Alive && ParticipationState == CombatantParticipationState.InFight;

        /// <summary>
        /// Check if combatant is participating in the current fight.
        /// </summary>
        public bool IsInFight => ParticipationState == CombatantParticipationState.InFight;

        /// <summary>
        /// Check if combatant can be targeted by abilities/attacks.
        /// Combatants are targetable if they're still in the fight (even if dead/downed).
        /// </summary>
        public bool IsTargetable => ParticipationState == CombatantParticipationState.InFight;

        /// <summary>
        /// Check if combatant is active in combat (backwards compatibility).
        /// Equivalent to CanAct.
        /// </summary>
        public bool IsActive => CanAct;

        /// <summary>
        /// Reset death saving throw counters.
        /// </summary>
        public void ResetDeathSaves()
        {
            DeathSaveSuccesses = 0;
            DeathSaveFailures = 0;
        }

        /// <summary>
        /// Add an active boost to this combatant.
        /// Called by BoostApplicator - prefer using BoostApplicator.ApplyBoosts() instead of calling directly.
        /// </summary>
        /// <param name="boost">The active boost to add</param>
        public void AddBoost(ActiveBoost boost)
        {
            if (boost == null)
                return;

            Boosts.AddBoost(boost);
        }

        /// <summary>
        /// Remove all boosts from a specific source.
        /// Called when a status expires, equipment is removed, etc.
        /// </summary>
        /// <param name="source">The type of source (e.g., "Status", "Passive")</param>
        /// <param name="sourceId">The instance ID of the source (e.g., "BLESSED")</param>
        /// <returns>The number of boosts removed</returns>
        public int RemoveBoostsFrom(string source, string sourceId)
        {
            return Boosts.RemoveBoostsFrom(source, sourceId);
        }

        /// <summary>
        /// Get all active boosts on this combatant.
        /// </summary>
        /// <returns>Read-only list of active boosts</returns>
        public IReadOnlyList<ActiveBoost> GetBoosts()
        {
            return Boosts.AllBoosts;
        }

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
