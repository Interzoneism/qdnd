using System.Collections.Generic;

namespace QDND.Combat.UI.Screens
{
    /// <summary>
    /// Unified data transfer object for the character inventory screen.
    /// Feeds the three-column layout: Info Panel | Equipment | Backpack.
    /// </summary>
    public class CharacterDisplayData
    {
        // ── Identity ───────────────────────────────────────────────
        public string Name { get; set; }
        public string Race { get; set; }
        public string Class { get; set; }
        public string Subclass { get; set; }
        public string Background { get; set; }
        public int Level { get; set; }

        // ── Hit Points ─────────────────────────────────────────────
        public int HpCurrent { get; set; }
        public int HpMax { get; set; }
        public int TempHp { get; set; }

        // ── Ability Scores ─────────────────────────────────────────
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Constitution { get; set; }
        public int Intelligence { get; set; }
        public int Wisdom { get; set; }
        public int Charisma { get; set; }

        /// <summary>
        /// Primary spellcasting/attack ability abbreviation (e.g. "INT", "WIS", "CHA").
        /// Highlighted in the Overview sub-tab.
        /// </summary>
        public string PrimaryAbility { get; set; }

        // ── Combat Stats ───────────────────────────────────────────
        public int ArmorClass { get; set; }
        public int Initiative { get; set; }
        public int Speed { get; set; }
        public int ProficiencyBonus { get; set; }

        // ── Detailed View Stats ────────────────────────────────────
        public int DarkvisionRange { get; set; }
        public string CreatureType { get; set; } = "Humanoid";
        public string Size { get; set; } = "Medium";
        public int CharacterWeight { get; set; }
        public int CarryingCapacity { get; set; }

        // ── Weapon Stats ───────────────────────────────────────────
        public int MeleeAttackBonus { get; set; }
        public string MeleeDamageRange { get; set; } = "";
        public string MeleeWeaponIconPath { get; set; } = "";

        public int RangedAttackBonus { get; set; }
        public string RangedDamageRange { get; set; } = "";
        public string RangedWeaponIconPath { get; set; } = "";

        // ── Saving Throws ──────────────────────────────────────────
        public List<string> ProficientSaves { get; set; } = new();
        public Dictionary<string, int> SavingThrowModifiers { get; set; } = new();

        // ── Skills ─────────────────────────────────────────────────
        public Dictionary<string, int> Skills { get; set; } = new();
        public List<string> ProficientSkills { get; set; } = new();
        public List<string> ExpertiseSkills { get; set; } = new();

        // ── Conditions ─────────────────────────────────────────────
        public List<ConditionDisplayData> Conditions { get; set; } = new();

        // ── Resistances ────────────────────────────────────────────
        public List<string> Resistances { get; set; } = new();
        public List<string> Immunities { get; set; } = new();
        public List<string> Vulnerabilities { get; set; } = new();

        // ── Notable Features ───────────────────────────────────────
        public List<FeatureDisplayData> NotableFeatures { get; set; } = new();

        // ── Features (plain names) ─────────────────────────────────
        public List<string> Features { get; set; } = new();

        // ── Proficiencies (for Detailed View) ──────────────────────
        public List<string> ArmorProficiencies { get; set; } = new();
        public List<string> WeaponProficiencies { get; set; } = new();
        public List<string> ToolProficiencies { get; set; } = new();

        // ── Tags ───────────────────────────────────────────────────
        public List<string> Tags { get; set; } = new();

        // ── Resources ──────────────────────────────────────────────
        public Dictionary<string, (int current, int max)> Resources { get; set; } = new();

        // ── Weight ─────────────────────────────────────────────────
        public int WeightCurrent { get; set; }
        public int WeightMax { get; set; } = 150;

        // ── Experience ─────────────────────────────────────────────
        public int Experience { get; set; }
        public int ExperienceToNextLevel { get; set; } = 300;
    }

    /// <summary>
    /// Display data for a single notable feature/passive.
    /// </summary>
    public class FeatureDisplayData
    {
        public string Name { get; set; }
        public string IconPath { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Display data for an active condition/status effect.
    /// </summary>
    public class ConditionDisplayData
    {
        public string Name { get; set; }
        public string IconPath { get; set; }
    }
}
