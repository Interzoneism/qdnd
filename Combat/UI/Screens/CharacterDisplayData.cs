using System.Collections.Generic;

namespace QDND.Combat.UI.Screens
{
    /// <summary>
    /// Unified data transfer object for the character inventory screen.
    /// Extends the basic CharacterSheetData with weapon stats, resistances,
    /// notable features, weight, and experience — everything the unified
    /// Equipment/Inventory/Character tabs need to render.
    /// </summary>
    public class CharacterDisplayData
    {
        // ── Identity ───────────────────────────────────────────────
        public string Name { get; set; }
        public string Race { get; set; }
        public string Class { get; set; }
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

        // ── Combat Stats ───────────────────────────────────────────
        public int ArmorClass { get; set; }
        public int Initiative { get; set; }
        public int Speed { get; set; }
        public int ProficiencyBonus { get; set; }

        // ── Weapon Stats ───────────────────────────────────────────
        public int MeleeAttackBonus { get; set; }
        public string MeleeDamageRange { get; set; } = "";
        public string MeleeWeaponIconPath { get; set; } = "";

        public int RangedAttackBonus { get; set; }
        public string RangedDamageRange { get; set; } = "";
        public string RangedWeaponIconPath { get; set; } = "";

        // ── Saving Throws ──────────────────────────────────────────
        public List<string> ProficientSaves { get; set; } = new();

        // ── Skills ─────────────────────────────────────────────────
        public Dictionary<string, int> Skills { get; set; } = new();

        // ── Resistances ────────────────────────────────────────────
        public List<string> Resistances { get; set; } = new();
        public List<string> Immunities { get; set; } = new();
        public List<string> Vulnerabilities { get; set; } = new();

        // ── Notable Features ───────────────────────────────────────
        public List<FeatureDisplayData> NotableFeatures { get; set; } = new();

        // ── Features (plain names for character tab) ───────────────
        public List<string> Features { get; set; } = new();

        // ── Resources ──────────────────────────────────────────────
        public Dictionary<string, (int current, int max)> Resources { get; set; } = new();

        // ── Weight ─────────────────────────────────────────────────
        public int WeightCurrent { get; set; }
        public int WeightMax { get; set; } = 150; // STR × 15

        // ── Experience ─────────────────────────────────────────────
        public int Experience { get; set; }
        public int ExperienceToNextLevel { get; set; } = 300;
    }

    /// <summary>
    /// Display data for a single notable feature/passive in the equipment tab.
    /// </summary>
    public class FeatureDisplayData
    {
        public string Name { get; set; }
        public string IconPath { get; set; }
        public string Description { get; set; }
    }
}
