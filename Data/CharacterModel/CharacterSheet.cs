using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Data.CharacterModel
{
    /// <summary>
    /// A complete BG3 character build.
    /// Composes race, class(es), feats, and ability scores into final derived stats.
    /// This does NOT handle combat state (HP, position, action budget) — that's Combatant's job.
    /// CharacterSheet is the "build" that feeds into a Combatant at scenario start.
    /// </summary>
    public class CharacterSheet
    {
        // --- Identity ---
        public string Name { get; set; }
        
        // --- Race ---
        public string RaceId { get; set; }
        public string SubraceId { get; set; }
        
        // --- Class ---
        /// <summary>Class levels: list of (classId, subclassId) pairs in order taken.
        /// First entry is the "starting class" which determines save proficiencies.</summary>
        public List<ClassLevel> ClassLevels { get; set; } = new();
        
        // --- Ability Scores (base, before racial/feat bonuses) ---
        public int BaseStrength { get; set; } = 10;
        public int BaseDexterity { get; set; } = 10;
        public int BaseConstitution { get; set; } = 10;
        public int BaseIntelligence { get; set; } = 10;
        public int BaseWisdom { get; set; } = 10;
        public int BaseCharisma { get; set; } = 10;
        
        // --- Racial Ability Bonuses (flexible +2/+1 in BG3) ---
        public string AbilityBonus2 { get; set; } // Ability getting +2
        public string AbilityBonus1 { get; set; } // Ability getting +1
        
        // --- Feats ---
        public List<string> FeatIds { get; set; } = new();
        
        // --- Background ---
        public string BackgroundId { get; set; }
        public List<string> BackgroundSkills { get; set; } = new(); // 2 skill proficiencies
        
        // --- Derived (computed) ---
        
        /// <summary>Total character level (sum of all class levels).</summary>
        public int TotalLevel => ClassLevels.Count;
        
        /// <summary>Proficiency bonus based on total level.</summary>
        public int ProficiencyBonus => TotalLevel switch
        {
            <= 0 => 2,
            <= 4 => 2,
            <= 8 => 3,
            <= 12 => 4,
            _ => 4  // BG3 caps at 12
        };
        
        /// <summary>Get the level in a specific class.</summary>
        public int GetClassLevel(string classId)
        {
            return ClassLevels.Count(cl => cl.ClassId == classId);
        }
        
        /// <summary>Get the starting (first) class.</summary>
        public string StartingClassId => ClassLevels.Count > 0 ? ClassLevels[0].ClassId : null;
        
        /// <summary>
        /// Compute final ability score including racial bonuses and feat ASIs.
        /// </summary>
        public int GetAbilityScore(AbilityType ability, List<Feature> allFeatures)
        {
            int baseScore = ability switch
            {
                AbilityType.Strength => BaseStrength,
                AbilityType.Dexterity => BaseDexterity,
                AbilityType.Constitution => BaseConstitution,
                AbilityType.Intelligence => BaseIntelligence,
                AbilityType.Wisdom => BaseWisdom,
                AbilityType.Charisma => BaseCharisma,
                _ => 10
            };
            
            // Apply racial +2/+1
            string abilityName = ability.ToString();
            if (AbilityBonus2 != null && AbilityBonus2.Equals(abilityName, StringComparison.OrdinalIgnoreCase))
                baseScore += 2;
            if (AbilityBonus1 != null && AbilityBonus1.Equals(abilityName, StringComparison.OrdinalIgnoreCase))
                baseScore += 1;
            
            // Apply feature-based ability increases
            foreach (var feature in allFeatures)
            {
                if (feature.AbilityScoreIncreases != null && 
                    feature.AbilityScoreIncreases.TryGetValue(abilityName, out int increase))
                {
                    baseScore += increase;
                }
            }
            
            // BG3 cap is 20 (can exceed with specific items, but baseline cap is 20)
            return Math.Min(baseScore, 20);
        }
        
        public static int GetModifier(int score) => (score - 10) / 2;
    }
    
    /// <summary>
    /// Represents one level taken in a specific class.
    /// </summary>
    public class ClassLevel
    {
        public string ClassId { get; set; }
        public string SubclassId { get; set; }
        
        public ClassLevel() { }
        public ClassLevel(string classId, string subclassId = null)
        {
            ClassId = classId;
            SubclassId = subclassId;
        }
    }
}
