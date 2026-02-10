using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Data.CharacterModel
{
    /// <summary>
    /// Resolves a CharacterSheet into final combat-ready stats.
    /// Follows the deterministic build resolution order from the BG3 guide:
    /// 1. Base ability scores (point buy)
    /// 2. Race/subrace grants
    /// 3. Background grants
    /// 4. Class level progression (including subclass)
    /// 5. Feat grants
    /// 6. Compute derived stats
    /// </summary>
    public class CharacterResolver
    {
        private readonly CharacterDataRegistry _registry;
        
        public CharacterResolver(CharacterDataRegistry registry)
        {
            _registry = registry;
        }
        
        /// <summary>
        /// Resolve a character sheet into a fully computed character.
        /// </summary>
        public ResolvedCharacter Resolve(CharacterSheet sheet)
        {
            var resolved = new ResolvedCharacter();
            resolved.Name = sheet.Name;
            resolved.Sheet = sheet;
            
            // Step 1: Collect all features from all sources
            var allFeatures = new List<Feature>();
            
            // Step 2: Race and subrace features
            var race = _registry.GetRace(sheet.RaceId);
            if (race != null)
            {
                resolved.Speed = race.Speed;
                resolved.DarkvisionRange = race.DarkvisionRange;
                
                // Add race features that are at or below character level
                foreach (var f in race.Features)
                {
                    if (f.GrantedAtLevel <= sheet.TotalLevel)
                        allFeatures.Add(f);
                }
                
                // Subrace
                if (!string.IsNullOrEmpty(sheet.SubraceId))
                {
                    var subrace = race.Subraces?.FirstOrDefault(s => s.Id == sheet.SubraceId);
                    if (subrace != null)
                    {
                        if (subrace.DarkvisionOverride.HasValue)
                            resolved.DarkvisionRange = subrace.DarkvisionOverride.Value;
                        if (subrace.SpeedOverride.HasValue)
                            resolved.Speed = subrace.SpeedOverride.Value;
                        
                        foreach (var f in subrace.Features)
                        {
                            if (f.GrantedAtLevel <= sheet.TotalLevel)
                                allFeatures.Add(f);
                        }
                    }
                }
            }
            
            // Step 3: Background skill proficiencies
            if (sheet.BackgroundSkills != null)
            {
                foreach (var skillName in sheet.BackgroundSkills)
                {
                    if (Enum.TryParse<Skill>(skillName, true, out var skill))
                        resolved.Proficiencies.Skills.Add(skill);
                }
            }
            
            // Step 4: Class progression
            // Starting class gets full proficiencies + save proficiencies
            // Additional classes get multiclass proficiencies only
            var classLevelCounts = new Dictionary<string, int>();
            bool isFirstClass = true;
            int maxExtraAttacks = 0; // Track highest extra attacks value
            
            foreach (var cl in sheet.ClassLevels)
            {
                if (!classLevelCounts.ContainsKey(cl.ClassId))
                    classLevelCounts[cl.ClassId] = 0;
                classLevelCounts[cl.ClassId]++;
                int currentClassLevel = classLevelCounts[cl.ClassId];
                
                var classDef = _registry.GetClass(cl.ClassId);
                if (classDef == null) continue;
                
                // First level of first class: full starting proficiencies + saves
                if (isFirstClass && currentClassLevel == 1)
                {
                    // Saving throw proficiencies (only from starting class)
                    foreach (var saveName in classDef.SavingThrowProficiencies)
                    {
                        if (Enum.TryParse<AbilityType>(saveName, true, out var ability))
                            resolved.Proficiencies.SavingThrows.Add(ability);
                    }
                    
                    ApplyProficiencyGrant(classDef.StartingProficiencies, resolved.Proficiencies);
                    isFirstClass = false;
                }
                else if (currentClassLevel == 1)
                {
                    // Multiclass into new class: limited proficiencies
                    ApplyProficiencyGrant(classDef.MulticlassProficiencies, resolved.Proficiencies);
                }
                
                // Level table features
                if (classDef.LevelTable.TryGetValue(currentClassLevel.ToString(), out var progression))
                {
                    ApplyLevelProgressionResources(progression, classDef.Id, resolved);

                    if (progression.Features != null)
                        allFeatures.AddRange(progression.Features);
                    
                    // Track the highest ExtraAttacks value from any class
                    if (progression.ExtraAttacks.HasValue && progression.ExtraAttacks.Value > maxExtraAttacks)
                        maxExtraAttacks = progression.ExtraAttacks.Value;
                }
                
                // Subclass features
                if (!string.IsNullOrEmpty(cl.SubclassId))
                {
                    var subclass = classDef.Subclasses?.FirstOrDefault(s => s.Id == cl.SubclassId);
                    if (subclass?.LevelTable != null && 
                        subclass.LevelTable.TryGetValue(currentClassLevel.ToString(), out var subProgression))
                    {
                        ApplyLevelProgressionResources(subProgression, classDef.Id, resolved);

                        if (subProgression.Features != null)
                            allFeatures.AddRange(subProgression.Features);
                    }
                }
            }
            
            // Step 5: Feats
            foreach (var featId in sheet.FeatIds)
            {
                var feat = _registry.GetFeat(featId);
                if (feat?.Features != null)
                    allFeatures.AddRange(feat.Features);
            }
            
            // Step 6: Apply all collected features and extra attacks
            resolved.Features = allFeatures;
            resolved.ExtraAttacks = maxExtraAttacks;
            foreach (var feature in allFeatures)
            {
                ApplyFeature(feature, resolved);
            }
            
            // Step 7: Compute final ability scores
            foreach (AbilityType ability in Enum.GetValues(typeof(AbilityType)))
            {
                resolved.AbilityScores[ability] = sheet.GetAbilityScore(ability, allFeatures);
            }
            
            // Step 8: Compute HP
            resolved.MaxHP = ComputeMaxHP(sheet, resolved);
            
            // Step 9: Compute AC base (10 + DEX mod, modified by armor later)
            int dexMod = CharacterSheet.GetModifier(resolved.AbilityScores[AbilityType.Dexterity]);
            resolved.BaseAC = 10 + dexMod;
            
            // Collect all granted abilities
            resolved.AllAbilities = allFeatures
                .Where(f => f.GrantedAbilities != null)
                .SelectMany(f => f.GrantedAbilities)
                .Distinct()
                .ToList();
            
            return resolved;
        }
        
        private int ComputeMaxHP(CharacterSheet sheet, ResolvedCharacter resolved)
        {
            int conMod = CharacterSheet.GetModifier(resolved.AbilityScores[AbilityType.Constitution]);
            int totalHP = 0;
            
            var classLevelCounts = new Dictionary<string, int>();
            foreach (var cl in sheet.ClassLevels)
            {
                if (!classLevelCounts.ContainsKey(cl.ClassId))
                    classLevelCounts[cl.ClassId] = 0;
                classLevelCounts[cl.ClassId]++;
                int currentClassLevel = classLevelCounts[cl.ClassId];
                
                var classDef = _registry.GetClass(cl.ClassId);
                if (classDef == null) continue;
                
                if (classLevelCounts[cl.ClassId] == 1 && cl == sheet.ClassLevels[0])
                {
                    // First level of starting class: max hit die + CON
                    totalHP += classDef.HpAtFirstLevel + conMod;
                }
                else
                {
                    // Subsequent levels: average + CON
                    totalHP += classDef.HpPerLevelAfterFirst + conMod;
                }
            }
            
            // Apply HP bonuses from features (e.g., Dwarven Toughness)
            int hpPerLevelBonus = resolved.Features
                .Where(f => f.HpPerLevel > 0)
                .Sum(f => f.HpPerLevel);
            totalHP += hpPerLevelBonus * sheet.TotalLevel;
            
            int flatHpBonus = resolved.Features.Sum(f => f.HpBonus);
            totalHP += flatHpBonus;
            
            return Math.Max(1, totalHP);
        }
        
        private void ApplyFeature(Feature feature, ResolvedCharacter resolved)
        {
            // Proficiencies
            if (feature.Proficiencies != null)
                ApplyProficiencyGrant(feature.Proficiencies, resolved.Proficiencies);
            
            // Resistances
            if (feature.Resistances != null)
                resolved.DamageResistances.UnionWith(feature.Resistances);
            
            // Immunities
            if (feature.Immunities != null)
                resolved.DamageImmunities.UnionWith(feature.Immunities);
            
            // Condition immunities
            if (feature.ConditionImmunities != null)
                resolved.ConditionImmunities.UnionWith(feature.ConditionImmunities);
            
            // Speed modifier
            if (feature.SpeedModifier != 0)
                resolved.Speed += feature.SpeedModifier;
            
            // Darkvision (take the highest)
            if (feature.DarkvisionRange > resolved.DarkvisionRange)
                resolved.DarkvisionRange = feature.DarkvisionRange;
            
            // Resource grants
            if (feature.ResourceGrants != null)
            {
                foreach (var kvp in feature.ResourceGrants)
                {
                    resolved.Resources[kvp.Key] = kvp.Value; // Overwrite (latest level wins)
                }
            }
        }

        private static void ApplyLevelProgressionResources(LevelProgression progression, string classId, ResolvedCharacter resolved)
        {
            if (progression == null || resolved == null)
                return;

            if (progression.Resources != null)
            {
                foreach (var (resourceId, value) in progression.Resources)
                {
                    resolved.Resources[resourceId] = value;
                }
            }

            if (progression.SpellSlots != null)
            {
                foreach (var (slotKey, slotCount) in progression.SpellSlots)
                {
                    if (int.TryParse(slotKey, out int spellLevel) && spellLevel > 0)
                    {
                        resolved.Resources[$"spell_slot_{spellLevel}"] = slotCount;

                        if (string.Equals(classId, "warlock", StringComparison.OrdinalIgnoreCase))
                        {
                            resolved.Resources["pact_slots"] = slotCount;
                            resolved.Resources["pact_slot_level"] = spellLevel;
                        }
                    }
                }
            }
        }
        
        private void ApplyProficiencyGrant(ProficiencyGrant grant, ProficiencySet proficiencies)
        {
            if (grant == null) return;
            
            if (grant.SavingThrows != null)
                foreach (var s in grant.SavingThrows)
                    if (Enum.TryParse<AbilityType>(s, true, out var st)) proficiencies.SavingThrows.Add(st);
            
            if (grant.Skills != null)
                foreach (var s in grant.Skills)
                    if (Enum.TryParse<Skill>(s, true, out var sk)) proficiencies.Skills.Add(sk);
            
            if (grant.Expertise != null)
                foreach (var s in grant.Expertise)
                    if (Enum.TryParse<Skill>(s, true, out var sk)) proficiencies.Expertise.Add(sk);
            
            if (grant.WeaponCategories != null)
                foreach (var s in grant.WeaponCategories)
                    if (Enum.TryParse<WeaponCategory>(s, true, out var wc)) proficiencies.WeaponCategories.Add(wc);
            
            if (grant.Weapons != null)
                foreach (var s in grant.Weapons)
                    if (Enum.TryParse<WeaponType>(s, true, out var wt)) proficiencies.Weapons.Add(wt);
            
            if (grant.ArmorCategories != null)
                foreach (var s in grant.ArmorCategories)
                    if (Enum.TryParse<ArmorCategory>(s, true, out var ac)) proficiencies.ArmorCategories.Add(ac);
        }
    }
    
    /// <summary>
    /// The fully resolved character — all features applied, all stats computed.
    /// </summary>
    public class ResolvedCharacter
    {
        public string Name { get; set; }
        public CharacterSheet Sheet { get; set; }
        
        // Final ability scores (after all bonuses)
        public Dictionary<AbilityType, int> AbilityScores { get; set; } = new()
        {
            { AbilityType.Strength, 10 },
            { AbilityType.Dexterity, 10 },
            { AbilityType.Constitution, 10 },
            { AbilityType.Intelligence, 10 },
            { AbilityType.Wisdom, 10 },
            { AbilityType.Charisma, 10 }
        };
        
        public int GetModifier(AbilityType ability) => CharacterSheet.GetModifier(AbilityScores[ability]);
        
        // Computed stats
        public int MaxHP { get; set; }
        public int BaseAC { get; set; } = 10;
        public float Speed { get; set; } = 30f;
        public float DarkvisionRange { get; set; } = 0f;
        
        // Proficiencies
        public ProficiencySet Proficiencies { get; set; } = new();
        
        // Resistances & Immunities
        public HashSet<DamageType> DamageResistances { get; set; } = new();
        public HashSet<DamageType> DamageImmunities { get; set; } = new();
        public HashSet<string> ConditionImmunities { get; set; } = new();
        
        // Resources (rage charges, ki points, etc.)
        public Dictionary<string, int> Resources { get; set; } = new();
        
        // All features applied
        public List<Feature> Features { get; set; } = new();
        
        // All ability IDs from features
        public List<string> AllAbilities { get; set; } = new();
        
        // Extra attacks from class features
        public int ExtraAttacks { get; set; } = 0;
        
        /// <summary>
        /// Get the skill check bonus for a skill.
        /// </summary>
        public int GetSkillBonus(Skill skill, int proficiencyBonus)
        {
            var ability = GetSkillAbility(skill);
            int abilityMod = GetModifier(ability);
            
            if (Proficiencies.HasExpertise(skill))
                return abilityMod + proficiencyBonus * 2;
            if (Proficiencies.IsProficientInSkill(skill))
                return abilityMod + proficiencyBonus;
            return abilityMod;
        }
        
        /// <summary>
        /// Get the saving throw bonus for an ability.
        /// </summary>
        public int GetSavingThrowBonus(AbilityType ability, int proficiencyBonus)
        {
            int abilityMod = GetModifier(ability);
            if (Proficiencies.IsProficientInSave(ability))
                return abilityMod + proficiencyBonus;
            return abilityMod;
        }
        
        /// <summary>
        /// Get the ability that governs a skill.
        /// </summary>
        public static AbilityType GetSkillAbility(Skill skill) => skill switch
        {
            Skill.Athletics => AbilityType.Strength,
            Skill.Acrobatics or Skill.SleightOfHand or Skill.Stealth => AbilityType.Dexterity,
            Skill.Arcana or Skill.History or Skill.Investigation or Skill.Nature or Skill.Religion => AbilityType.Intelligence,
            Skill.AnimalHandling or Skill.Insight or Skill.Medicine or Skill.Perception or Skill.Survival => AbilityType.Wisdom,
            Skill.Deception or Skill.Intimidation or Skill.Performance or Skill.Persuasion => AbilityType.Charisma,
            _ => AbilityType.Intelligence
        };
    }
}
