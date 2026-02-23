using System;
using System.Collections.Generic;

namespace QDND.Data.CharacterModel
{
    /// <summary>
    /// Weapon property flags matching BG3/5e mechanics.
    /// </summary>
    [Flags]
    public enum WeaponProperty
    {
        None = 0,
        Finesse = 1 << 0,      // Use STR or DEX for attack/damage
        Light = 1 << 1,        // Can be used for two-weapon fighting
        Heavy = 1 << 2,        // Small creatures have disadvantage
        TwoHanded = 1 << 3,    // Requires both hands
        Versatile = 1 << 4,    // Can use one or two-handed
        Thrown = 1 << 5,       // Can be thrown as ranged attack
        Ammunition = 1 << 6,   // Requires ammunition
        Loading = 1 << 7,      // One shot per action
        Reach = 1 << 8,        // +5 ft reach
        Special = 1 << 9       // Special rules (see description)
    }

    /// <summary>
    /// Weapon definition — describes a weapon TYPE, not an instance.
    /// Matches BG3/5e weapon properties.
    /// </summary>
    public class WeaponDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public WeaponType WeaponType { get; set; }
        public WeaponCategory Category { get; set; }
        public DamageType DamageType { get; set; }
        public int DamageDiceCount { get; set; } = 1;
        public int DamageDieFaces { get; set; } = 6;   // d6
        public int VersatileDieFaces { get; set; } = 0; // 0 = not versatile
        public int NormalRange { get; set; } = 5;  // feet (5 = melee)
        public int LongRange { get; set; } = 0;    // 0 = no long range
        public WeaponProperty Properties { get; set; } = WeaponProperty.None;
        public int Weight { get; set; } = 0; // pounds

        /// <summary>
        /// Weapon action IDs granted when this weapon is equipped in main hand.
        /// Maps to BG3's BoostsOnEquipMainHand UnlockSpell entries.
        /// </summary>
        public List<string> GrantedActionIds { get; set; } = new();

        // Helper checks
        public bool IsFinesse => Properties.HasFlag(WeaponProperty.Finesse);
        public bool IsLight => Properties.HasFlag(WeaponProperty.Light);
        public bool IsHeavy => Properties.HasFlag(WeaponProperty.Heavy);
        public bool IsTwoHanded => Properties.HasFlag(WeaponProperty.TwoHanded);
        public bool IsVersatile => Properties.HasFlag(WeaponProperty.Versatile);
        public bool IsThrown => Properties.HasFlag(WeaponProperty.Thrown);
        public bool IsRanged => Properties.HasFlag(WeaponProperty.Ammunition) || NormalRange > 5;
        public bool HasReach => Properties.HasFlag(WeaponProperty.Reach);
        
        // Damage string for display (e.g., "1d8")
        public string DamageDice => $"{DamageDiceCount}d{DamageDieFaces}";
        public string VersatileDice => VersatileDieFaces > 0 ? $"{DamageDiceCount}d{VersatileDieFaces}" : null;
    }

    /// <summary>
    /// Armor definition — describes an armor TYPE.
    /// Matches BG3/5e armor properties.
    /// </summary>
    public class ArmorDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public ArmorCategory Category { get; set; }
        public int BaseAC { get; set; }
        public int? MaxDexBonus { get; set; }   // null = unlimited, 0 = none, 2 = medium armor
        public bool StealthDisadvantage { get; set; } = false;
        public int StrengthRequirement { get; set; } = 0;
        public int Weight { get; set; } = 0;

        /// <summary>
        /// BG3-style boost DSL string applied when this item is equipped.
        /// E.g. "AC(1)" for Ring of Protection.
        /// </summary>
        public string BoostString { get; set; }
    }

}
