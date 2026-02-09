using System.Collections.Generic;

namespace QDND.Data.CharacterModel
{
    /// <summary>
    /// Tracks all proficiencies and expertise for a character.
    /// Proficiencies don't stack — multiple sources granting the same proficiency have no extra effect.
    /// Expertise doubles the proficiency bonus for specific skills.
    /// </summary>
    public class ProficiencySet
    {
        // Saving throw proficiencies (one per ability)
        public HashSet<AbilityType> SavingThrows { get; set; } = new();
        
        // Skill proficiencies
        public HashSet<Skill> Skills { get; set; } = new();
        
        // Skill expertise (doubles proficiency bonus)
        public HashSet<Skill> Expertise { get; set; } = new();
        
        // Weapon category proficiencies (Simple, Martial)
        public HashSet<WeaponCategory> WeaponCategories { get; set; } = new();
        
        // Specific weapon proficiencies (e.g., Longsword for Elves)
        public HashSet<WeaponType> Weapons { get; set; } = new();
        
        // Armor proficiencies
        public HashSet<ArmorCategory> ArmorCategories { get; set; } = new();
        
        // Check methods
        public bool IsProficientInSave(AbilityType ability) => SavingThrows.Contains(ability);
        public bool IsProficientInSkill(Skill skill) => Skills.Contains(skill);
        public bool HasExpertise(Skill skill) => Expertise.Contains(skill);
        public bool IsProficientWithWeaponCategory(WeaponCategory cat) => WeaponCategories.Contains(cat);
        public bool IsProficientWithWeapon(WeaponType weapon) => Weapons.Contains(weapon);
        public bool IsProficientWithArmor(ArmorCategory armor) => ArmorCategories.Contains(armor);
        
        /// <summary>
        /// Merge another set's proficiencies into this one (union, no stacking).
        /// </summary>
        public void MergeFrom(ProficiencySet other)
        {
            if (other == null) return;
            SavingThrows.UnionWith(other.SavingThrows);
            Skills.UnionWith(other.Skills);
            Expertise.UnionWith(other.Expertise);
            WeaponCategories.UnionWith(other.WeaponCategories);
            Weapons.UnionWith(other.Weapons);
            ArmorCategories.UnionWith(other.ArmorCategories);
        }
    }
}
